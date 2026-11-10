$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$version=(Get-Content (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json).version
$app=Join-Path $PSScriptRoot "PlexMonitor-$version.exe"
if (!(Test-Path -LiteralPath $app)) { throw "Build PlexMonitor $version before running tests." }
$testFolder=Join-Path $PSScriptRoot '.test-bin'
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
Copy-Item -LiteralPath $app -Destination $testFolder -Force
Push-Location $PSScriptRoot
try {
 foreach($name in @('TestNowPlaying','TestTelemetry','TestWindowLayout','TestAppIcon','TestCaptionRendering')) {
  $testExe=Join-Path $testFolder "$name.exe"
  & $compiler /nologo /out:$testExe /reference:$app /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot "$name.cs")
  if($LASTEXITCODE -ne 0) {throw "$name compilation failed"}
  & $testExe
  if($LASTEXITCODE -ne 0) {throw "$name failed"}
 }
} finally {Pop-Location}
