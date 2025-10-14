using ApexShop.LoadTests.Configuration;
using ApexShop.LoadTests.Load;
using NBomber.CSharp;
using NBomber.Contracts;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("        ApexShop API Load Testing Suite");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine($"API Base URL: {LoadTestConfig.BaseUrl}");
Console.WriteLine();
Console.WriteLine("Select test suite to run:");
Console.WriteLine();
Console.WriteLine("  1. Baseline Tests (CRUD - establish normal performance)");
Console.WriteLine("  2. User Journey Tests (realistic multi-step workflows)");
Console.WriteLine("  3. Stress Tests (individual stress scenarios)");
Console.WriteLine("  4. Production Mix (weighted traffic distribution)");
Console.WriteLine("  5. All Tests (NOT RECOMMENDED - use for demo only)");
Console.WriteLine();
Console.Write("Enter your choice (1-5): ");

var choice = Console.ReadLine();

var crudScenarios = new CrudScenarios();
var realisticScenarios = new RealisticScenarios();
var stressScenarios = new StressScenarios();

// Get the project root directory (up from bin/Debug/net9.0)
var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                  ?? AppContext.BaseDirectory;
var reportsPath = Path.Combine(projectRoot, "Reports");

ScenarioProps[] scenarios = choice switch
{
    "1" => [
        crudScenarios.GetProducts(),
        crudScenarios.GetProductById(),
        crudScenarios.CreateProduct(),
        crudScenarios.GetCategories(),
        crudScenarios.GetOrders()
    ],
    "2" => [
        realisticScenarios.BrowseAndAddReview(),
        realisticScenarios.CreateOrderWorkflow(),
        realisticScenarios.UserRegistrationAndBrowse()
    ],
    "3" => [
        stressScenarios.HighLoadGetProducts(),
        stressScenarios.SpikeTest(),
        stressScenarios.ConstantLoad(),
        stressScenarios.MixedOperationsStress()
    ],
    "4" => [
        // Simulates production traffic distribution
        crudScenarios.GetProducts(),      // High frequency
        crudScenarios.GetProductById(),   // High frequency
        crudScenarios.GetCategories(),    // Medium frequency
        realisticScenarios.CreateOrderWorkflow(), // Low frequency
        crudScenarios.CreateProduct()     // Low frequency
    ],
    "5" => [
        crudScenarios.GetProducts(),
        crudScenarios.GetProductById(),
        crudScenarios.CreateProduct(),
        crudScenarios.GetCategories(),
        crudScenarios.GetOrders(),
        realisticScenarios.BrowseAndAddReview(),
        realisticScenarios.CreateOrderWorkflow(),
        realisticScenarios.UserRegistrationAndBrowse(),
        stressScenarios.HighLoadGetProducts(),
        stressScenarios.SpikeTest(),
        stressScenarios.ConstantLoad(),
        stressScenarios.MixedOperationsStress()
    ],
    _ => throw new InvalidOperationException("Invalid choice. Please select 1-5.")
};

Console.WriteLine();
Console.WriteLine($"Starting test suite with {scenarios.Length} scenario(s)...");
Console.WriteLine("Press any key to begin...");
Console.ReadKey();
Console.WriteLine();

NBomberRunner
    .RegisterScenarios(scenarios)
    .WithReportFolder(reportsPath)
    .Run();
