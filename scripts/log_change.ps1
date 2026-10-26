param(
    [string]$Author = "assistant",
    [string[]]$Files,
    [string]$Summary,
    [string]$Reason,
    [string]$Notes = ""
)

if (-not $Files -or -not $Summary -or -not $Reason) {
    Write-Host "Usage: .\log_change.ps1 -Files 'file1','file2' -Summary 'short summary' -Reason 'why' [-Notes 'extra notes'] [-Author 'name']"
    exit 1
}

$date = (Get-Date).ToString('yyyy-MM-dd')
$entry = "### Date: $date`nAuthor: $Author`nFiles changed:`n"
foreach ($f in $Files) { $entry += "- $f`n" }
$entry += "`nChange summary:`n- $Summary`n`nReason:`n- $Reason`n`nNotes / Testing:`n- $Notes`n`n---`n`
"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$logFile = Join-Path -Path $repoRoot -ChildPath "DEVELOPMENT_LOG.md"

if (-not (Test-Path $logFile)) {
    $logFile = Join-Path -Path $repoRoot -ChildPath "STEPS.md"
}

if (-not (Test-Path $logFile)) {
    Write-Host "Log file not found at $logFile" -ForegroundColor Yellow
    exit 1
}

Add-Content -Path $logFile -Value $entry -Encoding UTF8
Write-Host "Appended entry to $logFile" -ForegroundColor Green
