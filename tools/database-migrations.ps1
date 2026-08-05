<#
.SYNOPSIS
    Manage IHeartFiction EF Core migrations against the running Aspire PostgreSQL container.

.DESCRIPTION
    Discovers the running postgres-* container, reads its published port and credentials,
    and invokes dotnet ef without printing the database password. This script changes
    database state only; it never deletes migration source files.

.PARAMETER Action
    Status lists migrations, Apply updates to the latest or named migration, and Rollback
    updates the database to the required earlier migration. Use target "0" to remove all
    applied migrations.

.PARAMETER TargetMigration
    Optional for Apply. Mandatory for Rollback. Supply the complete migration name shown
    by Status, or "0" for a full rollback.

.PARAMETER ContainerName
    Explicit PostgreSQL container name. When omitted, exactly one running postgres-*
    container with a published 5432/tcp port must exist.

.PARAMETER Database
    PostgreSQL database name. Defaults to the Aspire application database, fiction-db.

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Status

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Apply

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Apply -TargetMigration 20260805000626_AddWorkReadCounts

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Rollback -TargetMigration 20260621001615_AddBrowserReportStorage

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Rollback -TargetMigration 0 -Confirm
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Status', 'Apply', 'Rollback')]
    [string]$Action,

    [Parameter()]
    [ValidatePattern('^(0|[0-9]{14}_[A-Za-z][A-Za-z0-9_]*)$')]
    [string]$TargetMigration,

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$ContainerName,

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$Database = 'fiction-db'
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\lib\IHFiction.Data\IHFiction.Data.csproj'

if ($Action -eq 'Rollback' -and [string]::IsNullOrWhiteSpace($TargetMigration)) {
    throw 'Rollback requires -TargetMigration. Run with -Action Status to find the desired target; use 0 to remove all applied migrations.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'docker is required to discover the running PostgreSQL container.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required to run EF Core migrations.'
}

if ([string]::IsNullOrWhiteSpace($ContainerName)) {
    $candidates = @(docker ps --format '{{.Names}}' | Where-Object { $_ -match '^postgres-' })
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to list running Docker containers.'
    }
    if ($candidates.Count -ne 1) {
        $description = if ($candidates.Count -eq 0) { 'none' } else { $candidates -join ', ' }
        throw "Expected exactly one running postgres-* container, found $description. Supply -ContainerName explicitly."
    }
    $ContainerName = $candidates[0]
}

$inspection = docker inspect $ContainerName | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $inspection.Count -ne 1) {
    throw "Unable to inspect PostgreSQL container '$ContainerName'."
}

$container = $inspection[0]
$portBinding = $container.NetworkSettings.Ports.'5432/tcp'
if ($null -eq $portBinding -or $portBinding.Count -ne 1) {
    throw "Container '$ContainerName' must publish exactly one host port for 5432/tcp."
}

$containerEnvironment = @{}
foreach ($entry in $container.Config.Env) {
    $parts = $entry -split '=', 2
    if ($parts.Count -eq 2) {
        $containerEnvironment[$parts[0]] = $parts[1]
    }
}

$databaseUser = $containerEnvironment['POSTGRES_USER']
$databasePassword = $containerEnvironment['POSTGRES_PASSWORD']
if ([string]::IsNullOrWhiteSpace($databaseUser) -or [string]::IsNullOrWhiteSpace($databasePassword)) {
    throw "Container '$ContainerName' does not expose POSTGRES_USER and POSTGRES_PASSWORD."
}

$hostPort = $portBinding[0].HostPort
$connectionString = "Host=127.0.0.1;Port=$hostPort;Database=$Database;Username=$databaseUser;Password=$databasePassword"
$commonArguments = @(
    '--project', $projectPath,
    '--context', 'FictionDbContext',
    '--connection', $connectionString
)

if ($Action -eq 'Status') {
    Write-Host "Listing migrations for '$Database' in container '$ContainerName' (localhost:$hostPort)..."
    & dotnet ef migrations list @commonArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations list failed with exit code $LASTEXITCODE." }
    exit 0
}

$target = if ([string]::IsNullOrWhiteSpace($TargetMigration)) { 'latest' } else { $TargetMigration }
$operation = if ($Action -eq 'Rollback') { 'Roll back' } else { 'Apply migrations' }
if (-not $PSCmdlet.ShouldProcess("$Database on $ContainerName", "$operation to '$target'")) {
    exit 0
}

Write-Host "$operation for '$Database' in container '$ContainerName' to '$target'..."
$updateArguments = @('ef', 'database', 'update')
if ($target -ne 'latest') {
    $updateArguments += $target
}
$updateArguments += $commonArguments

& dotnet @updateArguments
if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed with exit code $LASTEXITCODE." }

Write-Host 'Migration operation completed. Current migration state:'
& dotnet ef migrations list @commonArguments
if ($LASTEXITCODE -ne 0) { throw "Migration verification failed with exit code $LASTEXITCODE." }
