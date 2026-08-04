<#
.SYNOPSIS
    Safely deploy generated IHeartFiction infrastructure to Docker Swarm.

.DESCRIPTION
    Validates the generated Compose files locally, stages them on a Swarm manager,
    validates them remotely, creates timestamped backups, installs them atomically,
    deploys the stack, waits for rollout completion, and verifies public endpoints.

    Secret files are never copied by default. Use -IncludeSecrets to hash-compare and
    install missing files from the local secrets directory with mode 0600. A differing
    existing source is rejected because active Docker Swarm secrets are immutable and
    should be rotated with a new versioned name.

.PARAMETER Source
    Generated infrastructure directory. Defaults to the repository's infra directory.

.PARAMETER Manager
    SSH destination for a Swarm manager. Defaults to ds-2.

.PARAMETER RemotePath
    Shared remote stack configuration directory.

.PARAMETER StackName
    Docker Swarm stack name.

.PARAMETER IncludeSecrets
    Hash-compare secret sources, install missing files, and enforce mode 0600. Secret
    values and hashes are never printed. Differing existing files cause the deployment
    to stop so their Docker secret names can be rotated intentionally.

.PARAMETER SkipVerification
    Skip public HTTP, WebSocket, and fresh-log verification after convergence.

.PARAMETER TimeoutSeconds
    Maximum time to wait for service convergence.

.EXAMPLE
    ./tools/deploy-infra.ps1 -WhatIf

.EXAMPLE
    ./tools/deploy-infra.ps1 -Confirm:$false

.EXAMPLE
    ./tools/deploy-infra.ps1 -IncludeSecrets -Confirm:$false
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter()]
    [System.IO.DirectoryInfo]$Source = (Join-Path $PSScriptRoot '..\infra'),

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9._@-]+$')]
    [string]$Manager = 'ds-2',

    [Parameter()]
    [ValidatePattern('^/[A-Za-z0-9._/-]+$')]
    [string]$RemotePath = '/mnt/swarm/config/ihfiction',

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$StackName = 'ihfiction',

    [Parameter()]
    [switch]$IncludeSecrets,

    [Parameter()]
    [switch]$SkipVerification,

    [Parameter()]
    [ValidateRange(60, 3600)]
    [int]$TimeoutSeconds = 900,

    [Parameter()]
    [ValidateRange(2, 60)]
    [int]$PollIntervalSeconds = 10,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string[]]$HealthUrls = @(
        'https://iheartfiction.net/',
        'https://api.iheartfiction.net/health',
        'https://auth.iheartfiction.net/realms/fiction/.well-known/openid-configuration'
    ),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [uri]$WebSocketUri = 'wss://iheartfiction.net/_blazor'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList,

        [Parameter()]
        [switch]$CaptureOutput
    )

    if ($CaptureOutput) {
        $output = & $FilePath @ArgumentList 2>&1
    }
    else {
        & $FilePath @ArgumentList
        $output = $null
    }

    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath' failed with exit code $LASTEXITCODE."
    }

    return $output
}

function Invoke-RemoteCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter()]
        [switch]$CaptureOutput
    )

    Invoke-NativeCommand -FilePath 'ssh' -ArgumentList @(
        '-o', 'BatchMode=yes',
        '-o', 'ConnectTimeout=10',
        $Manager,
        $Command
    ) -CaptureOutput:$CaptureOutput
}

