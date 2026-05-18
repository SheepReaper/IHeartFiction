$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
  $env:ALLOW_INSECURE_TLS = 'true'

  node verify-pagination-load-more.cjs
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  node verify-social-metadata.cjs
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
finally {
  Pop-Location
}