cls
Write-Host "Running test suite..." -ForegroundColor Cyan
Write-Host ""
$tmp = [System.IO.Path]::GetTempFileName()
dotnet test --nologo > $tmp 2>&1
$lines = Get-Content $tmp
Remove-Item $tmp
$lines | clip
$totalPassed = 0; $totalFailed = 0; $totalSkipped = 0
$lines | ForEach-Object {
    if ($_ -match "^Passed!|^Skipped!|^Failed!|warning NU") {
        if ($_ -match "Passed:\s+(\d+)")  { $totalPassed  += [int]$Matches[1] }
        if ($_ -match "Failed:\s+(\d+)")  { $totalFailed  += [int]$Matches[1] }
        if ($_ -match "Skipped:\s+(\d+)") { $totalSkipped += [int]$Matches[1] }
        $colour = if ($_ -match "^Failed!") { "Red" } elseif ($_ -match "^Skipped!|warning NU") { "Yellow" } else { "Green" }
        Write-Host $_ -ForegroundColor $colour
    }
}
if ($totalFailed -gt 0) {
    Write-Host ""
    Write-Host "--- Failed Tests ---" -ForegroundColor Red
    $lines | Where-Object { $_ -match "\[FAIL\]" } | ForEach-Object {
        if ($_ -match "\]\s+(.+)\s+\[FAIL\]") { Write-Host "  FAIL: $($Matches[1])" -ForegroundColor Red }
    }
    $lines | Where-Object { $_ -match "Error Message:" } | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
}
$total = $totalPassed + $totalFailed + $totalSkipped
Write-Host ""
Write-Host "Test summary: total: $total, failed: $totalFailed, succeeded: $totalPassed, skipped: $totalSkipped" -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Green" })
Write-Host "Full output copied to clipboard." -ForegroundColor DarkGray
