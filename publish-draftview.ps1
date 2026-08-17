# publish-draftview.ps1
$project = "C:\Users\alast\source\repos\DraftView\DraftView.Web\DraftView.Web.csproj"
$output  = "C:\Users\alast\publish\draftview"
$server  = "ubuntu@141.147.71.62"
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

# ---------------------------------------------------------------------------
# Guard: require full test suite to pass before publishing
# ---------------------------------------------------------------------------
$solution = "C:\Users\alast\source\repos\DraftView\DraftView.slnx"
Write-Host "Running full test suite..." -ForegroundColor Cyan
$testOutput = dotnet test $solution --nologo 2>&1
$testOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Test suite failed. Fix all failing tests before publishing." -ForegroundColor Red
    exit 1
}
# Extract and display summary line
$summary = $testOutput | Select-String -Pattern "^Test summary:"
if ($summary) { Write-Host $summary -ForegroundColor Green }
Write-Host "All tests passed." -ForegroundColor Green

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

Write-Host "Copying production appsettings to server..." -ForegroundColor Cyan
$prodConfig = "C:\Users\alast\source\repos\DraftView\appsettings.Production.json"
# The file is owned by www-data (640) from the previous deploy's permission fix,
# so ubuntu cannot overwrite it directly via scp. Open it first, then restore below.
ssh -i $key $server "sudo chmod 666 $remote/appsettings.Production.json 2>/dev/null; true"
scp -i $key "$prodConfig" "${server}:${remote}/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { Write-Host "SCP of production appsettings failed." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# scp creates new remote directories with a restrictive default mode (no
# real umask on the Windows client), which leaves wwwroot untraversable by
# the www-data service account and breaks all static file serving (CSS,
# images). Re-normalize perms after every deploy rather than relying on
# whatever scp happened to create.
# ---------------------------------------------------------------------------
Write-Host "Fixing static file permissions..." -ForegroundColor Cyan
ssh -i $key $server "sudo find $remote -type d -exec chmod 755 {} \; ; sudo find $remote -type f -exec chmod 644 {} \; ; sudo chown www-data:www-data $remote/appsettings.Production.json ; sudo chmod 640 $remote/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { Write-Host "Permission fix failed." -ForegroundColor Red; exit 1 }

Write-Host "Restarting service..." -ForegroundColor Cyan
ssh -i $key $server "sudo systemctl restart draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "Restart failed." -ForegroundColor Red; exit 1 }

Write-Host "Verifying service is running..." -ForegroundColor Cyan
Start-Sleep -Seconds 3
ssh -i $key $server "sudo systemctl is-active draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "Service did not start cleanly." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# Record deployment in TASKS.md and commit to main
# ---------------------------------------------------------------------------
Write-Host "Recording deployment in TASKS.md..." -ForegroundColor Cyan
$repoRoot    = "C:\Users\alast\source\repos\DraftView"
$tasksPath   = "$repoRoot\TASKS.md"
$deployDate  = Get-Date -Format "yyyy-MM-dd"
$commitHash  = git rev-parse --short HEAD
$deployEntry = "Last deployed: $deployDate (commit: $commitHash)"

$tasks = Get-Content $tasksPath -Raw
if ($tasks -match 'Last deployed:') {
    $tasks = $tasks -replace 'Last deployed:[^\r\n]*', $deployEntry
} else {
    $replacement = '$1' + "`r`n" + $deployEntry
    $tasks = $tasks -replace '(Last updated:[^\r\n]*)', $replacement
}

if ($tasks -notmatch [regex]::Escape($deployEntry)) {
    Write-Host "WARNING: TASKS.md was not updated — check the script." -ForegroundColor Yellow
} else {
    [System.IO.File]::WriteAllText($tasksPath, $tasks)
    git add TASKS.md
    git commit -m "chore: record production deployment $deployDate"
    if ($LASTEXITCODE -eq 0) {
        git push origin main
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARNING: Push failed — commit is local, push manually." -ForegroundColor Yellow
        } else {
            Write-Host "Deployment recorded in TASKS.md." -ForegroundColor Green
        }
    } else {
        Write-Host "WARNING: Commit failed." -ForegroundColor Yellow
    }
}

Write-Host "Done." -ForegroundColor Green