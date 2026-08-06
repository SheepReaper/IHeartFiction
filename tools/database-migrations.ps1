<#
.SYNOPSIS
    Manage IHeartFiction EF Core migrations against the running Aspire PostgreSQL container.

.DESCRIPTION
    Discovers the running postgres-* container, reads its published port and credentials,
    and invokes dotnet ef without printing the database password. This script changes
    database state only; it never deletes migration source files.

.PARAMETER Action
    Status lists migrations, Apply updates to the latest or named migration, Rollback
    updates the database to the required earlier migration, and Preflight checks
    credential alignment/non-mutating connectivity. Use target "0" to remove all
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

.EXAMPLE
    ./tools/database-migrations.ps1 -Action Preflight
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Status', 'Apply', 'Rollback', 'Preflight')]
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

function Get-ApplicationSchemaName {
    $schemasPath = Join-Path $PSScriptRoot '..\src\lib\IHFiction.Data\Schemas.cs'
    if (-not (Test-Path $schemasPath)) {
        throw "Unable to locate schema definition file at '$schemasPath'."
    }

    $match = Select-String -Path $schemasPath -Pattern 'Application\s*=\s*"([^"]+)"'
    if ($null -eq $match) {
        throw "Unable to determine application schema name from '$schemasPath'."
    }

    return $match.Matches[0].Groups[1].Value
}

function Get-AspirePostgresPassword {
    if (-not (Get-Command aspire -ErrorAction SilentlyContinue)) {
        return $null
    }

    $secretValue = & aspire secret get 'Parameters:postgres-password' --non-interactive 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $secret = ($secretValue | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($secret)) {
        return $null
    }

    return $secret
}

function Test-ContainerDatabaseCredential {
    param(
        [Parameter(Mandatory)]
        [string]$ContainerName,

        [Parameter(Mandatory)]
        [string]$Database,

        [Parameter(Mandatory)]
        [string]$DatabaseUser,

        [Parameter(Mandatory)]
        [string]$DatabasePassword
    )

    & docker exec '-e' "PGPASSWORD=$DatabasePassword" $ContainerName `
        psql -h 127.0.0.1 -U $DatabaseUser -d $Database -tAc 'select 1' 2>$null | Out-Null

    return $LASTEXITCODE -eq 0
}

function Repair-ContainerPostgresPassword {
    param(
        [Parameter(Mandatory)]
        [string]$ContainerName,

        [Parameter(Mandatory)]
        [string]$DatabaseUser,

        [Parameter(Mandatory)]
        [string]$DesiredPassword,

        [Parameter(Mandatory)]
        [string]$PgData
    )

    $pgHbaPath = "$PgData/pg_hba.conf"
    $backupPath = "$PgData/pg_hba.conf.bak"
    $escapedPassword = $DesiredPassword.Replace("'", "''")
    $patchCommand = "cp '$pgHbaPath' '$backupPath' && { printf 'local all all trust\nhost all all 127.0.0.1/32 trust\nhost all all ::1/128 trust\n'; cat '$backupPath'; } > '$pgHbaPath'"

    & docker exec -u 0 $ContainerName sh -lc $patchCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to prepare temporary trust authentication for PostgreSQL container '$ContainerName'."
    }

    try {
        & docker kill --signal HUP $ContainerName | Out-Null

        & docker exec $ContainerName `
            psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "ALTER ROLE \"$DatabaseUser\" WITH PASSWORD '$escapedPassword';"

        if ($LASTEXITCODE -ne 0) {
            throw "Unable to reset password for PostgreSQL role '$DatabaseUser' in container '$ContainerName'."
        }
    }
    finally {
        & docker exec -u 0 $ContainerName sh -lc "if [ -f '$backupPath' ]; then mv '$backupPath' '$pgHbaPath'; fi"
        & docker kill --signal HUP $ContainerName | Out-Null
    }
}

function Get-AllMigrations {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    $migrationLines = & dotnet ef migrations list --project $ProjectPath --context FictionDbContext
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations list failed with exit code $LASTEXITCODE."
    }

    return @($migrationLines | Where-Object { $_ -match '^[0-9]{14}_[A-Za-z][A-Za-z0-9_]*$' })
}

