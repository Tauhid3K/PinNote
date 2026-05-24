param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "1.0.2"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "PinNote.csproj"
$installerScript = Join-Path $repoRoot "installer\PinNote.iss"
$publishDir = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"
$installerDir = Join-Path $repoRoot "artifacts\installer"

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

Push-Location $repoRoot
try {
    dotnet publish $projectPath `
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
        -o $publishDir

    & $iscc "/DMyPublishDir=$publishDir" "/DMyAppVersion=$Version" "/O$installerDir" "/FPinNote-Setup-$Version" $installerScript
}
finally {
    Pop-Location
}