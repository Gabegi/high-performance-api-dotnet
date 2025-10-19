using ApexShop.LoadTests.Configuration;
using ApexShop.LoadTests.Load;
using NBomber.CSharp;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("        ApexShop API Load Testing Suite");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine($"API Base URL: {LoadTestConfig.BaseUrl}");
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