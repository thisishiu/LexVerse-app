param([switch]$SkipBuild)
& (Join-Path $PSScriptRoot 'Start-RecordingCase.ps1') -Case letters -SkipBuild:$SkipBuild
