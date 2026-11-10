param([string]$DotNetPath='dotnet')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'src\PlexMonitor\Tests\CoreTests.csproj'
& $DotNetPath restore $project --configfile "$PSScriptRoot\src\PlexMonitor\NuGet.Config" --verbosity minimal
if($LASTEXITCODE -ne 0){throw 'Test restore failed'}
& $DotNetPath run --project $project --no-restore
if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
