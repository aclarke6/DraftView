<#
Files changed by this script:
- DraftView.Web\wwwroot\css\DraftView.Core.css
- DraftView.Web\Views\Shared\_Layout.cshtml
#>

cls
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = 'C:\Users\alast\source\repos\DraftView'
$coreCssPath = Join-Path $repoRoot 'DraftView.Web\wwwroot\css\DraftView.Core.css'
$layoutPath  = Join-Path $repoRoot 'DraftView.Web\Views\Shared\_Layout.cshtml'

if (-not (Test-Path -LiteralPath $coreCssPath)) {
    Write-Host "ERROR: Core CSS file not found: $coreCssPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $layoutPath)) {
    Write-Host "ERROR: Layout file not found: $layoutPath" -ForegroundColor Red
    exit 1
}

$coreOriginal = Get-Content -LiteralPath $coreCssPath -Raw
$layoutOriginal = Get-Content -LiteralPath $layoutPath -Raw

$coreLe = if ($coreOriginal -match "`r`n") { "`r`n" } else { "`n" }
$layoutLe = if ($layoutOriginal -match "`r`n") { "`r`n" } else { "`n" }

$coreVersionMatch = [regex]::Match($coreOriginal, '--css-version:\s*"(?<version>v\d{4}-\d{2}-\d{2}-\d+)";')
if (-not $coreVersionMatch.Success) {
    Write-Host 'ERROR: Could not find --css-version in DraftView.Core.css' -ForegroundColor Red
    exit 1
}

$currentVersion = $coreVersionMatch.Groups['version'].Value
$currentVersionMatch = [regex]::Match($currentVersion, '^v(?<date>\d{4}-\d{2}-\d{2})-(?<count>\d+)$')
if (-not $currentVersionMatch.Success) {
    Write-Host "ERROR: Invalid CSS version format: $currentVersion" -ForegroundColor Red
    exit 1
}

$today = Get-Date
$todayText = $today.ToString('yyyy-MM-dd')
$currentDateText = $currentVersionMatch.Groups['date'].Value
$currentCount = [int]$currentVersionMatch.Groups['count'].Value

$newCount = if ($currentDateText -eq $todayText) { $currentCount + 1 } else { 1 }
$newVersion = "v$todayText-$newCount"

$coreUpdated = [regex]::Replace(
    $coreOriginal,
    '--css-version:\s*"v\d{4}-\d{2}-\d{2}-\d+";',
    ('--css-version: "{0}";' -f $newVersion),
    1
)

if ($coreUpdated -eq $coreOriginal) {
    Write-Host 'ERROR: DraftView.Core.css was not changed' -ForegroundColor Red
    exit 1
}

$layoutUpdated = [regex]::Replace(
    $layoutOriginal,
    '(?<prefix>/css/[^"]+\.css\?v=)v\d{4}-\d{2}-\d{2}-\d+(?<suffix>")',
    ('$1{0}$2' -f $newVersion)
)

if ($layoutUpdated -eq $layoutOriginal) {
    Write-Host 'ERROR: _Layout.cshtml was not changed' -ForegroundColor Red
    exit 1
}

[System.IO.File]::WriteAllText($coreCssPath, $coreUpdated)
[System.IO.File]::WriteAllText($layoutPath, $layoutUpdated)

$coreVerify = Get-Content -LiteralPath $coreCssPath -Raw
$layoutVerify = Get-Content -LiteralPath $layoutPath -Raw

if ($coreVerify -notmatch [regex]::Escape('--css-version: "' + $newVersion + '";')) {
    Write-Host 'ERROR: DraftView.Core.css validation failed' -ForegroundColor Red
    exit 1
}

$layoutMatches = [regex]::Matches($layoutVerify, '/css/[^"]+\.css\?v=' + [regex]::Escape($newVersion))
if ($layoutMatches.Count -lt 1) {
    Write-Host 'ERROR: _Layout.cshtml validation failed' -ForegroundColor Red
    exit 1
}

Write-Host "Updated CSS version: $currentVersion -> $newVersion" -ForegroundColor Green
Write-Host "Validated DraftView.Core.css" -ForegroundColor Green
Write-Host "Validated _Layout.cshtml" -ForegroundColor Green

