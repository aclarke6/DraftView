param(
    [string]$RepoPath = "C:\Users\alast\source\repos\DraftView"
)

$RepoPathForWsl = $RepoPath -replace '\\', '/'
$wslPath = wsl -d Ubuntu -- wslpath -a "$RepoPathForWsl"

if (-not $wslPath) {
    Write-Error "Failed to convert Windows path to WSL path."
    exit 1
}

$wslPath = $wslPath.Trim()

wsl -d Ubuntu -- bash -lc "source ~/.nvm/nvm.sh && cd '$wslPath' && codex"