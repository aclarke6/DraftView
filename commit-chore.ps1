param(
    [string]$Message = ""
)
# commit-chore.ps1
#
# ENCODING WARNING - READ BEFORE EDITING:
# This file MUST contain only ASCII characters (U+0000 to U+007F).
# Windows PowerShell 5.1 reads UTF-8 files without BOM as Windows-1252.
# Non-ASCII characters such as em dashes (U+2014) have byte sequences
# that Windows-1252 maps to smart quotes, which PowerShell treats as
# string delimiters -- causing silent parse failures far from the offending
# character. Use plain ASCII hyphens (-) not em dashes, and no curly quotes.
#
# Stages all modified .md files and commits them to main as a chore.
# Usage:
#   .\commit-chore.ps1
#   .\commit-chore.ps1 -Message "chore: update TASKS.md after PR #99 merge"

$repoRoot = "C:\Users\alast\source\repos\DraftView"

# ---------------------------------------------------------------------------
# Guard: must be on main
# ---------------------------------------------------------------------------
$branch = git -C $repoRoot branch --show-current
if ($branch -ne "main") {
    Write-Host "ERROR: on branch '$branch' -- switch to main before committing chores." -ForegroundColor Red
    exit 1
}
Write-Host "On branch main." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Collect all modified/untracked .md files from working tree
# ---------------------------------------------------------------------------
$statusLines = git -C $repoRoot status --porcelain 2>&1
$docFiles = $statusLines |
    Where-Object { $_ -match '\.md$' } |
    ForEach-Object { ($_ -replace '^.{3}', '').Trim() }

if (-not $docFiles) {
    Write-Host "Nothing to commit -- no modified .md files." -ForegroundColor Yellow
    exit 0
}

# ---------------------------------------------------------------------------
# Stage
# ---------------------------------------------------------------------------
foreach ($f in $docFiles) {
    git -C $repoRoot add $f
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: could not stage '$f'" -ForegroundColor Red
        exit 1
    }
}

$staged = git -C $repoRoot diff --cached --name-only
Write-Host "Staged:" -ForegroundColor Cyan
$staged | ForEach-Object { Write-Host "  $_" }

# ---------------------------------------------------------------------------
# Build commit message if not provided
# ---------------------------------------------------------------------------
if ($Message -eq "") {
    $names = ($staged | ForEach-Object { Split-Path $_ -Leaf }) -join ", "
    $Message = "chore: update $names"
}

# ---------------------------------------------------------------------------
# Commit and push
# ---------------------------------------------------------------------------
git -C $repoRoot commit -m $Message
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: commit failed." -ForegroundColor Red
    exit 1
}

git -C $repoRoot push
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: push failed." -ForegroundColor Red
    exit 1
}

Write-Host "Done -- chore committed and pushed to main." -ForegroundColor Green