function Copy-ToRemote {
    param(
        [Parameter(Mandatory)]
        [string[]]$Paths,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    if ($Paths.Count -eq 0) {
        return
    }

    $arguments = @(
        '-o', 'BatchMode=yes',
        '-o', 'ConnectTimeout=10',
        '--'
    ) + $Paths + @("${Manager}:$Destination")

    Invoke-NativeCommand -FilePath 'scp' -ArgumentList $arguments
}

function Wait-ForStackConvergence {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        $rows = @(Invoke-RemoteCommand -CaptureOutput -Command (
            "sudo docker stack services $StackName --format '{{.Name}}|{{.Replicas}}'; " +
            "for service in `$(sudo docker stack services $StackName --format '{{.Name}}'); do " +
            "sudo docker service inspect `"`$service`" --format '{{.Spec.Name}}|update={{if .UpdateStatus}}{{.UpdateStatus.State}}{{else}}none{{end}}|message={{if .UpdateStatus}}{{.UpdateStatus.Message}}{{end}}'; " +
            'done'
        ))

        $replicaRows = @($rows | Where-Object { $_ -match '^[^|]+\|\d+/\d+$' })
        $updateRows = @($rows | Where-Object { $_ -match '\|update=' })
        $failedUpdates = @($updateRows | Where-Object {
            $_ -match '\|update=(paused|rollback_paused|rollback_completed)\|'
        })

        if ($failedUpdates.Count -gt 0) {
            throw "Swarm update failed: $($failedUpdates -join '; ')"
        }

        $replicasConverged = $replicaRows.Count -gt 0
        foreach ($row in $replicaRows) {
            if ($row -notmatch '\|(\d+)/(\d+)$' -or $Matches[1] -ne $Matches[2]) {
                $replicasConverged = $false
                break
            }
        }

        $updatesConverged = @($updateRows | Where-Object {
            $_ -notmatch '\|update=(none|completed)\|'
        }).Count -eq 0

        if ($replicasConverged -and $updatesConverged) {
            Write-Host 'All stack services converged.' -ForegroundColor Green
            return
        }

        Write-Host 'Waiting for health-gated Swarm updates to complete...' -ForegroundColor DarkGray
        Start-Sleep -Seconds $PollIntervalSeconds
    } while ((Get-Date) -lt $deadline)

    throw "Stack did not converge within $TimeoutSeconds seconds."
}

function Test-PublicEndpoints {
    foreach ($url in $HealthUrls) {
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 30 -MaximumRedirection 5
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            throw "Endpoint '$url' returned HTTP $($response.StatusCode)."
        }

        Write-Host "HTTP $($response.StatusCode): $url" -ForegroundColor Green
    }

    $negotiateUri = [uri]::new($WebSocketUri, '?negotiateVersion=1')
    $httpScheme = if ($WebSocketUri.Scheme -eq 'wss') { 'https' } else { 'http' }
    $negotiateBuilder = [UriBuilder]$negotiateUri
    $negotiateBuilder.Scheme = $httpScheme
    $negotiateBuilder.Port = -1
    $negotiateBuilder.Path = "$($WebSocketUri.AbsolutePath)/negotiate"

    $negotiation = Invoke-RestMethod -Method Post -Uri $negotiateBuilder.Uri -TimeoutSec 30
    if ([string]::IsNullOrWhiteSpace($negotiation.connectionToken)) {
        throw 'SignalR negotiation returned no connection token.'
    }

    $socketBuilder = [UriBuilder]$WebSocketUri
    $socketBuilder.Query = "id=$([Uri]::EscapeDataString($negotiation.connectionToken))"
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $cancellation = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(30))

    try {
        $socket.ConnectAsync($socketBuilder.Uri, $cancellation.Token).GetAwaiter().GetResult()
        if ($socket.State -ne [Net.WebSockets.WebSocketState]::Open) {
            throw "WebSocket entered unexpected state '$($socket.State)'."
        }

        Write-Host "WebSocket open: $WebSocketUri" -ForegroundColor Green
    }
    finally {
        $socket.Abort()
        $socket.Dispose()
        $cancellation.Dispose()
    }
}

$sourcePath = $Source.FullName
$composePath = Join-Path $sourcePath 'docker-compose.yaml'
$deployPath = Join-Path $sourcePath 'docker-compose.deploy.yaml'
$secretsPath = Join-Path $sourcePath 'secrets'

