param(
    [switch]$SkipArtifacts
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$bin = Join-Path -Path $repoRoot -ChildPath "bin"
$obj = Join-Path -Path $repoRoot -ChildPath "obj"
$runErr = Join-Path -Path $repoRoot -ChildPath "run.err.txt"
$runOut = Join-Path -Path $repoRoot -ChildPath "run.out.txt"

$paths = @($bin, $obj, $runErr, $runOut)

if (-not $SkipArtifacts) {
    $paths += (Join-Path -Path $repoRoot -ChildPath "artifacts")
}

foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "Removing: $p"
        try {
            Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to remove $p - $_"
        }
    }
    else {
        Write-Host "Not found: $p"
    }
}

Write-Host "Workspace cleanup complete." -ForegroundColor Green
