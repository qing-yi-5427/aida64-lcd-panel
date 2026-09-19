param([switch]$Desktop, [switch]$AspNet, [string]$ResultPath, [switch]$PlanOnly, [switch]$FunctionsOnly)
$ErrorActionPreference = 'Stop'

function Get-RuntimePackage($Release, [string]$Component) {
    $section = if ($Component -eq 'Desktop') { $Release.windowsdesktop } elseif ($Component -eq 'AspNet') { $Release.'aspnetcore-runtime' } else { throw '未知运行时组件' }
    if ($section.version -notmatch '^10\.0\.\d+$') { throw '微软元数据未返回稳定版 .NET 10 运行时' }
    $prefix = if ($Component -eq 'Desktop') { 'windowsdesktop-runtime' } else { 'aspnetcore-runtime' }
    $files = @($section.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -eq "$prefix-win-x64.exe" })
    if ($files.Count -ne 1) { throw '未找到唯一的 x64 运行时安装包' }
    $file = $files[0]
    $uri = [Uri]$file.url
    if ($uri.Scheme -ne 'https' -or $uri.Port -ne 443 -or $uri.UserInfo -or
        $uri.Host -notin @('builds.dotnet.microsoft.com', 'dotnetcli.blob.core.windows.net', 'download.visualstudio.microsoft.com') -or
        -not $uri.AbsolutePath.EndsWith('.exe') -or $file.hash -notmatch '^[a-fA-F0-9]{128}$') { throw '安装包来源或 SHA-512 校验值无效' }
    [pscustomobject]@{ Component=$Component; Version=$section.version; Url=$uri.AbsoluteUri; Hash=$file.hash; Name="$prefix-$($section.version)-win-x64.exe" }
}

function Assert-RuntimePackage([string]$Path, [string]$ExpectedHash) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash -ne $ExpectedHash) { throw '下载校验失败，未运行安装包。请重试。' }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw '微软数字签名校验失败，未运行安装包。'
    }
}

function Get-InstallerOutcome([int]$Code) {
    switch ($Code) {
        0 { return '完成' }
        3010 { return '完成，Windows 提示需要稍后重启；曜屏不会自动重启' }
        1602 { throw '已取消安装，已有运行时保持不变。可稍后重试缺少项。' }
        1223 { throw '已取消管理员授权，未继续安装。' }
        default { throw "安装未完成（退出码 $Code），请重试或使用微软官方安装程序。" }
    }
}

if ($FunctionsOnly) { return }
$temporaryDirectory = $null
$exitCode = 0
try {
    if (-not $Desktop -and -not $AspNet) { throw '未指定需要安装的运行时组件。' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $metadata = Invoke-RestMethod -Uri 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json' -TimeoutSec 45
    $release = @($metadata.releases | Where-Object { $_.'release-version' -eq $metadata.'latest-release' })
    if ($release.Count -ne 1) { throw '无法确定微软 .NET 10 最新稳定版，请稍后重试。' }
    $packages = @()
    if ($Desktop) { $packages += Get-RuntimePackage $release[0] 'Desktop' }
    if ($AspNet) { $packages += Get-RuntimePackage $release[0] 'AspNet' }
    if ($PlanOnly) { $packages | ConvertTo-Json -Depth 4; exit 0 }
    $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('PanelDeck-runtime-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    # Download and verify all required installers before requesting any elevation.
    foreach ($package in $packages) {
        $target = Join-Path $temporaryDirectory $package.Name
        try { Invoke-WebRequest -UseBasicParsing -Uri $package.Url -OutFile $target -TimeoutSec 300 }
        catch {
            $uri = [Uri]$package.Url
            if ($uri.Host -ne 'builds.dotnet.microsoft.com' -or -not $uri.AbsolutePath.StartsWith('/dotnet/')) { throw }
            # Official mirrored artifact; the same SHA-512 and Authenticode checks still apply.
            Invoke-WebRequest -UseBasicParsing -Uri ('https://dotnetcli.blob.core.windows.net' + $uri.AbsolutePath) -OutFile $target -TimeoutSec 300
        }
        Assert-RuntimePackage $target $package.Hash
    }
    $outcomes = @()
    foreach ($package in $packages) {
        $installer = Start-Process -FilePath (Join-Path $temporaryDirectory $package.Name) -ArgumentList '/install', '/passive', '/norestart' -Verb RunAs -PassThru
        $installer.WaitForExit()
        $outcomes += "$($package.Component) $($package.Version)：$(Get-InstallerOutcome $installer.ExitCode)"
        $installer.Dispose()
    }
    $result = $outcomes -join [Environment]::NewLine
} catch {
    $exitCode = 1
    if ($_.Exception -is [System.ComponentModel.Win32Exception] -and $_.Exception.NativeErrorCode -eq 1223) { $result = '已取消管理员授权，未继续安装。' }
    else { $result = '安装未完成：' + $_.Exception.Message }
} finally {
    if ($temporaryDirectory) {
        # Only remove our exact per-run temporary directory after checking its parent and name.
        $resolved = [IO.Path]::GetFullPath($temporaryDirectory)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if ([IO.Path]::GetDirectoryName($resolved) -eq $temporaryRoot -and [IO.Path]::GetFileName($resolved) -match '^PanelDeck-runtime-[a-f0-9]{32}$') {
            Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
if ($ResultPath) { [IO.File]::WriteAllText($ResultPath, $result, [Text.UTF8Encoding]::new($false)) } else { Write-Output $result }
exit $exitCode
