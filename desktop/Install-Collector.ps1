#Requires -RunAsAdministrator
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$serviceName = 'PanelDeckCollector'
$installDirectory = Join-Path $env:ProgramFiles 'PanelDeck Collector'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($Uninstall) {
    if ($existing) { Stop-Service -Name $serviceName -Force; $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15)) }
    if ($existing) { & sc.exe delete $serviceName | Out-Null; if ($LASTEXITCODE -ne 0) { throw '无法移除采集服务。' } }
    Write-Output '已停止并移除完整采集服务。安装文件和 PawnIO 驱动保留，避免影响其他使用该驱动的软件。'
    return
}
$sourceExe = Join-Path $PSScriptRoot 'PanelDeck.exe'
if (-not (Test-Path -LiteralPath $sourceExe)) { throw '请在完整发行包目录中运行。' }
# Complete preflight before stopping an existing working service.
foreach ($required in @('PanelDeck.dll', 'PanelDeck.deps.json', 'PanelDeck.runtimeconfig.json', 'LibreHardwareMonitorLib.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $required) -PathType Leaf)) { throw "发行包不完整：$required" }
}
$sourceFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in @('.exe', '.dll', '.json') -and $_.Name -ne 'PawnIO_setup.exe' })
if (Test-Path -LiteralPath $installDirectory) {
    $reparse = Get-ChildItem -LiteralPath $installDirectory -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
    if (((Get-Item -LiteralPath $installDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or $reparse) { throw '安装目录包含链接，请先检查安装目录。' }
}
if ($sourceFiles | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw '发行包包含链接，无法安装。' }
if (-not (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'PawnIO/PawnIOLib.dll'))) {
    $installer = Join-Path $PSScriptRoot 'PawnIO_setup.exe'
    if (-not (Test-Path -LiteralPath $installer)) { throw '完整采集需要 PawnIO。请先安装官方驱动，或使用包含驱动安装包的发行版。' }
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'namazso') { throw 'PawnIO 安装包签名无效。' }
    $driverProcess = Start-Process -FilePath $installer -ArgumentList '-install','-silent' -WindowStyle Hidden -Wait -PassThru
    if ($driverProcess.ExitCode -ne 0) { throw 'PawnIO 安装未完成。' }
}
if ($existing) { Stop-Service -Name $serviceName -Force; $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15)) }
# Service binaries must live in an administrator-controlled directory, never a user-writable checkout.
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
$acl = New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
    $identity = [Security.Principal.SecurityIdentifier]::new($sid)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
Set-Acl -LiteralPath $installDirectory -AclObject $acl
$sourceFiles | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Force }
$serviceExe = Join-Path $installDirectory 'PanelDeck.exe'
$binaryPath = '"' + $serviceExe + '" --collector'
if ($existing) {
    $serviceInfo = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'"
    $changed = Invoke-CimMethod -InputObject $serviceInfo -MethodName Change -Arguments @{ PathName = $binaryPath; StartMode = 'Automatic'; StartName = 'LocalSystem' }
    if ($changed.ReturnValue -ne 0) { throw "无法更新采集服务：$($changed.ReturnValue)" }
} else {
    New-Service -Name $serviceName -BinaryPathName $binaryPath -DisplayName '曜屏硬件采集' -StartupType Automatic | Out-Null
}
& sc.exe description $serviceName '仅通过本机只读管道提供硬件传感器数据，无网络监听、手机控制或任意命令接口。' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
if ($LASTEXITCODE -ne 0) { throw '无法设置采集服务恢复策略。' }
Start-Service -Name $serviceName
Write-Output '完整采集服务已安装。曜屏主程序保持普通权限，无需重启即可自动连接。'
