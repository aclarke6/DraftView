# publish-draftview.ps1
$project = "C:\Users\alast\source\repos\DraftView\DraftView.Web\DraftView.Web.csproj"
$output  = "C:\Users\alast\publish\draftview"
$server  = "ubuntu@193.123.182.208"
$key     = "C:\Users\alast\.ssh\draftview-prod.key"
$remote  = "/var/www/draftview"

# ---------------------------------------------------------------------------
# Guard: require clean git state before publishing
# ---------------------------------------------------------------------------
Write-Host "Checking git status..." -ForegroundColor Cyan
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "ERROR: Uncommitted changes detected. Commit or stash before publishing:" -ForegroundColor Red
    git status --short
    exit 1
}
Write-Host "Git working tree is clean." -ForegroundColor Green

Write-Host "Cleaning previous publish..." -ForegroundColor Cyan
if (Test-Path $output) { Remove-Item $output -Recurse -Force }

Write-Host "Publishing DraftView..." -ForegroundColor Cyan
dotnet publish $project -c Release -o $output
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." -ForegroundColor Red; exit 1 }

Write-Host "Removing Development config from publish output..." -ForegroundColor Cyan
$devConfig = Join-Path $output "appsettings.Development.json"
if (Test-Path $devConfig) { Remove-Item $devConfig -Force }

Write-Host "Copying to server..." -ForegroundColor Cyan
scp -i $key -r "$output/*" "${server}:${remote}"
if ($LASTEXITCODE -ne 0) { Write-Host "SCP failed." -ForegroundColor Red; exit 1 }

Write-Host "Restarting service..." -ForegroundColor Cyan
ssh -i $key $server "sudo systemctl restart draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "Restart failed." -ForegroundColor Red; exit 1 }

Write-Host "Verifying service is running..." -ForegroundColor Cyan
Start-Sleep -Seconds 3
ssh -i $key $server "sudo systemctl is-active draftview"
Write-Host "Done." -ForegroundColor Green