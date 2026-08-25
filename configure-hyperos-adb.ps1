$ErrorActionPreference = "Stop"

$packageName = "com.paneldeck.aida"
$sdkAdb = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
$adbCommand = Get-Command adb -ErrorAction SilentlyContinue

if ($adbCommand) {
    $adb = $adbCommand.Source
} elseif (Test-Path -LiteralPath $sdkAdb) {
    $adb = $sdkAdb
} else {
    throw "未找到 adb。请安装 Android Platform Tools，或将 adb 加入 PATH。"
}

$devices = & $adb devices
if (-not ($devices -match "\tdevice$")) {
    throw "未发现已授权的 Android 设备。请连接手机并确认 USB 调试授权。"
}

& $adb shell cmd appops set --user 0 $packageName SCHEDULE_EXACT_ALARM allow
& $adb shell cmd appops set --user 0 $packageName TURN_SCREEN_ON allow
& $adb shell cmd appops set --user 0 $packageName 10020 allow
& $adb shell cmd appops set --user 0 $packageName 10021 allow
& $adb shell cmd deviceidle whitelist "+$packageName"
& $adb shell am set-inactive $packageName false

$state = & $adb shell cmd appops get --user 0 $packageName
$lockScreenAllowed = $state -match "MIUIOP\(10020\): allow"
$backgroundAllowed = $state -match "MIUIOP\(10021\): allow"
$exactAlarmAllowed = $state -match "SCHEDULE_EXACT_ALARM: allow"

if (-not ($lockScreenAllowed -and $backgroundAllowed -and $exactAlarmAllowed)) {
    Write-Host $state
    throw "授权未完全生效。请保持手机解锁后重新运行脚本。"
}

Write-Host "曜屏的 HyperOS 唤醒权限已配置完成。"
Write-Host "锁屏显示: 允许；后台启动: 允许；精确闹钟: 允许；电池优化白名单: 已加入。"
