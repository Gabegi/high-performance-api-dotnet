# ====================================================================
# ApexShop Benchmark & Load Test Runner with Auto-Shutdown
# ====================================================================
# Runs BenchmarkDotNet micro-benchmarks followed by load tests
# Optionally shuts down computer after completion (for overnight runs)
#
# Usage:
#   .\run-tests-overnight.ps1              # Run tests, no shutdown
#   .\run-tests-overnight.ps1 -Shutdown    # Run tests + shutdown after 5min
# ====================================================================

param(
    [switch]$Shutdown = $false,
    [switch]$SkipBenchmarks = $false,
    [switch]$SkipLoadTests = $false
)

$ErrorActionPreference = "Continue"
$startTime = Get-Date

# ====================================================================
# CONFIGURATION
# ====================================================================
$SolutionRoot = $PSScriptRoot
$BenchmarkProject = Join-Path $SolutionRoot "ApexShop.Benchmarks.Micro"
$LoadTestProject = Join-Path $SolutionRoot "ApexShop.LoadTests"
$LogFile = Join-Path $SolutionRoot "test-run-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').log"

# ====================================================================
# HELPER FUNCTIONS
# ====================================================================

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Yellow
    Write-Host " $Message" -ForegroundColor Yellow
    Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Yellow
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Message {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning-Message {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Stop-ApiProcesses {
    Write-Host "Checking for orphaned API processes..." -ForegroundColor Yellow
    
    # Kill any dotnet processes running the API
    $apiProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
                    Where-Object { $_.CommandLine -like "*ApexShop.API*" }
    
    if ($apiProcesses) {
        Write-Warning-Message "Found $($apiProcesses.Count) API process(es) still running"
        foreach ($proc in $apiProcesses) {
            try {
                Stop-Process -Id $proc.Id -Force
                Write-Success "Stopped process $($proc.Id)"
            } catch {
                Write-Warning-Message "Could not stop process $($proc.Id): $_"
            }
        }
    } else {
        Write-Host "No orphaned API processes found" -ForegroundColor Gray
    }
}

# ====================================================================
# PRE-FLIGHT CHECKS
# ====================================================================

Write-Header "ApexShop Test Runner - Pre-Flight Checks"

# Verify we're in the solution root
if (-not (Test-Path (Join-Path $SolutionRoot "ApexShop.sln"))) {
    Write-Error-Message "Not in solution root! Expected to find ApexShop.sln"
    Write-Host "Current directory: $SolutionRoot" -ForegroundColor Red
    exit 1
}
Write-Success "Solution root verified"

# Verify projects exist
if (-not $SkipBenchmarks -and -not (Test-Path $BenchmarkProject)) {
    Write-Error-Message "Benchmark project not found: $BenchmarkProject"
    exit 1
}

if (-not $SkipLoadTests -and -not (Test-Path $LoadTestProject)) {
    Write-Error-Message "Load test project not found: $LoadTestProject"
    exit 1
}

Write-Success "All project directories found"

# Clean up any orphaned API processes from previous runs
Stop-ApiProcesses

Write-Host ""
Write-Host "Starting tests at: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
Write-Host "Logs will be saved to: $LogFile" -ForegroundColor Cyan
Write-Host ""

# Start transcript logging
Start-Transcript -Path $LogFile -Append

# ====================================================================
# STEP 1: Run BenchmarkDotNet (Optional)
# ====================================================================

if (-not $SkipBenchmarks) {
    Write-Header "STEP 1: Running BenchmarkDotNet Micro-Benchmarks"
    
    try {
        Set-Location $BenchmarkProject
        Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray
        Write-Host ""
        
        Write-Host "Starting BenchmarkDotNet (this may take 30-60 minutes)..." -ForegroundColor Yellow
        Write-Host ""
        
        # Run benchmarks with output to console
        dotnet run -c Release
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "BenchmarkDotNet completed successfully!"
        } else {
            Write-Error-Message "BenchmarkDotNet failed with exit code: $LASTEXITCODE"
            Write-Warning-Message "Continuing to load tests anyway..."
        }
    } catch {
        Write-Error-Message "BenchmarkDotNet crashed: $_"
        Write-Warning-Message "Continuing to load tests anyway..."
    } finally {
        Set-Location $SolutionRoot
    }
    
    Write-Host ""
} else {
    Write-Warning-Message "Skipping BenchmarkDotNet (--SkipBenchmarks flag set)"
}

# ====================================================================
# STEP 2: Run Load Tests
# ====================================================================

if (-not $SkipLoadTests) {
    Write-Header "STEP 2: Running NBomber Load Tests"
    
    Write-Host "Load Test Suite includes:" -ForegroundColor Cyan
    Write-Host "  • 3 CRUD scenarios (Get list, Get by ID, Create)" -ForegroundColor Gray
    Write-Host "  • 3 Stress scenarios (High load, Spike, Constant)" -ForegroundColor Gray
    Write-Host "  • Automatic database reseeding" -ForegroundColor Gray
    Write-Host "  • Automatic API startup and health checks" -ForegroundColor Gray
    Write-Host ""
    
    try {
        Set-Location $LoadTestProject
        Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray
        Write-Host ""
        
        Write-Host "Starting load tests (estimated 10-15 minutes)..." -ForegroundColor Yellow
        Write-Host ""
        
        # Run load tests - Program.cs handles everything automatically
        # No input needed - it runs all 6 tests sequentially
        dotnet run -c Release
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Load tests completed successfully!"
        } else {
            Write-Error-Message "Load tests failed with exit code: $LASTEXITCODE"
        }
    } catch {
        Write-Error-Message "Load tests crashed: $_"
    } finally {
        Set-Location $SolutionRoot
        
        # Ensure API process is cleaned up
        Write-Host ""
        Stop-ApiProcesses
    }
    
    Write-Host ""
} else {
    Write-Warning-Message "Skipping load tests (--SkipLoadTests flag set)"
}

