# publish-draftview.ps1
$project = "C:\Users\alast\source\repos\DraftView\DraftView.Web\DraftView.Web.csproj"
$output  = "C:\Users\alast\publish\draftview"
$server  = "ubuntu@193.123.182.208"
$key     = "C:\Users\alast\.ssh\draftview-prod.key"
$remote  = "/var/www/draftview"

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

# Note: migrations run automatically on startup via db.Database.Migrate() in Program.cs
# The above is informational â€” the restart below will apply them

Write-Host "Restarting service..." -ForegroundColor Cyan
ssh -i $key $server "sudo systemctl restart draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "Restart failed." -ForegroundColor Red; exit 1 }

Write-Host "Verifying service is running..." -ForegroundColor Cyan
Start-Sleep -Seconds 3
ssh -i $key $server "sudo systemctl is-active draftview"

Write-Host "Done." -ForegroundColor Green
