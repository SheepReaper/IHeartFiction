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

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore
if ($LASTEXITCODE -ne 0) {
  throw "dotnet restore failed."
}

Write-Host "Preflight complete." -ForegroundColor Green
