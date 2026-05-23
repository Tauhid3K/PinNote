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

$stepsFile = Join-Path -Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) -ChildPath "..\STEPS.md"
$stepsFile = [System.IO.Path]::GetFullPath($stepsFile)

if (-not (Test-Path $stepsFile)) {
    Write-Host "STEPS.md not found at $stepsFile" -ForegroundColor Yellow
    exit 1
}

Add-Content -Path $stepsFile -Value $entry -Encoding UTF8
Write-Host "Appended entry to STEPS.md" -ForegroundColor Green
