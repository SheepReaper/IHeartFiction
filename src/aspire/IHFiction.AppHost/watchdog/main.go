package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"
)

const dockerApiVersion = "v1.41"

type target struct {
	service string
	host    string
	port    string
	restart bool
}

type watcher struct {
	docker       *dockerClient
	targets      []target
	threshold    int
	cooldown     time.Duration
	failures     map[string]int
	lastRecovery map[string]time.Time
	mu           sync.Mutex
}

type dockerClient struct {
	baseURL string
	http    *http.Client
}

type dockerService struct {
	ID      string                     `json:"ID"`
	Version struct{ Index uint64 }     `json:"Version"`
	Spec    map[string]json.RawMessage `json:"Spec"`
}

type serviceMode struct {
	Replicated *struct {
		Replicas *uint64 `json:"Replicas"`
	} `json:"Replicated"`
}

type serviceTaskTemplate struct {
	ForceUpdate   uint64 `json:"ForceUpdate"`
	ContainerSpec *struct {
		Image string `json:"Image"`
	} `json:"ContainerSpec"`
}

func main() {
	interval := envDuration("WATCHDOG_INTERVAL_SECONDS", 15)
	threshold := envInt("WATCHDOG_FAILURE_THRESHOLD", 3)
	w := &watcher{
		docker:       newDockerClient(),
		targets:      parseTargets(os.Getenv("WATCHDOG_TARGETS")),
		threshold:    threshold,
		cooldown:     envDuration("WATCHDOG_RECOVERY_COOLDOWN_SECONDS", 300),
		failures:     map[string]int{},
		lastRecovery: map[string]time.Time{},
	}
	if len(w.targets) == 0 {
		log.Fatal("WATCHDOG_TARGETS is empty")
	}
	if threshold < 1 || interval < time.Second || w.cooldown < time.Second {
		log.Fatal("watchdog interval, failure threshold, and cooldown must be positive")
	}
	go w.serveHealth()
	log.Printf("watchdog started targets=%d interval=%s threshold=%d", len(w.targets), interval, threshold)
	w.check(context.Background())
	for ticker := time.NewTicker(interval); ; <-ticker.C {
		w.check(context.Background())
	}
}

func newDockerClient() *dockerClient {
	host := strings.TrimSpace(os.Getenv("DOCKER_HOST"))
	if host == "" {
		host = "tcp://ihfiction-watchdog-socket-proxy:2375"
	}
	if !strings.HasPrefix(host, "tcp://") {
		log.Fatalf("unsupported DOCKER_HOST %q; expected tcp:// socket proxy", host)
	}
	address := strings.TrimPrefix(host, "tcp://")
	dialer := &net.Dialer{Timeout: 5 * time.Second}
	transport := &http.Transport{
		DialContext: func(ctx context.Context, _, _ string) (net.Conn, error) {
			return dialer.DialContext(ctx, "tcp", address)
		},
	}
	return &dockerClient{
		baseURL: "http://docker/" + dockerApiVersion,
		http:    &http.Client{Transport: transport, Timeout: 20 * time.Second},
	}
}

func (w *watcher) serveHealth() {
	http.HandleFunc("/alive", func(rw http.ResponseWriter, _ *http.Request) {
		rw.WriteHeader(http.StatusOK)
		_, _ = rw.Write([]byte("alive"))
	})
	log.Fatal(http.ListenAndServe(":8081", nil))
}

func (w *watcher) check(ctx context.Context) {
	for _, t := range w.targets {
		probeCtx, cancel := context.WithTimeout(ctx, 4*time.Second)
		err := probe(probeCtx, t)
		cancel()
		w.mu.Lock()
		if err == nil {
			if w.failures[t.service] > 0 {
				log.Printf("probe recovered service=%s", t.service)
			}
			w.failures[t.service] = 0
			w.mu.Unlock()
			continue
		}
		w.failures[t.service]++
		count := w.failures[t.service]
		w.mu.Unlock()
		log.Printf("probe failed service=%s endpoint=%s:%s failures=%d/%d error=%q", t.service, t.host, t.port, count, w.threshold, err)
		if count >= w.threshold && t.restart {
			w.recover(t)
		}
	}
}

func probe(ctx context.Context, t target) error {
	resolver := net.Resolver{}
	addresses, err := resolver.LookupHost(ctx, t.host)
	if err != nil {
		return fmt.Errorf("DNS lookup: %w", err)
	}
	var failures []string
	for _, ip := range addresses {
		conn, err := (&net.Dialer{Timeout: 2 * time.Second}).DialContext(ctx, "tcp", net.JoinHostPort(ip, t.port))
		if err == nil {
			_ = conn.Close()
			return nil
		}
		failures = append(failures, err.Error())
	}
	if len(failures) == 0 {
		return errors.New("DNS returned no addresses")
	}
	return fmt.Errorf("connect to resolved service IPs: %s", strings.Join(failures, "; "))
}

