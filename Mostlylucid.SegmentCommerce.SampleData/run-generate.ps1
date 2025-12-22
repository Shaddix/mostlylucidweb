# SegmentCommerce Sample Data Generator Runner
# Run this script to clear and regenerate sample data

param(
    [switch]$Clear,
    [switch]$SkipClear,
    [switch]$NoOllama,
    [switch]$NoImages,
    [int]$Count = 20,
    [string]$Category = "",
    [switch]$DryRun,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ProjectPath = $PSScriptRoot

if ($Help) {
    Write-Host @"
SegmentCommerce Sample Data Generator

Usage:
    .\run-generate.ps1 [options]

Options:
    -Clear          Clear database before generating (prompts for confirmation)
    -SkipClear      Skip the clear step entirely
    -NoOllama       Skip Ollama enhancement (faster, uses taxonomy-only data)
    -NoImages       Skip image generation (faster)
    -Count <n>      Number of products per category (default: 20)
    -Category <c>   Generate for a specific category only (tech, fashion, home, sport, books, food)
    -DryRun         Preview what would be generated without writing anything
    -Help           Show this help message

Examples:
    .\run-generate.ps1 -Clear -Count 50
    .\run-generate.ps1 -NoOllama -NoImages -Count 10
    .\run-generate.ps1 -Category tech -Count 30
    .\run-generate.ps1 -DryRun
"@
    exit 0
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " SegmentCommerce Sample Data Generator" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Build the project first
Write-Host "[1/4] Building project..." -ForegroundColor Yellow
dotnet build "$ProjectPath\Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green

# Clear database if requested
if ($Clear -and -not $SkipClear -and -not $DryRun) {
    Write-Host ""
    Write-Host "[2/4] Clearing database..." -ForegroundColor Yellow
    dotnet run --project "$ProjectPath\Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --no-build -- clear --confirm
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Clear failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Database cleared!" -ForegroundColor Green
} elseif ($SkipClear) {
    Write-Host ""
    Write-Host "[2/4] Skipping database clear" -ForegroundColor DarkGray
} else {
    Write-Host ""
    Write-Host "[2/4] No clear requested (use -Clear to clear first)" -ForegroundColor DarkGray
}

# Generate products
Write-Host ""
Write-Host "[3/4] Generating sample data..." -ForegroundColor Yellow

$generateArgs = @("generate", "--db", "--count", $Count.ToString())

if ($NoOllama) {
    $generateArgs += "--no-ollama"
}
if ($NoImages) {
    $generateArgs += "--no-images"
}
if ($Category) {
    $generateArgs += "--category"
    $generateArgs += $Category
}
if ($DryRun) {
    $generateArgs += "--dry-run"
}

Write-Host "Running: dotnet run -- $($generateArgs -join ' ')" -ForegroundColor DarkGray
dotnet run --project "$ProjectPath\Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --no-build -- @generateArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Generation failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/4] Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Generation Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Output files saved to: ./Output" -ForegroundColor White
    Write-Host "Run the main app to see the products:" -ForegroundColor White
    Write-Host "  cd ..\Mostlylucid.SegmentCommerce && dotnet run" -ForegroundColor Cyan
}
