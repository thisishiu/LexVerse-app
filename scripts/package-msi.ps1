[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '1.0.0',
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$parsed = [version]::Parse($Version)
if ($parsed.Major -gt 255 -or $parsed.Minor -gt 255 -or $parsed.Build -gt 65535) {
    throw 'MSI version limits: major/minor <= 255, build <= 65535.'
}
$output = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $root "artifacts\msi\$Version\win-x64"
} else {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
if (Test-Path $output) { throw "Output already exists: $output. Choose a new version or move the previous output." }
$publish = Join-Path $output 'publish'
$toolPath = Join-Path $root '.artifacts\tools\wix'
$wix = Join-Path $toolPath 'wix.exe'
if (-not (Test-Path $wix)) {
    & dotnet tool install wix --version 6.0.2 --tool-path $toolPath
    if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed.' }
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
& dotnet publish (Join-Path $root 'src\LexVerse.App\LexVerse.App.csproj') -c Release -r win-x64 --self-contained true -o $publish '-p:DebugSymbols=false' '-p:DebugType=None' '-p:PublishReadyToRun=true' "-p:Version=$Version" "-p:FileVersion=$Version.0"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
foreach ($required in @('LexVerse.App.exe', '.env', 'DesktopUi\index.html')) {
    if (-not (Test-Path (Join-Path $publish $required))) { throw "Missing payload: $required" }
}
$msi = Join-Path $output "LexVerse-$Version-x64.msi"
& $wix build (Join-Path $root 'packaging\msi\Package.wxs') -arch x64 -d "Version=$Version" -d "PublishDir=$publish" -o $msi
if ($LASTEXITCODE -ne 0) { throw 'WiX build failed.' }
$hash = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$msi.sha256", "$hash  $([System.IO.Path]::GetFileName($msi))`n")
Write-Warning 'This MSI is unsigned. Test install, launch, upgrade and uninstall on a clean Windows VM before public release. WebView2 Evergreen Runtime is required.'
Get-Item $msi, "$msi.sha256" | Select-Object FullName, Length
