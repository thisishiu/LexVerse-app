param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('article', 'game', 'letters', 'comic')]
    [string]$Case,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\LexVerse.App\LexVerse.App.csproj'
$executable = Join-Path $repoRoot 'src\LexVerse.App\bin\Debug\net10.0-windows10.0.19041.0\LexVerse.App.exe'
if (-not $SkipBuild) {
    & dotnet build $project --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the app before using -SkipBuild.' }
& $executable --demo --case $Case