func (w *watcher) recover(t target) {
	w.mu.Lock()
	if until := w.lastRecovery[t.service].Add(w.cooldown); time.Now().Before(until) {
		log.Printf("recovery suppressed service=%s cooldown_remaining=%s", t.service, time.Until(until).Round(time.Second))
		w.mu.Unlock()
		return
	}
	w.mu.Unlock()

	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	services, err := w.docker.listServices(ctx)
	if err != nil {
		log.Printf("recovery failed service=%s stage=service-list error=%q", t.service, err)
		return
	}
	var found []dockerService
	for _, service := range services {
		var serviceName string
		if err := json.Unmarshal(service.Spec["Name"], &serviceName); err == nil && serviceName == t.service {
			found = append(found, service)
		}
	}
	if len(found) != 1 {
		log.Printf("recovery aborted service=%s matching_services=%d", t.service, len(found))
		return
	}
	service := found[0]
	var mode serviceMode
	if err := json.Unmarshal(service.Spec["Mode"], &mode); err != nil || mode.Replicated == nil || mode.Replicated.Replicas == nil || *mode.Replicated.Replicas != 1 {
		log.Printf("recovery aborted service=%s reason=automatic-recovery-requires-single-replica", t.service)
		return
	}
	var taskTemplate map[string]json.RawMessage
	if err := json.Unmarshal(service.Spec["TaskTemplate"], &taskTemplate); err != nil {
		log.Printf("recovery aborted service=%s reason=unexpected-service-spec", t.service)
		return
	}
	var taskTemplateFields serviceTaskTemplate
	if err := json.Unmarshal(service.Spec["TaskTemplate"], &taskTemplateFields); err != nil || taskTemplateFields.ContainerSpec == nil || taskTemplateFields.ContainerSpec.Image == "" {
		log.Printf("recovery aborted service=%s reason=unexpected-service-spec", t.service)
		return
	}
	updatedForceUpdate, err := json.Marshal(taskTemplateFields.ForceUpdate + 1)
	if err != nil {
		log.Printf("recovery failed service=%s stage=encode-force-update error=%q", t.service, err)
		return
	}
	taskTemplate["ForceUpdate"] = updatedForceUpdate
	updatedTaskTemplate, err := json.Marshal(taskTemplate)
	if err != nil {
		log.Printf("recovery failed service=%s stage=encode-task-template error=%q", t.service, err)
		return
	}
	service.Spec["TaskTemplate"] = updatedTaskTemplate
	if err := w.docker.updateService(ctx, service); err != nil {
		log.Printf("recovery failed service=%s stage=service-update error=%q", t.service, err)
		return
	}
	w.mu.Lock()
	w.lastRecovery[t.service] = time.Now()
	w.failures[t.service] = 0
	w.mu.Unlock()
	log.Printf("recovery requested service=%s action=swarm-service-update", t.service)
}

func (c *dockerClient) listServices(ctx context.Context) ([]dockerService, error) {
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, c.baseURL+"/services", nil)
	if err != nil {
		return nil, err
	}
	response, err := c.http.Do(request)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(io.LimitReader(response.Body, 4096))
		return nil, fmt.Errorf("Docker API returned %s: %s", response.Status, strings.TrimSpace(string(body)))
	}
	var services []dockerService
	if err := json.NewDecoder(response.Body).Decode(&services); err != nil {
		return nil, fmt.Errorf("decode Docker services response: %w", err)
	}
	return services, nil
}

func (c *dockerClient) updateService(ctx context.Context, service dockerService) error {
	body, err := json.Marshal(service.Spec)
	if err != nil {
		return fmt.Errorf("encode service spec: %w", err)
	}
	endpoint := fmt.Sprintf("%s/services/%s/update?version=%d", c.baseURL, service.ID, service.Version.Index)
	request, err := http.NewRequestWithContext(ctx, http.MethodPost, endpoint, strings.NewReader(string(body)))
	if err != nil {
		return err
	}
	request.Header.Set("Content-Type", "application/json")
	response, err := c.http.Do(request)
	if err != nil {
		return err
	}
	defer response.Body.Close()
	if response.StatusCode < http.StatusOK || response.StatusCode >= http.StatusMultipleChoices {
		responseBody, _ := io.ReadAll(io.LimitReader(response.Body, 4096))
		return fmt.Errorf("Docker API returned %s: %s", response.Status, strings.TrimSpace(string(responseBody)))
	}
	return nil
}

func parseTargets(raw string) []target {
	var targets []target
	for _, item := range strings.Split(raw, ",") {
		parts := strings.Split(item, ":")
		if len(parts) != 4 || parts[0] == "" || parts[1] == "" || parts[2] == "" {
			log.Fatalf("invalid watchdog target %q; expected service:host:port:restart", item)
		}
		if _, err := strconv.Atoi(parts[2]); err != nil {
			log.Fatalf("invalid watchdog target port %q", parts[2])
		}
		restart, err := strconv.ParseBool(parts[3])
		if err != nil {
			log.Fatalf("invalid watchdog restart value %q", parts[3])
		}
		targets = append(targets, target{service: parts[0], host: parts[1], port: parts[2], restart: restart})
	}
	return targets
}

func envInt(name string, fallback int) int {
	value, err := strconv.Atoi(os.Getenv(name))
	if err != nil {
		return fallback
	}
	return value
}
func envDuration(name string, fallback int) time.Duration {
	return time.Duration(envInt(name, fallback)) * time.Second
}
