param([string]$Version)
$ErrorActionPreference = 'Stop'
$versionFile = Join-Path $PSScriptRoot 'version.json'
$state = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
if (!$Version) {
    $previous = [version]$state.version
    $Version = if ($state.built) { '{0}.{1}.{2}' -f $previous.Major,$previous.Minor,($previous.Build + 1) } else { $state.version }
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use major.minor.patch, such as 1.0.1.' }
$parsed = [version]$Version
if ($parsed.Major -gt 65534 -or $parsed.Minor -gt 65534 -or $parsed.Build -gt 65534) { throw 'Version components must be between 0 and 65534.' }
$name = "PlexMonitor-$Version"
$executable = Join-Path $PSScriptRoot "$name.exe"
$archive = Join-Path $PSScriptRoot "$name-Windows.zip"
if ((Test-Path -LiteralPath $executable) -or (Test-Path -LiteralPath $archive)) { throw "Version $Version already exists. Choose a new version." }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$assemblyInfo = Join-Path ([IO.Path]::GetTempPath()) ('PlexMonitor-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    @"
using System.Reflection;
[assembly: AssemblyTitle("PlexMonitor")]
[assembly: AssemblyProduct("PlexMonitor")]
[assembly: AssemblyDescription("Plex server recovery and Now Playing monitor")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
[assembly: AssemblyInformationalVersion("$Version")]
"@ | Set-Content -LiteralPath $assemblyInfo -Encoding UTF8
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:$executable "/win32icon:$PSScriptRoot\PlexMonitor.ico" "/resource:$PSScriptRoot\PlexMonitor.ico,PlexMonitor.AppIcon" "/resource:$PSScriptRoot\PlexMonitor-icon.png,PlexMonitor.IconImage" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll /reference:System.Net.Http.dll /reference:System.Security.dll "$PSScriptRoot\PlexMonitor.cs" "$PSScriptRoot\NowPlaying.cs" "$PSScriptRoot\PlexTheme.cs" "$PSScriptRoot\Telemetry.cs" "$PSScriptRoot\WindowUI.cs" $assemblyInfo
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed; version was not advanced.' }
} finally {
    if (Test-Path -LiteralPath $assemblyInfo) { Remove-Item -LiteralPath $assemblyInfo }
}
@{version=$Version; built=$true} | ConvertTo-Json | Set-Content -LiteralPath $versionFile -Encoding UTF8
@"
@echo off
start "PlexMonitor" "%~dp0$name.exe"
"@ | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Launch PlexMonitor.cmd') -Encoding ASCII
$files = @($executable, "$PSScriptRoot\PlexMonitor.cs", "$PSScriptRoot\NowPlaying.cs", "$PSScriptRoot\PlexTheme.cs", "$PSScriptRoot\Telemetry.cs", "$PSScriptRoot\WindowUI.cs", "$PSScriptRoot\Build.ps1", $versionFile, "$PSScriptRoot\README.md", "$PSScriptRoot\Launch PlexMonitor.cmd", "$PSScriptRoot\PlexMonitor.ico", "$PSScriptRoot\PlexMonitor-icon.png", "$PSScriptRoot\BuildIcon.ps1", "$PSScriptRoot\IconPrompt.txt")
foreach($extra in @('APP-GUIDE.md','RunTests.ps1','TestNowPlaying.cs','TestTelemetry.cs','TestWindowLayout.cs','TestAppIcon.cs','TestCaptionRendering.cs')) {
    $candidate=Join-Path $PSScriptRoot $extra
    if(Test-Path -LiteralPath $candidate) { $files += $candidate }
}
Compress-Archive -LiteralPath $files -DestinationPath $archive
Write-Output "Built $executable"
Write-Output "Packaged $archive"