# ====================================================================
# SUMMARY
# ====================================================================

$endTime = Get-Date
$duration = $endTime - $startTime

Write-Header "Test Run Complete!"

Write-Host "Started:  $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
Write-Host "Finished: $($endTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
Write-Host "Duration: $([math]::Round($duration.TotalMinutes, 1)) minutes" -ForegroundColor Cyan
Write-Host ""

Write-Host "Results saved to:" -ForegroundColor Cyan
if (-not $SkipBenchmarks) {
    Write-Host "  • Benchmarks: $BenchmarkProject\Reports\" -ForegroundColor Gray
}
if (-not $SkipLoadTests) {
    Write-Host "  • Load Tests: $LoadTestProject\Reports\" -ForegroundColor Gray
}
Write-Host "  • Run Log: $LogFile" -ForegroundColor Gray
Write-Host ""

Stop-Transcript

# ====================================================================
# STEP 3: Shutdown (if requested)
# ====================================================================

if ($Shutdown) {
    Write-Header "SHUTDOWN SEQUENCE INITIATED"
    
    Write-Host "Computer will shut down in 5 minutes" -ForegroundColor Red
    Write-Host "Press Ctrl+C within 5 minutes to cancel" -ForegroundColor Yellow
    Write-Host ""
    
    for ($i = 5; $i -gt 0; $i--) {
        Write-Host "Shutting down in $i minute(s)..." -ForegroundColor Yellow
        Start-Sleep -Seconds 60
    }
    
    Write-Host ""
    Write-Host "Shutting down NOW..." -ForegroundColor Red
    Write-Host ""
    
    Stop-Computer -Force
} else {
    Write-Success "Tests complete! Computer will NOT shut down."
    Write-Host ""
    Write-Host "To enable auto-shutdown for overnight runs:" -ForegroundColor Cyan
    Write-Host "  .\run-tests-overnight.ps1 -Shutdown" -ForegroundColor White
    Write-Host ""
}