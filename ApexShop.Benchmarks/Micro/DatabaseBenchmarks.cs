using ApexShop.Infrastructure.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Benchmarks.Micro;

[MemoryDiagnoser]
[GcServer(true)]
public class DatabaseBenchmarks
{
    private AppDbContext _dbContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=apexshop;Username=postgres;Password=postgres")
            .Options;

        _dbContext = new AppDbContext(options);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [Benchmark]
    public async Task<int> GetProducts_WithTracking()
    {
        var products = await _dbContext.Products.ToListAsync();
        return products.Count;
    }

    [Benchmark]
    public async Task<int> GetProducts_NoTracking()
    {
        var products = await _dbContext.Products.AsNoTracking().ToListAsync();
        return products.Count;
    }

    [Benchmark]
    public async Task<int> GetProducts_WithIncludes_Tracking()
    {
        var products = await _dbContext.Products
            .Include(p => p.Category)
            .ToListAsync();
        return products.Count;
    }

    [Benchmark]
    public async Task<int> GetProducts_WithIncludes_NoTracking()
    {
        var products = await _dbContext.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .ToListAsync();
        return products.Count;
    }

    [Benchmark]
    public async Task<int> GetOrders_WithMultipleIncludes_Tracking()
    {
        var orders = await _dbContext.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .ToListAsync();
        return orders.Count;
    }

    [Benchmark]
    public async Task<int> GetOrders_WithMultipleIncludes_NoTracking()
    {
        var orders = await _dbContext.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .ToListAsync();
        return orders.Count;
    }

    [Benchmark]
    public async Task<int> GetSingleProduct_ById_Tracking()
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == 1);
        return product?.Id ?? 0;
    }

    [Benchmark]
    public async Task<int> GetSingleProduct_ById_NoTracking()
    {
        var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1);
        return product?.Id ?? 0;
    }
}
