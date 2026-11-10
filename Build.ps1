param([string]$Version,[string]$DotNetPath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'artifacts'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$arguments=@{OutputDirectory=$OutputDirectory}
if($Version){$arguments.Version=$Version}
if($DotNetPath){$arguments.DotNetPath=$DotNetPath}
& "$PSScriptRoot\src\PlexMonitor\Build.ps1" @arguments
if($LASTEXITCODE){exit $LASTEXITCODE}
Copy-Item -LiteralPath "$PSScriptRoot\src\PlexMonitor\version.json" -Destination "$PSScriptRoot\version.json" -Force
