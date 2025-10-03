using ApexShop.API.Endpoints.Categories;
using ApexShop.API.Endpoints.Orders;
using ApexShop.API.Endpoints.Products;
using ApexShop.API.Endpoints.Reviews;
using ApexShop.API.Endpoints.Users;
using ApexShop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map endpoints
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapOrderEndpoints();
app.MapReviewEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in tests/benchmarks
namespace ApexShop.API
{
    public partial class Program { }
}
