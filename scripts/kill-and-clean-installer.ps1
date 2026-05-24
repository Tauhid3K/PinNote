$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerPath = Join-Path $repoRoot "artifacts\installer\PinNote-Setup.exe"

if (-not (Test-Path $installerPath)) {
    Write-Host "Installer not found at: $installerPath"
    exit 0
}

Write-Host "Searching for processes that reference the installer..."
$found = @()

Get-Process | ForEach-Object {
    try {
        $p = $_
        foreach ($m in $p.Modules) {
            if ($m.FileName -eq $installerPath -or $m.FileName -like '*PinNote-Setup*') {
                $found += $p
                break
            }
        }
    } catch {
        # ignore access denied for some system processes
    }
}

if ($found.Count -eq 0) {
    $byName = Get-Process -Name 'PinNote-Setup' -ErrorAction SilentlyContinue
    if ($byName) { $found += $byName }
}

if ($found.Count -eq 0) {
    Write-Host "No direct locking processes found; searching common candidates (ISCC, explorer, msiexec, setup, PinNote)..."
    $candidates = Get-Process | Where-Object { $_.ProcessName -match 'ISCC|explorer|msiexec|setup|PinNote' }
    foreach ($p in $candidates) { $found += $p }
}

if ($found.Count -eq 0) {
    Write-Host "No candidate process found to kill. Proceeding to attempt removal anyway."
} else {
    Write-Host "Found processes to stop:"
    $found | Select-Object Id, ProcessName | Format-Table -AutoSize
    foreach ($p in $found | Select-Object -Unique) {
        try {
            Stop-Process -Id $p.Id -Force -ErrorAction Stop
            Write-Host "Stopped process $($p.Id) $($p.ProcessName)"
        } catch {
            Write-Warning "Failed to stop $($p.Id) $($p.ProcessName): $_"
        }
    }
}

Write-Host "Attempting to remove artifacts folder..."
try {
    $artifacts = Join-Path $repoRoot 'artifacts'
    if (Test-Path $artifacts) {
        Remove-Item -LiteralPath $artifacts -Recurse -Force -ErrorAction Stop
        Write-Host "Removed: $artifacts"
    } else {
        Write-Host "Artifacts folder not found: $artifacts"
    }
} catch {
    Write-Warning "Failed to remove artifacts: $_"
}

Write-Host "Done." -ForegroundColor Green
