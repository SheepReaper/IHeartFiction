[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-OriginRemoteConfigured {
  try {
    $null = git remote get-url origin 2>$null
    return $LASTEXITCODE -eq 0
  }
  catch {
    return $false
  }
}

function Add-OriginRemoteIfPossible {
  if (Get-OriginRemoteConfigured) {
    return $true
  }

  if ($env:GITHUB_REPOSITORY) {
    $remoteUrl = "https://github.com/$($env:GITHUB_REPOSITORY).git"
    Write-Host "Configuring origin from GITHUB_REPOSITORY: $remoteUrl" -ForegroundColor Cyan
    git remote add origin $remoteUrl
    return (Get-OriginRemoteConfigured)
  }

  $gh = Get-Command gh -ErrorAction SilentlyContinue
  if ($gh) {
    $repoName = gh repo view --json nameWithOwner --jq .nameWithOwner 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($repoName)) {
      $remoteUrl = "https://github.com/$repoName.git"
      Write-Host "Configuring origin from GitHub CLI context: $remoteUrl" -ForegroundColor Cyan
      git remote add origin $remoteUrl
      return (Get-OriginRemoteConfigured)
    }
  }

  return $false
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$sourceGeneratorProject = Join-Path $repoRoot 'src/lib/IHFiction.SourceGenerators/IHFiction.SourceGenerators.csproj'
$localPackageFeed = Join-Path $repoRoot '.artifacts/packages'
$sourceGeneratorPackageId = 'IHFiction.SourceGenerators'
$sourceGeneratorPackageVersion = '0.1.0-local'
$sourceGeneratorPackageFileName = "$sourceGeneratorPackageId.$sourceGeneratorPackageVersion.nupkg"

function Remove-SourceGeneratorPackageFromGlobalCache {
  $globalPackagesLine = dotnet nuget locals global-packages --list |
    Where-Object { $_ -like 'global-packages:*' } |
    Select-Object -First 1

  if ([string]::IsNullOrWhiteSpace($globalPackagesLine)) {
    return
  }

  $globalPackagesPath = $globalPackagesLine.Substring($globalPackagesLine.IndexOf(':') + 1).Trim()
  $cachedPackagePath = Join-Path $globalPackagesPath (Join-Path $sourceGeneratorPackageId.ToLowerInvariant() $sourceGeneratorPackageVersion)

  if (Test-Path -LiteralPath $cachedPackagePath) {
    Write-Host "Removing cached $sourceGeneratorPackageId $sourceGeneratorPackageVersion package..." -ForegroundColor Cyan
    Remove-Item -LiteralPath $cachedPackagePath -Recurse -Force
  }
}

function Get-ZipEntryContentHashes {
  param([Parameter(Mandatory)][string]$PackagePath)

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  Add-Type -AssemblyName System.Security.Cryptography

  $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
  try {
    $entries = New-Object System.Collections.Generic.List[string]
    foreach ($entry in ($archive.Entries | Sort-Object FullName)) {
      if ($entry.FullName -eq '_rels/.rels' -or $entry.FullName -like 'package/services/metadata/core-properties/*.psmdcp') {
        continue
      }

      $sha256 = [System.Security.Cryptography.SHA256]::Create()
      $stream = $entry.Open()
      try {
        $hash = [Convert]::ToHexString($sha256.ComputeHash($stream))
        $entries.Add("$($entry.FullName)|$($entry.Length)|$hash")
      }
      finally {
        $stream.Dispose()
        $sha256.Dispose()
      }
    }

    return $entries.ToArray()
  }
  finally {
    $archive.Dispose()
  }
}

function Test-PackagesHaveSameContent {
  param(
    [Parameter(Mandatory)][string]$FirstPackagePath,
    [Parameter(Mandatory)][string]$SecondPackagePath
  )

  if (-not (Test-Path -LiteralPath $FirstPackagePath) -or -not (Test-Path -LiteralPath $SecondPackagePath)) {
    return $false
  }

  $firstEntries = Get-ZipEntryContentHashes -PackagePath $FirstPackagePath
  $secondEntries = Get-ZipEntryContentHashes -PackagePath $SecondPackagePath

  if ($firstEntries.Count -ne $secondEntries.Count) {
    return $false
  }

  for ($index = 0; $index -lt $firstEntries.Count; $index++) {
    if ($firstEntries[$index] -ne $secondEntries[$index]) {
      return $false
    }
  }

  return $true
}

function Publish-LocalSourceGeneratorPackage {
  New-Item -ItemType Directory -Force -Path $localPackageFeed | Out-Null

  Write-Host "Stopping .NET build servers before repacking local analyzers..." -ForegroundColor Cyan
  dotnet build-server shutdown
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet build-server shutdown failed."
  }

  Write-Host "Restoring source generator package dependencies..." -ForegroundColor Cyan
  dotnet restore $sourceGeneratorProject
  if ($LASTEXITCODE -ne 0) {
    throw "source generator restore failed."
  }

  Write-Host "Packing source generator for local restore..." -ForegroundColor Cyan
  $stagingFeed = Join-Path ([System.IO.Path]::GetTempPath()) "ihfiction-source-generator-package-$([guid]::NewGuid())"
  $stagedPackage = Join-Path $stagingFeed $sourceGeneratorPackageFileName
  $publishedPackage = Join-Path $localPackageFeed $sourceGeneratorPackageFileName

  try {
    New-Item -ItemType Directory -Force -Path $stagingFeed | Out-Null
    dotnet pack $sourceGeneratorProject --no-restore -p:PackageOutputPath=$stagingFeed
    if ($LASTEXITCODE -ne 0) {
      throw "source generator pack failed."
    }

    if (Test-PackagesHaveSameContent -FirstPackagePath $publishedPackage -SecondPackagePath $stagedPackage) {
      Write-Host "Local source generator package content is unchanged; keeping existing package." -ForegroundColor DarkGray
    }
    else {
      Remove-Item -Path (Join-Path $localPackageFeed "$sourceGeneratorPackageId.*.nupkg") -Force -ErrorAction SilentlyContinue
      Copy-Item -LiteralPath $stagedPackage -Destination $publishedPackage -Force
    }
  }
  finally {
    if (Test-Path -LiteralPath $stagingFeed) {
      Remove-Item -LiteralPath $stagingFeed -Recurse -Force
    }
  }

  Remove-SourceGeneratorPackageFromGlobalCache
}

Write-Host "Running cloud-agent preflight for IHeartFiction..." -ForegroundColor Green

$dotnetVersion = dotnet --version
Write-Host "Detected .NET SDK: $dotnetVersion" -ForegroundColor DarkGray

if (-not (Add-OriginRemoteIfPossible)) {
  Write-Warning "Could not infer origin remote automatically."
  Write-Host "Run: git remote add origin https://github.com/SheepReaper/IHeartFiction.git" -ForegroundColor Yellow
}
else {
  $originUrl = git remote get-url origin
  Write-Host "origin remote: $originUrl" -ForegroundColor DarkGray
}

Publish-LocalSourceGeneratorPackage

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore
if ($LASTEXITCODE -ne 0) {
  throw "dotnet restore failed."
}

Write-Host "Preflight complete." -ForegroundColor Green
