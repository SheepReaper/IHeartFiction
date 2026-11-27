package main

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

func TestRecoveryPreservesServiceSpecAndForcesUpdate(t *testing.T) {
	updates := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case r.Method == http.MethodGet && r.URL.Path == "/v1.41/services":
			w.Header().Set("Content-Type", "application/json")
			_, _ = w.Write([]byte(`[{"ID":"serviceid","Version":{"Index":42},"Spec":{"Name":"ihfiction_fiction","Mode":{"Replicated":{"Replicas":1}},"TaskTemplate":{"ForceUpdate":7,"ContainerSpec":{"Image":"example:latest","Env":["KEY=value"]},"Networks":[{"Target":"net"}]},"Labels":{"keep":"yes"}}}]`))
		case r.Method == http.MethodPost && r.URL.Path == "/v1.41/services/serviceid/update":
			updates++
			if r.URL.Query().Get("version") != "42" {
				t.Error("missing service version")
			}
			var spec map[string]any
			if err := json.NewDecoder(r.Body).Decode(&spec); err != nil {
				t.Fatal(err)
			}
			task := spec["TaskTemplate"].(map[string]any)
			if task["ForceUpdate"] != float64(8) || task["Networks"] == nil || spec["Labels"] == nil {
				t.Errorf("service spec changed unexpectedly: %#v", spec)
			}
			container := task["ContainerSpec"].(map[string]any)
			if container["Env"] == nil {
				t.Error("container environment lost")
			}
		default:
			t.Errorf("unexpected request: %s %s", r.Method, r.URL.String())
			w.WriteHeader(http.StatusNotFound)
		}
	}))
	defer server.Close()
	w := &watcher{docker: &dockerClient{baseURL: server.URL + "/v1.41", http: server.Client()}, threshold: 3, cooldown: time.Minute, failures: map[string]int{}, lastRecovery: map[string]time.Time{}}
	target := target{service: "ihfiction_fiction", host: "fiction", port: "8080", restart: true}
	w.recover(target)
	w.recover(target)
	if updates != 1 {
		t.Errorf("updates = %d; want 1 with cooldown", updates)
	}
}