function Get-AppliedMigrations {
    param(
        [Parameter(Mandatory)]
        [string]$ContainerName,

        [Parameter(Mandatory)]
        [string]$Database,

        [Parameter(Mandatory)]
        [string]$DatabaseUser,

        [Parameter(Mandatory)]
        [string]$DatabasePassword,

        [Parameter(Mandatory)]
        [string]$SchemaName
    )

    $tableQuery = "SELECT to_regclass('""$SchemaName"".""__EFMigrationsHistory""');"
    $tableName = (& docker exec '-e' "PGPASSWORD=$DatabasePassword" $ContainerName `
        psql -h 127.0.0.1 -U $DatabaseUser -d $Database -tAc $tableQuery 2>$null | Out-String).Trim()

    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query migration history table existence in database '$Database'."
    }

    if ([string]::IsNullOrWhiteSpace($tableName)) {
        return @()
    }

    $historyQuery = "SELECT migration_id FROM ""$SchemaName"".""__EFMigrationsHistory"" ORDER BY migration_id;"
    $applied = & docker exec '-e' "PGPASSWORD=$DatabasePassword" $ContainerName `
        psql -h 127.0.0.1 -U $DatabaseUser -d $Database -tAc $historyQuery

    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query applied EF migrations from database '$Database'."
    }

    return @($applied | Where-Object { $_ -match '^[0-9]{14}_[A-Za-z][A-Za-z0-9_]*$' })
}

