$projectPath = "EtherChess.csproj"
$outputDir = "dist"

Write-Host "Building EtherChess..."
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $outputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! Executable located at $outputDir\EtherChess.exe"
}
else {
    Write-Host "Build failed."
    exit 1
}
