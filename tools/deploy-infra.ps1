<#
.SYNOPSIS
    Copy infrastructure files to a remote Swarm manager.

.DESCRIPTION
    Copies the contents of the repository's "infra" directory to a remote target using scp.
    This script is intended to help deploy infrastructure configuration (docker-compose, secrets, etc.)
    to a remote swarm manager node. It performs a recursive copy of files under the infra directory.

    Notes:
    - scp must be available on the machine running this script (Windows has curl/ssh/scp in recent builds;
      on older Windows use an SSH client with scp such as OpenSSH).
    - The remote target must be reachable over SSH and you must have permission to write to the destination path.
    - This script does not attempt to manage remote file permissions or service orchestration.
    - Consider running a dry-run in a safe environment before using in production.

.PARAMETER Source
    Directory to copy from. Defaults to the "infra" directory located alongside this script.

.PARAMETER Target
    Remote destination in scp format (user@host:/path). This parameter is mandatory.

.EXAMPLE
    # Copy infra folder to a remote host
    .\deploy-infra.ps1 -Target "swarm-manager.example.com:/home/deploy/ihfiction"

.EXAMPLE
    # Specify a local source directory (useful for testing)
    .\deploy-infra.ps1 -Source "C:\work\iheartfiction\infra" -Target "deploy@10.0.0.5:/mnt/swarm/data/ihfiction"

.INPUTS
    None. (This script does not accept pipeline objects.)

.OUTPUTS
    Writes progress and errors to the host. Returns non-zero exit code on failure.

.NOTES
    - This script performs a simple copy using scp. For production deployments consider:
      - Using rsync over ssh for delta transfers (if available on both ends).
      - Packaging and signing artifacts.
      - Using CI/CD pipeline to push immutable releases to the target.
    - Ensure SSH key-based authentication is configured for the user on the target host to avoid interactive password prompts.

.LINK
    scp documentation: https://man.openbsd.org/scp

#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [System.IO.DirectoryInfo]$Source = (Join-Path -Path (Get-Location) -ChildPath "infra"),

    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Target
)

begin {
    $ErrorActionPreference = 'Stop'
}

process {
    try {
        if (-not $Source.Exists) {
            Write-Error "Source directory does not exist: $($Source.FullName)"
            exit 2
        }

        Write-Host "Preparing to copy contents of '$($Source.FullName)' to '$Target'..." -ForegroundColor Cyan

        # Build the scp command. Use -r for recursive copy.
        # Be mindful: quoting/escaping may be necessary depending on remote path content.
        $scpCommand = @(
            'scp',
            '-r',
            '--',   # end of scp options; helps with paths that start with '-'
            "$($Source.FullName)/*",
            $Target
        ) -join ' '

        Write-Host "Executing: $scpCommand" -ForegroundColor DarkGray

        # Execute scp. Use Start-Process so output streams are visible and exit code is propagated.
        $proc = Start-Process -FilePath scp -ArgumentList @('-r', '--', "$($Source.FullName)/*", $Target) -NoNewWindow -Wait -PassThru

        if ($proc.ExitCode -ne 0) {
            Write-Error "scp failed with exit code $($proc.ExitCode). Check SSH connectivity, permissions, and remote path."
            exit $proc.ExitCode
        }

        Write-Host "Copy completed successfully." -ForegroundColor Green
    }
    catch {
        Write-Error "Error while copying infra files: $($_.Exception.Message)"
        # Re-throw if you want the script to fail the calling process with a non-zero exit code:
        throw
    }
}