#Requires -RunAsAdministrator
param([ValidateRange(1024,65535)][int]$Port = 8080, [switch]$Remove)
$ErrorActionPreference = 'Stop'
$ruleName = 'PanelDeck-LAN'
if ($Remove) {
    Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Write-Output '已移除曜屏的局域网防火墙规则。'
    return
}
$exe = Join-Path $PSScriptRoot 'PanelDeck.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '请在完整发行包目录中运行。' }
Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -Name $ruleName -DisplayName '曜屏局域网面板' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Program $exe -Profile Private -RemoteAddress LocalSubnet | Out-Null
Write-Output "已允许曜屏在专用网络中使用 TCP $Port，仅限本地子网。"
