using ApexShop.Domain.Entities;

namespace ApexShop.Application.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Order>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<Order?> GetWithItemsAsync(int orderId, CancellationToken cancellationToken = default);
}
