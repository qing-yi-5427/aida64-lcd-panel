param([switch]$IncludeDriver, [switch]$FrameworkDependent, [string]$OutputDirectory)
$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectVersion = ([xml](Get-Content (Join-Path $PSScriptRoot 'PanelDeck.Desktop/PanelDeck.Desktop.csproj') -Raw)).Project.PropertyGroup.Version
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root $(if ($FrameworkDependent) { 'artifacts/PanelDeck-Desktop-Lite' } else { "artifacts/PanelDeck-Desktop-$projectVersion" }) }
$existingAssembly = Join-Path $output 'PanelDeck.dll'
if (Test-Path -LiteralPath $existingAssembly) {
    try { $publishGuard = [IO.File]::Open($existingAssembly, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $publishGuard.Dispose() }
    catch { throw '目标目录中的曜屏仍在运行或文件不可写，请完整退出后再发布。未修改发行文件。' }
}
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish (Join-Path $PSScriptRoot 'PanelDeck.Desktop') -c Release -r win-x64 --self-contained $selfContained -p:CopyDebugSymbolFilesFromPackages=false -p:DebugType=None -p:DebugSymbols=false -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw '电脑端构建失败' }
$bootstrapCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $bootstrapCompiler)) { throw '构建启动检查器需要 Windows 自带的 .NET Framework 4 编译器' }
& $bootstrapCompiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$output/PanelDeck.Launcher.exe" "/win32icon:$PSScriptRoot/PanelDeck.Desktop/Assets/paneldeck.ico" "/win32manifest:$PSScriptRoot/PanelDeck.Desktop/app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'RuntimeBootstrap/Launcher.cs') (Join-Path $PSScriptRoot 'RuntimeBootstrap/RuntimePrerequisites.cs')
if ($LASTEXITCODE -ne 0) { throw '启动检查器构建失败' }
Copy-Item (Join-Path $PSScriptRoot 'Install-Runtime.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') (Join-Path $output '使用说明.md') -Force
Copy-Item (Join-Path $PSScriptRoot 'VALIDATION.md') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY.md') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'licenses') $output -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'research') $output -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'Install-Collector.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Configure-LAN.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Setup-Windows.ps1') $output -Force
if ($IncludeDriver) {
    $driver = Join-Path $root 'artifacts/PawnIO_setup.exe'
    if (-not (Test-Path $driver)) {
        Invoke-WebRequest 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe' -OutFile $driver
    }
    $signature = Get-AuthenticodeSignature $driver
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'namazso') { throw '驱动签名验证失败' }
    Copy-Item $driver $output -Force
}
Write-Output "已构建：$output/PanelDeck.exe"
if ($FrameworkDependent) {
    Write-Output '精简版请使用 PanelDeck.Launcher.exe 启动；缺少运行时时提供安装入口，主动点击安装后才会请求管理员权限。'
}
