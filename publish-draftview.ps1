param()
# publish-draftview.ps1
#
# ENCODING WARNING - READ BEFORE EDITING:
# This file MUST contain only ASCII characters (U+0000 to U+007F).
# Windows PowerShell 5.1 reads UTF-8 files without BOM as Windows-1252.
# Non-ASCII characters such as em dashes (U+2014) have byte sequences
# that Windows-1252 maps to smart quotes, which PowerShell treats as
# string delimiters -- causing silent parse failures far from the offending
# character. Use plain ASCII hyphens (-) not em dashes, and no curly quotes.
$project  = "C:\Users\alast\source\repos\DraftView\DraftView.Web\DraftView.Web.csproj"
$infra    = "C:\Users\alast\source\repos\DraftView\DraftView.Infrastructure\DraftView.Infrastructure.csproj"
$output   = "C:\Users\alast\publish\draftview"
$bundle   = "C:\Users\alast\publish\efbundle"
$server   = "ubuntu@141.147.71.62"
$key      = "C:\Users\alast\.ssh\draftview-prod.key"
$remote   = "/var/www/draftview"

# ---------------------------------------------------------------------------
# Guard: must be on main branch before publishing
# ---------------------------------------------------------------------------
$currentBranch = git branch --show-current
if ($currentBranch -ne "main") {
    Write-Host "ERROR: You are on branch '$currentBranch'. Switch to main before publishing." -ForegroundColor Red
    exit 1
}
Write-Host "On branch main." -ForegroundColor Green

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
$summary = $testOutput | Select-String -Pattern "^Test summary:"
if ($summary) { Write-Host $summary -ForegroundColor Green }
Write-Host "All tests passed." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Build app
# ---------------------------------------------------------------------------
Write-Host "Cleaning previous publish output..." -ForegroundColor Cyan
if (Test-Path $output) { Remove-Item $output -Recurse -Force }

Write-Host "Publishing DraftView..." -ForegroundColor Cyan
dotnet publish $project -c Release -o $output
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Publish failed." -ForegroundColor Red; exit 1 }

Write-Host "Removing Development config from publish output..." -ForegroundColor Cyan
$devConfig = Join-Path $output "appsettings.Development.json"
if (Test-Path $devConfig) { Remove-Item $devConfig -Force }

# ---------------------------------------------------------------------------
# Build EF migration bundle
# Produces a self-contained Linux-x64 executable that applies all pending
# migrations against the production database. No EF tools needed on the server.
# The bundle picks up the connection string from appsettings.Production.json
# in its working directory when run with ASPNETCORE_ENVIRONMENT=Production.
# ---------------------------------------------------------------------------
Write-Host "Building EF migration bundle..." -ForegroundColor Cyan
if (Test-Path $bundle) { Remove-Item $bundle -Force }
dotnet ef migrations bundle `
    --project $infra `
    --startup-project $project `
    --output $bundle `
    --target-runtime linux-x64 `
    --self-contained `
    --force
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Migration bundle build failed." -ForegroundColor Red; exit 1 }
Write-Host "Migration bundle built." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Copy app to server
# Uses scp because rsync is not natively available on Windows.
# To enable incremental transfers install rsync via Chocolatey (choco install
# rsync) or via WSL, then replace this block with:
#   rsync -az --checksum --delete -e "ssh -i $key" "$output/" "${server}:${remote}/"
# (--checksum is required because dotnet publish always produces fresh
#  timestamps even for unchanged files, so timestamp-based sync copies
#  everything every time.)
# appsettings.json is included in $output and arrives here before the
# migration bundle runs, so the bundle always finds it in its working dir.
# ---------------------------------------------------------------------------
Write-Host "Copying app to server..." -ForegroundColor Cyan
scp -i $key -r "$output/*" "${server}:${remote}"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: SCP of app failed." -ForegroundColor Red; exit 1 }