foreach ($command in @('docker', 'ssh', 'scp')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command is not available: $command"
    }
}

foreach ($requiredPath in @($composePath, $deployPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required deployment artifact does not exist: $requiredPath"
    }
}

Write-Host 'Validating generated Compose artifacts locally...' -ForegroundColor Cyan
Invoke-NativeCommand -FilePath 'docker' -ArgumentList @(
    'stack', 'config',
    '-c', $composePath,
    '-c', $deployPath
) | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$stagePath = "/tmp/$StackName-deploy-$timestamp"
$remoteComposePath = "$RemotePath/docker-compose.yaml"
$remoteDeployPath = "$RemotePath/docker-compose.deploy.yaml"

if (-not $PSCmdlet.ShouldProcess(
    "${Manager}:$RemotePath",
    "stage, validate, back up, and deploy Swarm stack '$StackName'"
)) {
    return
}

try {
    Write-Host "Checking Swarm manager and shared storage on $Manager..." -ForegroundColor Cyan
    Invoke-RemoteCommand -Command (
        "set -eu; " +
        "test `"`$(sudo docker info --format '{{.Swarm.LocalNodeState}}|{{.Swarm.ControlAvailable}}')`" = 'active|true'; " +
        "sudo docker node ls >/dev/null; " +
        "findmnt -T '$RemotePath' >/dev/null"
    )

    Write-Host "Staging deployment artifacts on $Manager..." -ForegroundColor Cyan
    Invoke-RemoteCommand -Command "set -eu; mkdir -p '$stagePath/secrets'"
    Copy-ToRemote -Paths @($composePath, $deployPath) -Destination "$stagePath/"

    if ($IncludeSecrets) {
        if (-not (Test-Path -LiteralPath $secretsPath -PathType Container)) {
            throw "Secrets directory does not exist: $secretsPath"
        }

        $changedSecrets = [Collections.Generic.List[IO.FileInfo]]::new()
        foreach ($secret in Get-ChildItem -LiteralPath $secretsPath -File) {
            if ($secret.Name -notmatch '^[A-Za-z0-9_.-]+$') {
                throw "Secret filename contains unsupported characters: $($secret.Name)"
            }

            $localHash = (Get-FileHash -LiteralPath $secret.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            $remoteHash = @(Invoke-RemoteCommand -CaptureOutput -Command (
                "if sudo test -f '$RemotePath/secrets/$($secret.Name)'; then " +
                "sudo sha256sum '$RemotePath/secrets/$($secret.Name)' | cut -d' ' -f1; fi"
            )) | Select-Object -First 1

            if ([string]::IsNullOrWhiteSpace($remoteHash)) {
                $changedSecrets.Add($secret)
                Write-Host "Missing secret source scheduled for installation: $($secret.Name)" -ForegroundColor Yellow
            }
            elseif ($localHash -eq $remoteHash) {
                Write-Host "Secret unchanged: $($secret.Name)" -ForegroundColor DarkGray
            }
            else {
                throw (
                    "Remote secret source '$($secret.Name)' differs from the local file. " +
                    'Docker Swarm secrets are immutable; version the secret name in the AppHost ' +
                    'and generated Compose file before deploying the replacement.'
                )
            }
        }

        if ($changedSecrets.Count -gt 0) {
            Copy-ToRemote -Paths @($changedSecrets.FullName) -Destination "$stagePath/secrets/"
        }
    }

    Write-Host 'Validating staged Compose artifacts remotely...' -ForegroundColor Cyan
    Invoke-RemoteCommand -Command (
        "sudo docker stack config -c '$stagePath/docker-compose.yaml' " +
        "-c '$stagePath/docker-compose.deploy.yaml' >/dev/null"
    )

    Write-Host 'Backing up and atomically installing deployment artifacts...' -ForegroundColor Cyan
    Invoke-RemoteCommand -Command (
        "set -eu; sudo mkdir -p '$RemotePath'; " +
        "for file in docker-compose.yaml docker-compose.deploy.yaml; do " +
        "if sudo test -f `"$RemotePath/`$file`"; then " +
        "sudo cp -a `"$RemotePath/`$file`" `"$RemotePath/`$file.pre-deploy-$timestamp`"; fi; done; " +
        "sudo install -m 0644 '$stagePath/docker-compose.yaml' '$RemotePath/.docker-compose.yaml.$timestamp'; " +
        "sudo install -m 0644 '$stagePath/docker-compose.deploy.yaml' '$RemotePath/.docker-compose.deploy.yaml.$timestamp'; " +
        "sudo mv '$RemotePath/.docker-compose.yaml.$timestamp' '$remoteComposePath'; " +
        "sudo mv '$RemotePath/.docker-compose.deploy.yaml.$timestamp' '$remoteDeployPath'"
    )

    if ($IncludeSecrets -and $changedSecrets.Count -gt 0) {
        Invoke-RemoteCommand -Command "set -eu; sudo mkdir -p '$RemotePath/secrets'"
        foreach ($secret in $changedSecrets) {
            Invoke-RemoteCommand -Command (
                "set -eu; " +
                "if sudo test -f '$RemotePath/secrets/$($secret.Name)'; then " +
                "sudo cp -a '$RemotePath/secrets/$($secret.Name)' " +
                "'$RemotePath/secrets/$($secret.Name).pre-deploy-$timestamp'; fi; " +
                "sudo install -m 0600 '$stagePath/secrets/$($secret.Name)' " +
                "'$RemotePath/secrets/.$($secret.Name).$timestamp'; " +
                "sudo mv '$RemotePath/secrets/.$($secret.Name).$timestamp' " +
                "'$RemotePath/secrets/$($secret.Name)'"
            )
        }
    }

    if ($IncludeSecrets) {
        foreach ($secret in Get-ChildItem -LiteralPath $secretsPath -File) {
            Invoke-RemoteCommand -Command (
                "if sudo test -f '$RemotePath/secrets/$($secret.Name)'; then " +
                "sudo chmod 0600 '$RemotePath/secrets/$($secret.Name)'; fi"
            )
        }
    }

    Write-Host "Deploying stack '$StackName'..." -ForegroundColor Cyan
    Invoke-RemoteCommand -Command (
        "sudo docker stack deploy --resolve-image always " +
        "-c '$remoteComposePath' -c '$remoteDeployPath' '$StackName'"
    )

    Wait-ForStackConvergence

    if (-not $SkipVerification) {
        Test-PublicEndpoints

        $logErrors = @(Invoke-RemoteCommand -CaptureOutput -Command (
            "for service in `$(sudo docker stack services '$StackName' --format '{{.Name}}'); do " +
            "sudo docker service logs --since 10m `"`$service`" 2>&1; done | " +
            "grep -Ei 'no such host|name does not resolve|unable to reach the origin|relation .* does not exist|fatal|unhandled exception' || true"
        ))

        if ($logErrors.Count -gt 0) {
            throw "Fresh service logs contain deployment errors:`n$($logErrors -join [Environment]::NewLine)"
        }

        Write-Host 'Fresh deployment log scan is clean.' -ForegroundColor Green
    }

    Write-Host "Deployment completed. Backup suffix: pre-deploy-$timestamp" -ForegroundColor Green
}
finally {
    try {
        Invoke-RemoteCommand -Command (
            "if test -d '$stagePath'; then " +
            "find '$stagePath' -type f -delete; " +
            "rmdir '$stagePath/secrets' 2>/dev/null || true; " +
            "rmdir '$stagePath' 2>/dev/null || true; fi"
        )
    }
    catch {
        Write-Warning "Could not remove remote staging directory '$stagePath': $($_.Exception.Message)"
    }
}
