[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath,

    [string] $KeyId = "agent-auth-1"
)

$parentPath = Split-Path -Parent $OutputPath
if (-not $parentPath -or -not (Test-Path -LiteralPath $parentPath -PathType Container)) {
    throw "The parent directory for '$OutputPath' does not exist."
}

if (Test-Path -LiteralPath $OutputPath) {
    throw "Refusing to overwrite the existing signing key at '$OutputPath'."
}

$key = [System.Security.Cryptography.ECDsa]::Create(
    [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    [System.IO.File]::WriteAllText(
        $OutputPath,
        $key.ExportECPrivateKeyPem(),
        [System.Text.UTF8Encoding]::new($false))
}
finally {
    $key.Dispose()
}

Write-Host "Created P-256 agent signing key '$KeyId' at '$OutputPath'."