Write-Host "Copying production appsettings to server..." -ForegroundColor Cyan
$prodConfig = "C:\Users\alast\source\repos\DraftView\appsettings.Production.json"
# File is owned by www-data (640) - open permissions before overwriting, restore below.
ssh -i $key $server "sudo chmod 666 $remote/appsettings.Production.json 2>/dev/null; true"
scp -i $key "$prodConfig" "${server}:${remote}/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: SCP of production appsettings failed." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# Copy migration bundle to server
# ---------------------------------------------------------------------------
Write-Host "Copying migration bundle to server..." -ForegroundColor Cyan
scp -i $key "$bundle" "${server}:/tmp/efbundle"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: SCP of migration bundle failed." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# Apply EF migrations on server
# Runs before service restart so the new schema is in place before the new
# binary starts. All current migrations are additive so the running app is
# unaffected while the bundle executes.
#
# --connection is passed explicitly because the self-contained bundle uses
# EF's design-time host builder, which walks up the directory tree looking
# for a .sln/.slnx file to resolve user-secrets. No solution file exists
# in /var/www/draftview, so the lookup fails with "Solution root not found."
# Passing --connection bypasses the design-time host entirely.
#
# QUOTING: the migration script is a single-quoted PowerShell here-string
# (@'...'@) so PowerShell makes zero changes to its content. It is piped
# to "bash -s" rather than passed as a CLI argument, so all bash quoting
# (single quotes around the python3 -c argument) survives intact.
# set -e ensures any failure propagates as a non-zero exit code.
# ---------------------------------------------------------------------------
Write-Host "Applying EF migrations on server..." -ForegroundColor Cyan
$migrateScript = @'
set -e
chmod +x /tmp/efbundle
CONN=$(python3 -c 'import json; print(json.load(open("/var/www/draftview/appsettings.Production.json"))["ConnectionStrings"]["DefaultConnection"])')
ASPNETCORE_ENVIRONMENT=Production /tmp/efbundle --connection "$CONN"
rm -f /tmp/efbundle
'@
$migrateScript | ssh -i $key $server "bash -s"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Migration bundle failed. Aborting - service NOT restarted." -ForegroundColor Red; exit 1 }
Write-Host "Migrations applied." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Fix permissions
# scp creates new directories with restrictive modes; re-normalize after every
# deploy so wwwroot is traversable by www-data and static files are served.
# ---------------------------------------------------------------------------
Write-Host "Fixing file permissions..." -ForegroundColor Cyan
ssh -i $key $server "sudo find $remote -type d -exec chmod 755 {} \; ; sudo find $remote -type f -exec chmod 644 {} \; ; sudo chown www-data:www-data $remote/appsettings.Production.json ; sudo chmod 640 $remote/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Permission fix failed." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# Restart and verify
# ---------------------------------------------------------------------------
Write-Host "Restarting service..." -ForegroundColor Cyan
ssh -i $key $server "sudo systemctl restart draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Restart failed." -ForegroundColor Red; exit 1 }

Write-Host "Verifying service is running..." -ForegroundColor Cyan
Start-Sleep -Seconds 3
ssh -i $key $server "sudo systemctl is-active draftview"
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Service did not start cleanly." -ForegroundColor Red; exit 1 }
Write-Host "Service is active." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Record deployment in Docs/TASKS.md and commit to main
# ---------------------------------------------------------------------------
Write-Host "Recording deployment in Docs/TASKS.md..." -ForegroundColor Cyan
$repoRoot    = "C:\Users\alast\source\repos\DraftView"
$tasksPath   = "$repoRoot\Docs\TASKS.md"
$deployDate  = Get-Date -Format "yyyy-MM-dd HH:mm"
$commitHash  = git rev-parse --short HEAD
$deployEntry = "Last deployed: $deployDate (commit: $commitHash)"

$tasks = [System.IO.File]::ReadAllText($tasksPath, [System.Text.Encoding]::UTF8)
if ($tasks -match 'Last deployed:') {
    $tasks = $tasks -replace 'Last deployed:[^\r\n]*', $deployEntry
} else {
    $replacement = '$1' + "`r`n" + $deployEntry
    $tasks = $tasks -replace '(Last updated:[^\r\n]*)', $replacement
}

if ($tasks -notmatch [regex]::Escape($deployEntry)) {
    Write-Host "WARNING: Docs/TASKS.md was not updated - check the script." -ForegroundColor Yellow
} else {
    [System.IO.File]::WriteAllText($tasksPath, $tasks, [System.Text.Encoding]::UTF8)
    git pull origin main --ff-only
    git add "Docs/TASKS.md"
    git commit -m "chore: record production deployment $deployDate"
    if ($LASTEXITCODE -eq 0) {
        git push origin main
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARNING: Push failed - commit is local, push manually." -ForegroundColor Yellow
        } else {
            Write-Host "Deployment recorded in Docs/TASKS.md." -ForegroundColor Green
        }
    } else {
        Write-Host "WARNING: Commit failed." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Deploy complete." -ForegroundColor Green
