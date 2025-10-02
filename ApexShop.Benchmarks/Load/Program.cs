using NBomber.CSharp;

namespace ApexShop.Benchmarks.Load;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("NBomber Load Testing for ApexShop API");
        Console.WriteLine("======================================");
        Console.WriteLine();
        Console.WriteLine("Press 1 to run all load tests...");
        Console.ReadLine();

        Console.WriteLine("Running all scenarios...");
        Console.WriteLine("This will take several minutes.");
        Console.WriteLine();

        // Run CRUD scenarios
        Console.WriteLine("Running CRUD Scenarios...");
        NBomberRunner
            .RegisterScenarios(
                CrudScenarios.GetProducts(),
                CrudScenarios.GetProductById(),
                CrudScenarios.CreateProduct(),
                CrudScenarios.GetCategories(),
                CrudScenarios.GetOrders()
            )
            .WithReportFolder("reports/crud")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
            .Run();

        // Run Realistic scenarios
        Console.WriteLine("\nRunning Realistic Scenarios...");
        NBomberRunner
            .RegisterScenarios(
                RealisticScenarios.BrowseAndAddReview(),
                RealisticScenarios.CreateOrderWorkflow(),
                RealisticScenarios.UserRegistrationAndBrowse()
            )
            .WithReportFolder("reports/realistic")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
            .Run();

        // Run Stress scenarios
        Console.WriteLine("\nRunning Stress Scenarios...");
        NBomberRunner
            .RegisterScenarios(
                StressScenarios.HighLoadGetProducts(),
                StressScenarios.SpikeTest(),
                StressScenarios.ConstantLoad(),
                StressScenarios.MixedOperationsStress()
            )
            .WithReportFolder("reports/stress")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
            .Run();

        Console.WriteLine("\nAll tests completed! Check the reports folder for results.");
    }
}
