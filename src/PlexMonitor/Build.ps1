param([string]$Version,[string]$DotNetPath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(!$DotNetPath) {
    $installed=Get-Command dotnet -ErrorAction SilentlyContinue
    if($installed) {$DotNetPath=$installed.Source}
    else {$DotNetPath=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'work\dotnet\dotnet.exe'}
}
if(!(Test-Path -LiteralPath $DotNetPath)) {throw 'Install the .NET 10 SDK, or pass -DotNetPath with its dotnet.exe path.'}
if(!$OutputDirectory) {$OutputDirectory=Split-Path $PSScriptRoot -Parent}
$stateFile=Join-Path $PSScriptRoot 'version.json'
$state=Get-Content $stateFile -Raw | ConvertFrom-Json
if(!$Version) {$previous=[version]$state.version; $Version=if($state.built){'{0}.{1}.{2}' -f $previous.Major,$previous.Minor,($previous.Build+1)}else{$state.version}}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a major.minor.patch version.'}
$name="PlexMonitor-$Version"
$executable=Join-Path $OutputDirectory "$name.exe"
$publish=Join-Path $PSScriptRoot "bin\single-file-$Version"
if(Test-Path -LiteralPath $executable){throw "Version $Version already exists. Choose a new version."}
& $DotNetPath restore (Join-Path $PSScriptRoot 'PlexMonitor.csproj') --configfile (Join-Path $PSScriptRoot 'NuGet.Config') --verbosity minimal
if($LASTEXITCODE -ne 0){throw 'Restore failed; version unchanged.'}
& $DotNetPath publish (Join-Path $PSScriptRoot 'PlexMonitor.csproj') -c Release --no-restore -p:Version=$Version -o $publish --verbosity minimal
if($LASTEXITCODE -ne 0){throw 'Publish failed; version unchanged.'}
$files=@(Get-ChildItem -LiteralPath $publish -File -Recurse)
if($files.Count -ne 1 -or $files[0].Name -ne "$name.exe"){throw 'Expected exactly one bundled executable; delivery was not updated.'}
Copy-Item -LiteralPath $files[0].FullName -Destination $executable
@{version=$Version;built=$true} | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding UTF8
Write-Output "Built single executable $executable"
