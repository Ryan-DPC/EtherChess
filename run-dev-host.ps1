$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet run --project EtherChess.csproj -- --dev --name HostPlayer
