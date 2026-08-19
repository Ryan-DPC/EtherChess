param(
    [string]$TargetRoot = "D:\ProjetsLocaux",
    [string]$RepoUrl = "https://github.com/Ryan-DPC/EtherChess.git",
    [string]$Branch = "cursor/audit-remediation-bea0"
)

$ErrorActionPreference = "Stop"
$projectDir = Join-Path $TargetRoot "EtherChess"

Write-Host "EtherChess -> $projectDir"

if (-not (Test-Path $TargetRoot)) {
    New-Item -ItemType Directory -Path $TargetRoot | Out-Null
    Write-Host "Created $TargetRoot"
}

if (Test-Path (Join-Path $projectDir ".git")) {
    Write-Host "Existing repo found. Updating..."
    Set-Location $projectDir
    git fetch origin
    git checkout $Branch
    git pull origin $Branch
}
elseif (Test-Path $projectDir) {
    throw "Folder exists but is not a git repo: $projectDir"
}
else {
    git clone --branch $Branch $RepoUrl $projectDir
    Set-Location $projectDir
}

Write-Host ""
Write-Host "Installed at: $projectDir"
Write-Host "Run dev mode:"
Write-Host "  cd `"$projectDir`""
Write-Host "  dotnet run -- --dev"
Write-Host ""
Write-Host "Build release:"
Write-Host "  .\build.ps1"
