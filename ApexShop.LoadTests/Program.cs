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
// STEP 0: Reseed Database for Clean Test Data
// ====================================================================
Console.WriteLine("────────────────────────────────────────────────────────────");
Console.WriteLine("STEP 0: Reseeding database for clean test data...");
Console.WriteLine("────────────────────────────────────────────────────────────");

try
{
    // Drop database
    Console.WriteLine("→ Dropping existing database...");
    var dropProcess = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = "ef database drop --startup-project ../ApexShop.API --force --project ../ApexShop.Infrastructure",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    if (dropProcess != null)
    {
        await dropProcess.WaitForExitAsync();
        if (dropProcess.ExitCode == 0)
        {
            Console.WriteLine("✓ Database dropped successfully");
        }
        else
        {
            Console.WriteLine("⚠ Database drop had issues (may not have existed)");
        }
    }

    // Recreate and migrate
    Console.WriteLine("→ Recreating database with migrations...");
    var migrateProcess = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = "ef database update --startup-project ../ApexShop.API --project ../ApexShop.Infrastructure",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    if (migrateProcess != null)
    {
        await migrateProcess.WaitForExitAsync();
        if (migrateProcess.ExitCode != 0)
        {
            Console.WriteLine("✗ Migration failed!");
            Console.WriteLine("Please ensure PostgreSQL is running and accessible.");
            return;
        }
    }

    Console.WriteLine("✓ Database recreated successfully");

    // Seed database via API call
    Console.WriteLine("→ Seeding database with test data...");
    Console.WriteLine("  Note: API must be running for seeding to work!");
    Console.WriteLine("  Start API with: RUN_SEEDING=true dotnet run -c Release");
    Console.WriteLine();
    Console.WriteLine("⚠ MANUAL STEP REQUIRED:");
    Console.WriteLine("  1. Start API in another terminal: cd ApexShop.API && RUN_SEEDING=true dotnet run -c Release");
    Console.WriteLine("  2. Wait for seeding to complete");
    Console.WriteLine("  3. Press any key here to continue with load tests...");
    Console.ReadKey();
    Console.WriteLine();
    Console.WriteLine("✓ Proceeding with assumption that database is seeded");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Database reseed failed: {ex.Message}");
    Console.WriteLine("Continuing anyway...");
}

Console.WriteLine("────────────────────────────────────────────────────────────");
Console.WriteLine();
Console.WriteLine("Running ALL tests sequentially...");
Console.WriteLine();

var crudScenarios = new CrudScenarios();
var realisticScenarios = new RealisticScenarios();
var stressScenarios = new StressScenarios();

// Get the project root directory (up from bin/Debug/net9.0)
var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                  ?? AppContext.BaseDirectory;
var reportsPath = Path.Combine(projectRoot, "Reports");

// Define all scenarios in order
var allScenarios = new[]
{
    ("CRUD: Get Products", crudScenarios.GetProducts()),
    ("CRUD: Get Product By ID", crudScenarios.GetProductById()),
    ("CRUD: Create Product", crudScenarios.CreateProduct()),
    ("CRUD: Get Categories", crudScenarios.GetCategories()),
    ("CRUD: Get Orders", crudScenarios.GetOrders()),
    ("User Journey: Browse and Add Review", realisticScenarios.BrowseAndAddReview()),
    ("User Journey: Create Order Workflow", realisticScenarios.CreateOrderWorkflow()),
    ("User Journey: User Registration and Browse", realisticScenarios.UserRegistrationAndBrowse()),
    ("Stress: High Load Get Products", stressScenarios.HighLoadGetProducts()),
    ("Stress: Spike Test", stressScenarios.SpikeTest()),
    ("Stress: Constant Load", stressScenarios.ConstantLoad()),
    ("Stress: Mixed Operations", stressScenarios.MixedOperationsStress())
};

Console.WriteLine($"Total tests to run: {allScenarios.Length}");
Console.WriteLine("Press any key to begin...");
Console.ReadKey();
Console.WriteLine();
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

    // Short pause between tests to let the server recover
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