$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Cleaning stale build artifacts..."
dotnet clean EtherChess.csproj -v q

Write-Host "Starting EtherChess in dev mode..."
dotnet run --project EtherChess.csproj -- --dev
