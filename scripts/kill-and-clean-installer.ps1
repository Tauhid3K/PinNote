$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerDir = Join-Path $repoRoot "artifacts\installer"

Write-Host "Searching for processes that reference PinNote or installer build tools..."
$found = @()

Get-Process | ForEach-Object {
    try {
        $p = $_
        if ($p.ProcessName -match '^PinNote$|^PinNote-Setup|^ISCC$') {
            $found += $p
        }
    } catch {
        # ignore access denied for system processes
    }
}

if ($found.Count -gt 0) {
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
} else {
    Write-Host "No active PinNote or ISCC processes found."
}

Write-Host "Attempting to clean artifacts folder..."
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
