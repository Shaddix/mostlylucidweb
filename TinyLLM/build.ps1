# TinyLLM Build Script for Windows
# Run this script from PowerShell to build and run the application

Write-Host "🤖 TinyLLM Build Script" -ForegroundColor Cyan
Write-Host ""

# Check if .NET 9 is installed
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 9 SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found .NET SDK version: $dotnetVersion" -ForegroundColor Green

# Check if CUDA is available
$cudaAvailable = $false
$nvidiaSmi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
if ($nvidiaSmi) {
    Write-Host "✅ NVIDIA GPU detected - GPU acceleration available!" -ForegroundColor Green
    $cudaAvailable = $true
} else {
    Write-Host "⚠️  No NVIDIA GPU detected - CPU mode only" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Cyan
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Package restore failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔨 Building application..." -ForegroundColor Cyan
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host "Run the application with:" -ForegroundColor Cyan
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Or find the executable in:" -ForegroundColor Cyan
Write-Host "  bin\Release\net9.0-windows\TinyLLM.exe" -ForegroundColor White
Write-Host ""

$runNow = Read-Host "Would you like to run TinyLLM now? (y/n)"
if ($runNow -eq 'y' -or $runNow -eq 'Y') {
    Write-Host ""
    Write-Host "🚀 Launching TinyLLM..." -ForegroundColor Cyan
    if ($cudaAvailable) {
        Write-Host "💡 Tip: Enable 'Use GPU' in the app for faster inference!" -ForegroundColor Yellow
    }
    Write-Host ""
    dotnet run
}
