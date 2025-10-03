using ApexShop.LoadTests.Load;
using NBomber.CSharp;

Console.Write("Press 1 to start load tests: ");
Console.ReadLine();

var crudScenarios = new CrudScenarios();
var realisticScenarios = new RealisticScenarios();
var stressScenarios = new StressScenarios();

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
    .Run();
