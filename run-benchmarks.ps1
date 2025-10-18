# ====================================================================
# Automated Benchmark Suite with Optional Shutdown
# ====================================================================
# Usage:
#   .\run-benchmarks.ps1                    # Run tests, no shutdown
#   .\run-benchmarks.ps1 -Shutdown          # Run tests, then shutdown in 5 min
#   .\run-benchmarks.ps1 -Shutdown -ShutdownDelayMinutes 10
# ====================================================================

param(
    [switch]$Shutdown = $false,           # Add -Shutdown to enable shutdown
    [int]$ShutdownDelayMinutes = 5        # Wait time before shutdown
)

# Configuration
$SolutionRoot = $PSScriptRoot  # Assumes script is in solution root
$BenchmarkProject = "ApexShop.Benchmarks.Micro"
$LoadTestProject = "ApexShop.LoadTests"
$LogDir = Join-Path $SolutionRoot "BenchmarkResults"
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"

# Create results directory
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# Logging function
function Write-Log {
    param($Message, $Color = "White")
    $LogMessage = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
    Write-Host $LogMessage -ForegroundColor $Color
    Add-Content -Path "$LogDir\run_$Timestamp.log" -Value $LogMessage
}

# ====================================================================
# CHECK PREREQUISITES
# ====================================================================

Write-Log "========================================" "Cyan"
Write-Log "Pre-Flight Checks" "Cyan"
Write-Log "========================================" "Cyan"

# Check if running as Administrator (required for BenchmarkDotNet)
$IsAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Log "WARNING: Not running as Administrator" "Yellow"
    Write-Log "BenchmarkDotNet hardware counters may not work" "Yellow"
    Write-Log "Continuing anyway..." "Yellow"
} else {
    Write-Log "Running as Administrator: Yes" "Green"
}

# Check .NET SDK
try {
    $DotnetVersion = dotnet --version
    Write-Log "✓ .NET SDK found: $DotnetVersion" "Green"
} catch {
    Write-Log "ERROR: .NET SDK not found" "Red"
    exit 1
}

Write-Log ""

# ====================================================================
# START
# ====================================================================

Write-Log "========================================" "Cyan"
Write-Log "Benchmark Suite Started" "Cyan"
Write-Log "========================================" "Cyan"
Write-Log "Timestamp: $Timestamp"
Write-Log "Solution Root: $SolutionRoot"
Write-Log "Shutdown Enabled: $Shutdown"
if ($Shutdown) {
    Write-Log "Shutdown Delay: $ShutdownDelayMinutes minutes" "Yellow"
}
Write-Log ""

$TotalStartTime = Get-Date
$TestsPassed = 0
$TestsFailed = 0

# ====================================================================
# PHASE 1: BenchmarkDotNet (Micro-Benchmarks)
# ====================================================================

Write-Log "========================================" "Yellow"
Write-Log "PHASE 1: Running BenchmarkDotNet" "Yellow"
Write-Log "========================================" "Yellow"

$BenchmarkPath = Join-Path $SolutionRoot $BenchmarkProject
if (-not (Test-Path $BenchmarkPath)) {
    Write-Log "ERROR: Benchmark project not found at $BenchmarkPath" "Red"
    $TestsFailed++
} else {
    Set-Location $BenchmarkPath
    Write-Log "Changed directory to: $BenchmarkPath"

    $BenchmarkStartTime = Get-Date
    Write-Log "Starting BenchmarkDotNet at $(Get-Date -Format 'HH:mm:ss')..."

    try {
        # Run benchmarks and capture output
        $BenchmarkLog = "$LogDir\benchmark_$Timestamp.log"
        dotnet run -c Release --verbosity quiet 2>&1 | Tee-Object -FilePath $BenchmarkLog

        if ($LASTEXITCODE -eq 0) {
            $BenchmarkDuration = (Get-Date) - $BenchmarkStartTime
            Write-Log "✓ BenchmarkDotNet completed in $($BenchmarkDuration.ToString('hh\:mm\:ss'))" "Green"
            $TestsPassed++

            # Copy BenchmarkDotNet results (they're in Reports folder)
            $BdnResults = Join-Path $BenchmarkPath "Reports"
            if (Test-Path $BdnResults) {
                $LatestReport = Get-ChildItem -Path $BdnResults -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($LatestReport) {
                    Copy-Item -Path $LatestReport.FullName -Destination "$LogDir\BDN_$Timestamp" -Recurse -Force
                    Write-Log "✓ Results copied to: $LogDir\BDN_$Timestamp" "Green"
                }
            }
        } else {
            throw "BenchmarkDotNet exited with code $LASTEXITCODE"
        }
    } catch {
        Write-Log "✗ ERROR in BenchmarkDotNet: $_" "Red"
        $TestsFailed++
        Write-Log "Continuing to load tests anyway..." "Yellow"
    }
}

