# DraftView - Get Dropbox refresh token
# Run from solution root: C:\Users\alast\source\repos\DraftView
# Usage: .\get-dropbox-refresh-token.ps1

$ErrorActionPreference = "Stop"

Write-Host "DraftView - Dropbox OAuth token refresh" -ForegroundColor Cyan
Write-Host ""

# Read AppKey and AppSecret from user secrets
Write-Host "Reading credentials from user secrets..." -ForegroundColor Gray
$secrets = dotnet user-secrets list --project DraftView.Web
$appKey    = ($secrets | Where-Object { $_ -match "^Dropbox:AppKey\s*=" }) -replace "^Dropbox:AppKey\s*=\s*", ""
$appSecret = ($secrets | Where-Object { $_ -match "^Dropbox:AppSecret\s*=" }) -replace "^Dropbox:AppSecret\s*=\s*", ""

if ([string]::IsNullOrWhiteSpace($appKey) -or [string]::IsNullOrWhiteSpace($appSecret)) {
    Write-Host "Could not read from user secrets. Enter manually:" -ForegroundColor Yellow
    $appKey    = Read-Host "Enter your Dropbox App Key"
    $appSecret = Read-Host "Enter your Dropbox App Secret"
}

$appKey    = $appKey.Trim()
$appSecret = $appSecret.Trim()

Write-Host "App Key: $($appKey.Substring(0, [Math]::Min(6, $appKey.Length)))..." -ForegroundColor Gray
Write-Host ""

# Step 1: Open browser to authorisation URL
$authUrl = "https://www.dropbox.com/oauth2/authorize?client_id=$appKey&response_type=code&token_access_type=offline"
Write-Host "Opening Dropbox authorisation page..." -ForegroundColor Cyan
Start-Process $authUrl
Write-Host "If the browser didn't open, visit:" -ForegroundColor Gray
Write-Host "  $authUrl" -ForegroundColor White
Write-Host ""

# Step 2: Get the auth code from user
$authCode = Read-Host "Paste the authorisation code from Dropbox"
$authCode  = $authCode.Trim()

# Step 3: Exchange for tokens
Write-Host ""
Write-Host "Exchanging code for tokens..." -ForegroundColor Cyan

$body  = "code=$authCode&grant_type=authorization_code"
$creds = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${appKey}:${appSecret}"))

$response = Invoke-RestMethod `
    -Uri "https://api.dropbox.com/oauth2/token" `
    -Method Post `
    -Headers @{ Authorization = "Basic $creds" } `
    -ContentType "application/x-www-form-urlencoded" `
    -Body $body

$accessToken  = $response.access_token
$refreshToken = $response.refresh_token

Write-Host ""
Write-Host "Tokens received." -ForegroundColor Green
Write-Host "Access token:  $($accessToken.Substring(0, 20))..." -ForegroundColor Gray
Write-Host "Refresh token: $($refreshToken.Substring(0, 20))..." -ForegroundColor Gray
Write-Host ""

# Step 4: Save to user secrets
Write-Host "Saving to user secrets..." -ForegroundColor Cyan
dotnet user-secrets set "Dropbox:AccessToken"  $accessToken  --project DraftView.Web
dotnet user-secrets set "Dropbox:RefreshToken" $refreshToken --project DraftView.Web

Write-Host ""
Write-Host "Done. Restart the app and sync." -ForegroundColor Green