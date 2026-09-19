$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Install-Runtime.ps1') -FunctionsOnly
function Require([bool]$Condition, [string]$Name) { if (-not $Condition) { throw $Name }; Write-Output "PASS $Name" }
function Rejects([scriptblock]$Action) { try { & $Action | Out-Null; return $false } catch { return $true } }
$file = [pscustomobject]@{ rid='win-x64'; name='windowsdesktop-runtime-win-x64.exe'; url='https://builds.dotnet.microsoft.com/dotnet/test.exe'; hash=('a' * 128) }
$release = [pscustomobject]@{ windowsdesktop=[pscustomobject]@{ version='10.0.12'; files=@($file) } }
$package = Get-RuntimePackage $release 'Desktop'
Require ($package.Version -eq '10.0.12' -and $package.Component -eq 'Desktop') 'Official x64 stable package selected'
$file.url = 'http://builds.dotnet.microsoft.com/dotnet/test.exe'
Require (Rejects { Get-RuntimePackage $release 'Desktop' }) 'Plain HTTP rejected'
$file.url = 'https://example.com/test.exe'
Require (Rejects { Get-RuntimePackage $release 'Desktop' }) 'Unapproved download host rejected'
$file.url = 'https://builds.dotnet.microsoft.com/dotnet/test.exe'; $file.hash = 'abc'
Require (Rejects { Get-RuntimePackage $release 'Desktop' }) 'Missing or invalid SHA-512 rejected'
$file.hash = 'a' * 128; $file.rid = 'win-x86'
Require (Rejects { Get-RuntimePackage $release 'Desktop' }) 'Wrong architecture rejected'
$file.rid = 'win-x64'; $release.windowsdesktop.version = '10.0.0-preview.1'
Require (Rejects { Get-RuntimePackage $release 'Desktop' }) 'Preview package rejected'
Require ((Get-InstallerOutcome 0) -eq '完成') 'Success code handled'
Require ((Get-InstallerOutcome 3010) -match '稍后重启') 'Restart request is reported without rebooting'
Require (Rejects { Get-InstallerOutcome 1602 }) 'User cancellation is not success'
Require (Rejects { Get-InstallerOutcome 1223 }) 'UAC cancellation is not success'
Require (Rejects { Get-InstallerOutcome 1603 }) 'Installation failure is not success'
$fixture = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($fixture, 'not a Microsoft executable')
    Require (Rejects { Assert-RuntimePackage $fixture ('0' * 128) }) 'Tampered download rejected'
    $hash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA512).Hash
    Require (Rejects { Assert-RuntimePackage $fixture $hash }) 'Matching hash without Microsoft signature rejected'
} finally { Remove-Item -LiteralPath $fixture -Force }
