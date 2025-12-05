using ApexShop.LoadTests.Configuration;
using ApexShop.LoadTests.Load;
using NBomber.CSharp;
using System.Diagnostics;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("        ApexShop API Load Testing Suite");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine($"API Base URL: {LoadTestConfig.BaseUrl}");
Console.WriteLine();

// ====================================================================
// SETUP: Process Management
// ====================================================================
Process? apiProcess = null;

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (sender, e) =>
{
    Console.WriteLine();
    Console.WriteLine("⚠ Interrupted! Cleaning up...");
    CleanupApiProcess();
    e.Cancel = false;
};

// ====================================================================
// STEP 0: Start API (Skip database seeding)
// ====================================================================
Console.WriteLine("────────────────────────────────────────────────────────────");
Console.WriteLine("STEP 0: Starting API...");
Console.WriteLine("────────────────────────────────────────────────────────────");

try
{
    var apiProjectPath = Path.Combine("..", "ApexShop.API");

    // Start API without seeding
    Console.WriteLine("→ Starting API (using existing database)...");
    apiProcess = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project \"{apiProjectPath}\" -c Release",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    if (apiProcess == null)
    {
        Console.WriteLine("✗ Failed to start API process!");
        return;
    }

    // Optional: Stream API output (uncomment to see logs)
    _ = Task.Run(async () =>
    {
        if (apiProcess.StandardOutput != null)
        {
            string? line;
            while ((line = await apiProcess.StandardOutput.ReadLineAsync()) != null)
            {
                // Uncomment to see API logs during startup:
                // Console.WriteLine($"[API] {line}");
            }
        }
    });

    _ = Task.Run(async () =>
    {
        if (apiProcess.StandardError != null)
        {
            string? line;
            while ((line = await apiProcess.StandardError.ReadLineAsync()) != null)
            {
                // Log errors from API
                // Console.WriteLine($"[API ERROR] {line}");
            }
        }
    });

    // Wait for API to be ready
    Console.WriteLine("→ Waiting for API to be ready (max 2 minutes)...");
    var apiReady = await WaitForApiReadyAsync(LoadTestConfig.BaseUrl, maxWaitSeconds: 120);

    if (!apiReady)
    {
        Console.WriteLine("✗ API did not start in time!");
        CleanupApiProcess();
        return;
    }

    Console.WriteLine("✓ API is ready and database seeded successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Setup failed: {ex.Message}");
    CleanupApiProcess();
    return;
}

Console.WriteLine("────────────────────────────────────────────────────────────");
Console.WriteLine();
Console.WriteLine("Running ALL tests sequentially...");
Console.WriteLine();

var crudScenarios = new CrudScenarios();
var stressScenarios = new StressScenarios();

// Get the project root directory
var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                  ?? AppContext.BaseDirectory;
var reportsPath = Path.Combine(projectRoot, "Reports");

// Define all scenarios (Products only)
var allScenarios = new[]
{
    ("CRUD: Get Products", crudScenarios.GetProducts()),
    ("CRUD: Get Product By ID", crudScenarios.GetProductById()),
    ("CRUD: Create Product", crudScenarios.CreateProduct()),
    ("Stress: High Load Get Products", stressScenarios.HighLoadGetProducts()),
    ("Stress: Spike Test", stressScenarios.SpikeTest()),
    ("Stress: Constant Load", stressScenarios.ConstantLoad())
};

Console.WriteLine($"Total tests to run: {allScenarios.Length}");
Console.WriteLine();
Console.WriteLine("════════════════════════════════════════════════════════════");
Console.WriteLine("Starting load tests...");
Console.WriteLine("════════════════════════════════════════════════════════════");
Console.WriteLine();

// Run each scenario sequentially
for (int i = 0; i < allScenarios.Length; i++)
{
    var (name, scenario) = allScenarios[i];

    Console.WriteLine($"[{i + 1}/{allScenarios.Length}] Running: {name}");
    Console.WriteLine(new string('─', 60));

    NBomberRunner
        .RegisterScenarios(scenario)
        .WithReportFolder(reportsPath)
        .Run();

    Console.WriteLine();
    Console.WriteLine($"✓ Completed: {name}");
    Console.WriteLine();

    // Pause between tests to let server recover
    if (i < allScenarios.Length - 1)
    {
        Console.WriteLine("Pausing 5 seconds before next test...");
        Thread.Sleep(5000);
        Console.WriteLine();
    }
}

Console.WriteLine("════════════════════════════════════════════════════════════");
Console.WriteLine("ALL TESTS COMPLETED!");
Console.WriteLine($"Reports saved to: {reportsPath}");
Console.WriteLine("════════════════════════════════════════════════════════════");

// ====================================================================
// CLEANUP
// ====================================================================
Console.WriteLine();
Console.WriteLine("Cleaning up...");
CleanupApiProcess();
Console.WriteLine("✓ Cleanup complete");

// ====================================================================
// Helper Methods
// ====================================================================

/// <summary>
/// Stops the API process if it's running.
/// </summary>
void CleanupApiProcess()
{
    try
    {
        if (apiProcess != null && !apiProcess.HasExited)
        {
            Console.WriteLine("→ Stopping API process...");
            apiProcess.Kill(entireProcessTree: true);
            apiProcess.WaitForExit(5000); // Wait up to 5 seconds
            Console.WriteLine("✓ API process stopped");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Failed to stop API process: {ex.Message}");
        Console.WriteLine("  You may need to manually stop the API process:");
        Console.WriteLine($"  Kill process: {apiProcess?.Id}");
    }
}

/// <summary>
/// Waits for the API to be ready by polling the /products endpoint.
/// Returns true if API responds successfully within the timeout.
/// </summary>
static async Task<bool> WaitForApiReadyAsync(string baseUrl, int maxWaitSeconds)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var endTime = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
    var checkIntervalMs = 2000;

    while (DateTime.UtcNow < endTime)
    {
        try
        {
            // Use lightweight endpoint to check if API is ready
            var response = await client.GetAsync($"{baseUrl}/products?page=1&pageSize=1");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine();
                Console.WriteLine("✓ API health check passed");
                return true;
            }
        }
        catch
        {
            // API not ready yet, continue polling
        }

        var timeRemaining = (endTime - DateTime.UtcNow).TotalSeconds;
        if (timeRemaining > checkIntervalMs / 1000.0)
        {
            Console.Write(".");
            await Task.Delay(checkIntervalMs);
        }
        else
        {
            break;
        }
    }

    Console.WriteLine();
    return false;
}