function Invoke-MigrationScript {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$ContainerName,

        [Parameter(Mandatory)]
        [string]$Database,

        [Parameter(Mandatory)]
        [string]$DatabaseUser,

        [Parameter(Mandatory)]
        [string]$DatabasePassword,

        [Parameter(Mandatory)]
        [string]$FromMigration,

        [Parameter(Mandatory)]
        [string]$ToMigration
    )

    $scriptFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ihfiction-ef-{0}.sql" -f [Guid]::NewGuid().ToString('N'))
    $containerScriptPath = "/tmp/{0}.sql" -f [System.IO.Path]::GetFileNameWithoutExtension($scriptFile)

    try {
        & dotnet ef migrations script $FromMigration $ToMigration --project $ProjectPath --context FictionDbContext --output $scriptFile
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef migrations script failed with exit code $LASTEXITCODE."
        }

        & docker cp $scriptFile "${ContainerName}:${containerScriptPath}"
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to copy generated migration script into PostgreSQL container '$ContainerName'."
        }

        & docker exec '-e' "PGPASSWORD=$DatabasePassword" $ContainerName `
            psql -h 127.0.0.1 -U $DatabaseUser -d $Database -v ON_ERROR_STOP=1 -f $containerScriptPath

        if ($LASTEXITCODE -ne 0) {
            throw "psql failed while applying migration script in container '$ContainerName'."
        }
    }
    finally {
        if (Test-Path $scriptFile) {
            Remove-Item $scriptFile -Force
        }

        & docker exec $ContainerName rm -f $containerScriptPath 2>$null | Out-Null
    }
}

if ($Action -eq 'Rollback' -and [string]::IsNullOrWhiteSpace($TargetMigration)) {
    throw 'Rollback requires -TargetMigration. Run with -Action Status to find the desired target; use 0 to remove all applied migrations.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'docker is required to discover the running PostgreSQL container.'
}

if ($Action -ne 'Preflight' -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
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
if ([string]::IsNullOrWhiteSpace($databaseUser)) {
    $databaseUser = 'postgres'
}

$aspirePassword = Get-AspirePostgresPassword
$containerPassword = $containerEnvironment['POSTGRES_PASSWORD']

if ($Action -eq 'Preflight') {
    Write-Host "Running PostgreSQL credential preflight for '$Database' in container '$ContainerName' (localhost:$($portBinding[0].HostPort))..."

    $probes = [System.Collections.Generic.List[object]]::new()

    if (-not [string]::IsNullOrWhiteSpace($aspirePassword)) {
        $aspireOk = Test-ContainerDatabaseCredential -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $aspirePassword
        $probes.Add([pscustomobject]@{
                Source   = 'Aspire secret (Parameters:postgres-password)'
                Provided = $true
                Valid    = $aspireOk
            })
    }
    else {
        $probes.Add([pscustomobject]@{
                Source   = 'Aspire secret (Parameters:postgres-password)'
                Provided = $false
                Valid    = $false
            })
    }

    if (-not [string]::IsNullOrWhiteSpace($containerPassword)) {
        $containerOk = Test-ContainerDatabaseCredential -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $containerPassword
        $probes.Add([pscustomobject]@{
                Source   = 'Container env (POSTGRES_PASSWORD)'
                Provided = $true
                Valid    = $containerOk
            })
    }
    else {
        $probes.Add([pscustomobject]@{
                Source   = 'Container env (POSTGRES_PASSWORD)'
                Provided = $false
                Valid    = $false
            })
    }

    $probes | Format-Table -AutoSize | Out-Host

    $validProbeCount = @($probes | Where-Object { $_.Valid }).Count

    if ($validProbeCount -eq 0) {
        throw "Preflight failed: no available credential could authenticate to '$Database' on '$ContainerName'."
    }

    if (@($probes | Where-Object { $_.Source -like 'Aspire secret*' -and $_.Provided -and -not $_.Valid }).Count -gt 0 -and
        @($probes | Where-Object { $_.Source -like 'Container env*' -and $_.Valid }).Count -gt 0) {
        Write-Warning 'Credential drift detected: container password authenticates, but Aspire secret does not.'
    }
    elseif (@($probes | Where-Object { $_.Source -like 'Container env*' -and $_.Provided -and -not $_.Valid }).Count -gt 0 -and
        @($probes | Where-Object { $_.Source -like 'Aspire secret*' -and $_.Valid }).Count -gt 0) {
        Write-Warning 'Credential drift detected: Aspire secret authenticates, but container POSTGRES_PASSWORD does not.'
    }
    else {
        Write-Host 'Preflight passed: at least one configured credential authenticates and no obvious secret/env mismatch was detected.'
    }

    exit 0
}

$databasePassword = $aspirePassword
if ([string]::IsNullOrWhiteSpace($databasePassword)) {
    $databasePassword = $containerPassword
}

if ([string]::IsNullOrWhiteSpace($databasePassword)) {
    throw "Unable to determine the PostgreSQL password. Tried Aspire secret Parameters:postgres-password and the container POSTGRES_PASSWORD environment variable."
}

$pgData = $containerEnvironment['PGDATA']
if ([string]::IsNullOrWhiteSpace($pgData)) {
    $pgData = '/var/lib/postgresql/data'
}

$schemaName = Get-ApplicationSchemaName

if (-not (Test-ContainerDatabaseCredential -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $databasePassword)) {
    Write-Warning "The configured PostgreSQL password does not match the running '$ContainerName' database state. Attempting to repair local password drift."
    Repair-ContainerPostgresPassword -ContainerName $ContainerName -DatabaseUser $databaseUser -DesiredPassword $databasePassword -PgData $pgData

    if (-not (Test-ContainerDatabaseCredential -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $databasePassword)) {
        throw "Unable to authenticate to PostgreSQL container '$ContainerName' after attempting password repair."
    }
}

$hostPort = $portBinding[0].HostPort
$allMigrations = Get-AllMigrations -ProjectPath $projectPath
$appliedMigrations = Get-AppliedMigrations -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $databasePassword -SchemaName $schemaName

if ($Action -eq 'Status') {
    Write-Host "Listing migrations for '$Database' in container '$ContainerName' (localhost:$hostPort)..."
    $appliedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($migration in $appliedMigrations) {
        $null = $appliedSet.Add($migration)
    }

    foreach ($migration in $allMigrations) {
        if ($appliedSet.Contains($migration)) {
            Write-Host "$migration (Applied)"
        }
        else {
            Write-Host $migration
        }
    }

    exit 0
}

$target = if ([string]::IsNullOrWhiteSpace($TargetMigration)) { 'latest' } else { $TargetMigration }
$operation = if ($Action -eq 'Rollback') { 'Roll back' } else { 'Apply migrations' }
if (-not $PSCmdlet.ShouldProcess("$Database on $ContainerName", "$operation to '$target'")) {
    exit 0
}

Write-Host "$operation for '$Database' in container '$ContainerName' to '$target'..."
$fromMigration = if ($appliedMigrations.Count -eq 0) { '0' } else { $appliedMigrations[-1] }
$toMigration = if ($target -eq 'latest') { $allMigrations[-1] } else { $target }

if ($fromMigration -eq $toMigration) {
    Write-Host 'Database is already at the requested migration.'
}
else {
    Invoke-MigrationScript -ProjectPath $projectPath -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $databasePassword -FromMigration $fromMigration -ToMigration $toMigration
}

Write-Host 'Migration operation completed. Current migration state:'
$appliedMigrations = Get-AppliedMigrations -ContainerName $ContainerName -Database $Database -DatabaseUser $databaseUser -DatabasePassword $databasePassword -SchemaName $schemaName
$appliedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($migration in $appliedMigrations) {
    $null = $appliedSet.Add($migration)
}

foreach ($migration in $allMigrations) {
    if ($appliedSet.Contains($migration)) {
        Write-Host "$migration (Applied)"
    }
    else {
        Write-Host $migration
    }
}
