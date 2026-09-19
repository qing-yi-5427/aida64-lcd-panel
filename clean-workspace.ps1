[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$Apply)

# Defaults to a read-only preview. Preserve source and one current PC/APK copy.
$ErrorActionPreference = 'Stop'
$cleanupRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$version = ([xml](Get-Content -LiteralPath (Join-Path $cleanupRoot 'desktop/PanelDeck.Desktop/PanelDeck.Desktop.csproj') -Raw)).Project.PropertyGroup.Version
$keep = @('PanelDeck-Desktop-Update', "PanelDeck-Android-$version.apk")
$artifactRoot = Join-Path $cleanupRoot 'artifacts'
$currentAssembly = Join-Path $artifactRoot 'PanelDeck-Desktop-Update/PanelDeck.dll'
$currentApk = Join-Path $artifactRoot "PanelDeck-Android-$version.apk"
if (-not (Test-Path -LiteralPath $currentAssembly) -or -not (Test-Path -LiteralPath $currentApk)) {
    throw '未找到要保留的最新版 PC 和 APK，停止清理。请先准备两个完整发行文件。'
}
if ((Get-Item -LiteralPath $currentAssembly).VersionInfo.ProductVersion.Split('+')[0] -ne $version) {
    throw '保留目录的 PC 版本与源码不一致，停止清理。'
}
$targets = @()
if (Test-Path -LiteralPath $artifactRoot) {
    $targets += @(Get-ChildItem -LiteralPath $artifactRoot -Force | Where-Object Name -NotIn $keep | ForEach-Object FullName)
}
$targets += @('.gradle', 'app/build' | ForEach-Object { Join-Path $cleanupRoot $_ })
foreach ($project in @('PanelDeck.Desktop','PanelDeck.MemoryProbe','PanelDeck.PhoneTest','PanelDeck.Tests','PanelDeck.WindowsTests')) {
    foreach ($kind in @('bin','obj')) { $targets += Join-Path $cleanupRoot "desktop/$project/$kind" }
}
$targets = @($targets | Where-Object { Test-Path -LiteralPath $_ })
if ($Apply -and (Get-Process -Name PanelDeck -ErrorAction SilentlyContinue)) {
    throw '请先从托盘完整退出曜屏，再清理构建文件。'
}
# Validate the entire list before the first deletion; do not traverse links.
foreach ($target in $targets) {
    $resolved = [IO.Path]::GetFullPath($target)
    if (-not $resolved.StartsWith($cleanupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "目录越界：$resolved" }
    $relative = $resolved.Substring($cleanupRoot.Length + 1).Replace('\','/')
    $tracked = @(git -C $cleanupRoot ls-files -- $relative)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -ne 0) { throw "无法确认目录可清理，或包含 Git 源文件：$relative" }
    $parts = $relative.Split('/')
    $ancestor = $cleanupRoot
    foreach ($part in $parts) {
        $ancestor = Join-Path $ancestor $part
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "拒绝清理链接：$ancestor" }
    }
    if ((Get-Item -LiteralPath $resolved -Force).PSIsContainer) {
        if (Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint) { throw "目录包含链接：$resolved" }
    }
}
$removedBytes = 0L
foreach ($target in $targets) {
    $item = Get-Item -LiteralPath $target -Force
    $bytes = if ($item.PSIsContainer) { (Get-ChildItem -LiteralPath $target -Recurse -File -Force | Measure-Object Length -Sum).Sum } else { $item.Length }
    if (-not $Apply) {
        [pscustomobject]@{Path=$target.Substring($cleanupRoot.Length + 1); MiB=[math]::Round($bytes/1MB,2)}
    } elseif ($PSCmdlet.ShouldProcess($target, '删除可重新生成的构建文件')) {
        Remove-Item -LiteralPath $target -Recurse -Force
        $removedBytes += $bytes
    }
}
if ($Apply) { Write-Output ('已清理 {0:N2} MiB；保留最新版 PC 目录和 APK。' -f ($removedBytes/1MB)) }
else { Write-Output '以上为预览，尚未删除。确认后运行 ./clean-workspace.ps1 -Apply；也可加 -WhatIf 再次预览。' }
