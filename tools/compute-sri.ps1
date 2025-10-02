<#
.SYNOPSIS
    Compute an SRI-style base64 digest for a URL or a string payload.

.DESCRIPTION
    Computes a base64-encoded digest suitable for use in integrity attributes
    or CSP hash-sources. The script accepts an algorithm (sha256, sha384, sha512)
    and either a remote URL (fetched in-memory) or a string payload (or pipeline input).
    When a URL is provided the resource is fetched but not written to disk.

    The output format is: "<algorithm>-<base64digest>" (e.g. sha384-Base64Hash)

.PARAMETER Algorithm
    Hash algorithm to use. Allowed values: sha256, sha384, sha512 (case-insensitive).

.PARAMETER Url
    Optional. If provided the script fetches the remote resource and hashes the
    raw bytes. When -Url is provided pipeline input is ignored.

.PARAMETER Payload
    Optional. A string to hash. If used together with pipeline input each piped
    string is hashed and a separate result is emitted.

.INPUTS
    System.String (via -Payload parameter or pipeline input)

.OUTPUTS
    System.String — one line per hashed input of the form "<alg>-<base64>"

.EXAMPLE
    # Hash a remote file (no disk writes)
    pwsh .\compute-sri.ps1 -Algorithm sha384 -Url "https://uicdn.toast.com/editor/3.2.2/toastui-editor-all.min.js"

.EXAMPLE
    # Hash a literal payload
    pwsh .\compute-sri.ps1 -Algorithm sha256 -Payload "hello world"

.EXAMPLE
    # Pipe text into the script
    "hello world" | pwsh .\compute-sri.ps1 -Algorithm sha256

.NOTES
    - Use sha384 for SRI when possible; browsers accept sha256/sha384/sha512.
    - For cross-origin SRI add crossorigin="anonymous" to the <script> tag and
      ensure the remote host sends CORS headers (otherwise the browser may block).
    - Pin to a versioned CDN URL (avoid /latest) to keep integrity stable.
    - The script is written for PowerShell Core / pwsh but works in Windows PowerShell where HttpClient is available.

.LINK
    Subresource Integrity (SRI): https://developer.mozilla.org/en-US/docs/Web/Security/Subresource_Integrity

#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("sha256", "sha384", "sha512", IgnoreCase = $true)]
    [string]$Algorithm,

    [Parameter(Mandatory = $false, Position = 1)]
    [string]$Url,

    [Parameter(Mandatory = $false, ValueFromPipeline = $true, Position = 2)]
    [string]$Payload
)

Begin {
    $ErrorActionPreference = 'Stop'

    switch ($Algorithm.ToLower()) {
        'sha256' { $algoName = 'SHA256' }
        'sha384' { $algoName = 'SHA384' }
        'sha512' { $algoName = 'SHA512' }
        default { throw "Unsupported algorithm: $Algorithm" }
    }

    $httpClient = $null
}

Process {
    # If Url is provided, fetch once and ignore pipeline payloads.
    if ($PSBoundParameters.ContainsKey('Url') -and $Url) {
        if (-not $httpClient) { $httpClient = [System.Net.Http.HttpClient]::new() }

        try {
            $bytes = $httpClient.GetByteArrayAsync($Url).GetAwaiter().GetResult()
        }
        catch {
            Write-Error ("Failed to fetch '{0}': {1}" -f $Url, $_.Exception.Message)
            return
        }

        try {
            $hashAlg = [System.Security.Cryptography.HashAlgorithm]::Create($algoName)
            $digest = $hashAlg.ComputeHash($bytes)
            $b64 = [Convert]::ToBase64String($digest)
            Write-Output ("{0}-{1}" -f $Algorithm.ToLower(), $b64)
        }
        finally {
            if ($hashAlg) { $hashAlg.Dispose() }
        }

        return
    }

    # No Url: support -Payload and pipeline strings.
    if ($null -ne $Payload) {
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]$Payload)
            $hashAlg = [System.Security.Cryptography.HashAlgorithm]::Create($algoName)
            $digest = $hashAlg.ComputeHash($bytes)
            $b64 = [Convert]::ToBase64String($digest)
            Write-Output ("{0}-{1}" -f $Algorithm.ToLower(), $b64)
        }
        finally {
            if ($hashAlg) { $hashAlg.Dispose() }
        }
    }
}

End {
    if ($httpClient) { $httpClient.Dispose() }
}