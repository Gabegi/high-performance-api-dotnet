using ApexShop.Domain.Entities;

namespace ApexShop.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Category>> GetWithProductsAsync(CancellationToken cancellationToken = default);
}