Write-Log ""
Set-Location $SolutionRoot

# ====================================================================
# PHASE 2: Load Testing (NBomber)
# ====================================================================

Write-Log "========================================" "Yellow"
Write-Log "PHASE 2: Running Load Tests (NBomber)" "Yellow"
Write-Log "========================================" "Yellow"

$LoadTestPath = Join-Path $SolutionRoot $LoadTestProject
if (-not (Test-Path $LoadTestPath)) {
    Write-Log "WARNING: Load test project not found at $LoadTestPath" "Yellow"
    Write-Log "Skipping load tests..." "Yellow"
    $TestsFailed++
} else {
    Set-Location $LoadTestPath
    Write-Log "Changed directory to: $LoadTestPath"

    $LoadTestStartTime = Get-Date
    Write-Log "Starting load tests at $(Get-Date -Format 'HH:mm:ss')..."
    Write-Log ""
    Write-Log "Select test suite (will auto-select in 10 seconds):" "Cyan"
    Write-Log "  1. Baseline Tests (CRUD)" "White"
    Write-Log "  2. User Journey Tests" "White"
    Write-Log "  3. Stress Tests" "White"
    Write-Log "  4. Production Mix" "White"
    Write-Log "  5. All Tests (NOT RECOMMENDED)" "White"
    Write-Log ""
    Write-Log "Auto-selecting option 1 (Baseline) in 10 seconds..." "Yellow"

    try {
        # Run load tests - pipe '1' to select baseline tests
        $LoadTestLog = "$LogDir\loadtest_$Timestamp.log"
        echo "1" | dotnet run -c Release 2>&1 | Tee-Object -FilePath $LoadTestLog

        if ($LASTEXITCODE -eq 0) {
            $LoadTestDuration = (Get-Date) - $LoadTestStartTime
            Write-Log "✓ Load tests completed in $($LoadTestDuration.ToString('hh\:mm\:ss'))" "Green"
            $TestsPassed++

            # Copy NBomber results (they're in Reports folder)
            $LoadTestResults = Join-Path $LoadTestPath "Reports"
            if (Test-Path $LoadTestResults) {
                $LatestReport = Get-ChildItem -Path $LoadTestResults -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($LatestReport) {
                    Copy-Item -Path $LatestReport.FullName -Destination "$LogDir\LoadTest_$Timestamp" -Recurse -Force
                    Write-Log "✓ Results copied to: $LogDir\LoadTest_$Timestamp" "Green"
                }
            }
        } else {
            throw "Load tests exited with code $LASTEXITCODE"
        }
    } catch {
        Write-Log "✗ ERROR in load tests: $_" "Red"
        $TestsFailed++
    }
}

Write-Log ""
Set-Location $SolutionRoot

# ====================================================================
# SUMMARY
# ====================================================================

$TotalDuration = (Get-Date) - $TotalStartTime

Write-Log "========================================" "Cyan"
Write-Log "Benchmark Suite Completed" "Cyan"
Write-Log "========================================" "Cyan"
Write-Log "Total Duration: $($TotalDuration.ToString('hh\:mm\:ss'))"
Write-Log "Tests Passed: $TestsPassed" $(if ($TestsPassed -gt 0) { "Green" } else { "Red" })
Write-Log "Tests Failed: $TestsFailed" $(if ($TestsFailed -eq 0) { "Green" } else { "Red" })
Write-Log "Results saved to: $LogDir"
Write-Log ""

# List generated files
Write-Log "Generated files:" "Cyan"
Get-ChildItem -Path $LogDir -Filter "*$Timestamp*" | ForEach-Object {
    Write-Log "  - $($_.Name)" "White"
}
Write-Log ""

# ====================================================================
# OPTIONAL SHUTDOWN
# ====================================================================

if ($Shutdown) {
    Write-Log "========================================" "Red"
    Write-Log "SHUTDOWN INITIATED" "Red"
    Write-Log "========================================" "Red"
    Write-Log "Computer will shut down in $ShutdownDelayMinutes minutes..." "Red"
    Write-Log "Press Ctrl+C to cancel" "Yellow"
    Write-Log ""

    # Countdown
    for ($i = $ShutdownDelayMinutes; $i -gt 0; $i--) {
        Write-Log "⏰ Shutting down in $i minute(s)..." "Yellow"
        Start-Sleep -Seconds 60
    }

    Write-Log "🔌 Shutting down NOW..." "Red"
    Stop-Computer -Force
} else {
    Write-Log "✓ Shutdown disabled. Computer will remain on." "Green"
    Write-Log "  To enable shutdown, run with -Shutdown flag" "Cyan"
}

Write-Log ""
Write-Log "Script completed at $(Get-Date -Format 'HH:mm:ss')"

# Exit with error code if any tests failed
exit $TestsFailed
