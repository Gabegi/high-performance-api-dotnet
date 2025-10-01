using ApexShop.Domain.Entities;

namespace ApexShop.Application.Interfaces;

public interface IReviewRepository : IRepository<Review>
{
    Task<IEnumerable<Review>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Review>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<double> GetAverageRatingByProductIdAsync(int productId, CancellationToken cancellationToken = default);
}
