param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "PinNote.csproj"
$installerScript = Join-Path $repoRoot "installer\PinNote.iss"
$publishDir = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$packageDir = Join-Path $repoRoot "artifacts\package"

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup compiler not found. Install Inno Setup 6 or update this script with the ISCC.exe path."
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

Push-Location $repoRoot
try {
    dotnet publish $projectPath `
        -f net8.0-windows `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained true `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -o $publishDir

    # 1. Compile Inno Setup installer
    & $iscc "/DMyPublishDir=$publishDir" "/DMyAppVersion=$Version" "/O$installerDir" "/FPinNote-Setup-$Version" $installerScript

    # 2. Package portable .zip archive
    $zipPath = Join-Path $packageDir "PinNote-$Version-$RuntimeIdentifier.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
    Write-Host "=== Portable package ready: $zipPath ==="
}
finally {
    Pop-Location
}