<#
.SYNOPSIS
    Terminates Evermail Aspire processes (AppHost, worker, web apps).

.DESCRIPTION
    Useful when Aspire or development services leave dotnet/Node processes running.
    Supports -WhatIf/-Confirm thanks to SupportsShouldProcess, and optional -Force
    to send a more aggressive termination signal.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$Force
)

$dotnetNames = @('dotnet', 'dotnet.exe')
$evermailNamePattern = '(?i)evermail'
$evermailCommandPattern = '(?i)Evermail\.(AppHost|WebApp|AdminApp|IngestionWorker|MigrationService)'

$processes = Get-CimInstance Win32_Process |
    Where-Object {
        if ($_.Name -match $evermailNamePattern) {
            return $true
        }

        $isDotnetHost = $dotnetNames -contains $_.Name.ToLowerInvariant()
        $hasEvermailCommand = $_.CommandLine -and $_.CommandLine -match $evermailCommandPattern

        return $isDotnetHost -and $hasEvermailCommand
    }

if (-not $processes) {
    Write-Host 'No Evermail processes are currently running.'
    return
}

Write-Host "Found $($processes.Count) Evermail-related process(es)."

foreach ($proc in $processes) {
    $label = "{0} (PID {1})" -f $proc.Name, $proc.ProcessId

    if ($PSCmdlet.ShouldProcess($label, 'Stop-Process')) {
        try {
            $stopParams = @{
                Id          = $proc.ProcessId
                ErrorAction = 'Stop'
            }

            if ($Force) {
                $stopParams.Force = $true
            }

            Stop-Process @stopParams
            Write-Host "Stopped $label"
        }
        catch {
            Write-Warning "Failed to stop ${label}: $($_.Exception.Message)"
        }
    }
}

