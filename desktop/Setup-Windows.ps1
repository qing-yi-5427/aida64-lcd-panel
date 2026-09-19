#Requires -RunAsAdministrator
param([ValidateRange(1024,65535)][int]$Port = 8080, [switch]$Collector)
$ErrorActionPreference = 'Stop'
$logDirectory = Join-Path $env:ProgramData 'PanelDeck'
try {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Start-Transcript -LiteralPath (Join-Path $logDirectory 'setup.log') -Force | Out-Null
    if ($Collector) { & (Join-Path $PSScriptRoot 'Install-Collector.ps1') }
    & (Join-Path $PSScriptRoot 'Configure-LAN.ps1') -Port $Port
    Stop-Transcript | Out-Null
    exit 0
} catch {
    Write-Output $_
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
