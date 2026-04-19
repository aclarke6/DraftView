cls
Write-Host "Running test suite..." -ForegroundColor Cyan
Write-Host ""
$result = & cmd /c "dotnet test --nologo 2>&1"
$result | clip
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0
$result | ForEach-Object {
    if ($_ -match "^Passed!|^Skipped!|^Failed!") {
        if ($_ -match "Passed:\s+(\d+)")  { $totalPassed  += [int]$Matches[1] }
        if ($_ -match "Failed:\s+(\d+)")  { $totalFailed  += [int]$Matches[1] }
        if ($_ -match "Skipped:\s+(\d+)") { $totalSkipped += [int]$Matches[1] }
        $colour = if ($_ -match "^Failed!") { "Red" } elseif ($_ -match "^Skipped!") { "Yellow" } else { "Green" }
        Write-Host $_ -ForegroundColor $colour
    }
    elseif ($_ -match "warning NU") {
        Write-Host $_ -ForegroundColor Yellow
    }
}
$total = $totalPassed + $totalFailed + $totalSkipped
$summaryColour = if ($totalFailed -gt 0) { "Red" } else { "Green" }
Write-Host ""
Write-Host "Test summary: total: $total, failed: $totalFailed, succeeded: $totalPassed, skipped: $totalSkipped" -ForegroundColor $summaryColour
Write-Host "Full output copied to clipboard." -ForegroundColor DarkGray
