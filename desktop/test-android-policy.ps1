$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root 'artifacts/android-policy-tests'
New-Item -ItemType Directory -Force $output | Out-Null
$javaSource = Join-Path $root 'app/src/main/java/com/paneldeck/aida'
$tests = Join-Path $root 'app/src/testStandalone/java/com/paneldeck/aida/DesktopPolicyTest.java'
javac -encoding UTF-8 -d $output (Join-Path $javaSource 'DesktopScreenPolicy.java') (Join-Path $javaSource 'LanAddress.java') (Join-Path $javaSource 'EventStream.java') $tests
if ($LASTEXITCODE -ne 0) { throw 'Android 策略测试编译失败。' }
java -cp $output com.paneldeck.aida.DesktopPolicyTest
if ($LASTEXITCODE -ne 0) { throw 'Android 策略测试失败。' }
javac -encoding UTF-8 -d $output (Join-Path $javaSource 'PanelTheme.java') (Join-Path $root 'app/src/testStandalone/java/com/paneldeck/aida/PanelThemeTest.java')
if ($LASTEXITCODE -ne 0) { throw 'Android 主题测试编译失败。' }
java -cp $output com.paneldeck.aida.PanelThemeTest
if ($LASTEXITCODE -ne 0) { throw 'Android 主题测试失败。' }
