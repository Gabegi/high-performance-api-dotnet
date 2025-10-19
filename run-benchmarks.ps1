# ====================================================================
# Simple Benchmark & Load Test Runner with Auto-Shutdown
# ====================================================================
# Usage:
#   .\run-benchmarks.ps1                    # Run tests, no shutdown
#   .\run-benchmarks.ps1 -Shutdown          # Run tests + shutdown
# ====================================================================

param(
    [switch]$Shutdown = $false
)

$ErrorActionPreference = "Continue"

# ====================================================================
# STEP 1: Run BenchmarkDotNet
# ====================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "STEP 1: Running BenchmarkDotNet" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Set-Location "ApexShop.Benchmarks.Micro"
dotnet run -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: BenchmarkDotNet failed!" -ForegroundColor Red
    Write-Host "Continuing anyway..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Benchmark completed!" -ForegroundColor Green
Write-Host ""

# ====================================================================
# STEP 2: Run Load Tests
# ====================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "STEP 2: Running Load Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Set-Location "..\ApexShop.LoadTests"

# Auto-select option 1 (Baseline tests)
echo "1" | dotnet run -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Load tests failed!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Load tests completed!" -ForegroundColor Green
Write-Host ""

# Return to root
Set-Location ".."

# ====================================================================
# STEP 3: Shutdown (if requested)
# ====================================================================

if ($Shutdown) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "SHUTTING DOWN IN 5 MINUTES" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Press Ctrl+C to cancel" -ForegroundColor Yellow
    Write-Host ""

    for ($i = 5; $i -gt 0; $i--) {
        Write-Host "Shutting down in $i minute(s)..." -ForegroundColor Yellow
        Start-Sleep -Seconds 60
    }

    Write-Host ""
    Write-Host "Shutting down NOW..." -ForegroundColor Red
    Stop-Computer -Force
} else {
    Write-Host ""
    Write-Host "Tests complete! Computer will NOT shutdown." -ForegroundColor Green
    Write-Host "To enable shutdown, run: .\run-benchmarks.ps1 -Shutdown" -ForegroundColor Cyan
    Write-Host ""
}
