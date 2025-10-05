using ApexShop.LoadTests.Load;
using NBomber.CSharp;
using NBomber.Contracts;

Console.Write("Press 1 to start load tests: ");
Console.ReadLine();

var crudScenarios = new CrudScenarios();
var realisticScenarios = new RealisticScenarios();
var stressScenarios = new StressScenarios();

// Get the project root directory (up from bin/Debug/net9.0)
var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                  ?? AppContext.BaseDirectory;
var reportsPath = Path.Combine(projectRoot, "Reports");

NBomberRunner
    .RegisterScenarios(
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
    )
    .WithReportFolder(reportsPath)
    .Run();
