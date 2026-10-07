param([string]$MsvcRoot, [string]$SdkIncludeRoot, [string]$SdkLibRoot, [switch]$TestOnly)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$MsvcRoot) { $MsvcRoot = Join-Path $root 'artifacts/native-tools/msvc/Contents/VC/Tools/MSVC/14.44.35207' }
if (!$SdkIncludeRoot) { $SdkIncludeRoot = Join-Path $root 'artifacts/native-tools/microsoft.windows.sdk.cpp/c/Include/10.0.28000.0' }
if (!$SdkLibRoot) { $SdkLibRoot = Join-Path $root 'artifacts/native-tools/microsoft.windows.sdk.cpp.x64/c' }
$compiler = Join-Path $MsvcRoot 'bin/Hostx64/x64/cl.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Set MsvcRoot to an x64 MSVC toolset, and provide Windows SDK include/library roots.' }
$output = Join-Path $root 'desktop/PanelDeck.PresentMon/bin'
$obj = Join-Path $root 'artifacts/presentmon-obj'
if ($TestOnly) { $output = Join-Path $root 'artifacts/presentmon-tests'; $obj = Join-Path $output 'obj' }
New-Item -ItemType Directory -Force $output,$obj | Out-Null
$oldInclude = $env:INCLUDE; $oldLib = $env:LIB
try {
    $env:INCLUDE = "$MsvcRoot/include;$SdkIncludeRoot/ucrt;$SdkIncludeRoot/um;$SdkIncludeRoot/shared"
    $env:LIB = "$MsvcRoot/lib/x64;$SdkLibRoot/um/x64;$SdkLibRoot/ucrt/x64"
    $sources = @('Debug.cpp','GpuTrace.cpp','InterPresentActivity.cpp','NvidiaTraceConsumer.cpp','PresentMonTraceConsumer.cpp','TraceConsumer.cpp','PresentMonTraceSession.cpp','TraceLogging.cpp') | ForEach-Object { Join-Path $PSScriptRoot "third_party/PresentMon/PresentData/$_" }
    $mode = if ($TestOnly) { @() } else { @('/LD') }
    $entry = if ($TestOnly) { 'BridgeTests.cpp' } else { 'Bridge.cpp' }
    $binary = if ($TestOnly) { 'BridgeTests.exe' } else { 'PanelDeck.PresentMon.dll' }
    & $compiler /nologo @mode /O2 /MT /EHsc /std:c++20 /DNDEBUG /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /D_WIN32_WINNT=0x0A00 /DNTDDI_VERSION=0x0A000000 /utf-8 /W3 "/Fo$obj/" (Join-Path $PSScriptRoot "PanelDeck.PresentMon/$entry") @sources /link "/OUT:$output/$binary" /OPT:REF /OPT:ICF "/IMPLIB:$output/PanelDeck.PresentMon.lib" advapi32.lib tdh.lib user32.lib
    if ($LASTEXITCODE -ne 0) { throw 'PresentMon bridge compilation failed' }
    if ($TestOnly) {
        & (Join-Path $output $binary)
        if ($LASTEXITCODE -ne 0) { throw 'PresentMon bridge tests failed' }
    }
} finally { $env:INCLUDE = $oldInclude; $env:LIB = $oldLib }
Write-Output "Built $output/$binary"
