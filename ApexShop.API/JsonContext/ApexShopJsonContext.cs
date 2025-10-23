using System.Text.Json.Serialization;
using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Entities;

namespace ApexShop.API.JsonContext;

/// <summary>
/// JSON serializer context for ApexShop API using source generators.
/// This enables AOT (Ahead-Of-Time) compilation and improved performance through compile-time JSON serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(ProductListDto))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(List<ProductListDto>))]
[JsonSerializable(typeof(IAsyncEnumerable<ProductListDto>))]
[JsonSerializable(typeof(CategoryDto))]
[JsonSerializable(typeof(CategoryListDto))]
[JsonSerializable(typeof(List<CategoryDto>))]
[JsonSerializable(typeof(List<CategoryListDto>))]
[JsonSerializable(typeof(IAsyncEnumerable<CategoryListDto>))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserListDto))]
[JsonSerializable(typeof(List<UserDto>))]
[JsonSerializable(typeof(List<UserListDto>))]
[JsonSerializable(typeof(IAsyncEnumerable<UserListDto>))]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(OrderListDto))]
[JsonSerializable(typeof(List<OrderDto>))]
[JsonSerializable(typeof(List<OrderListDto>))]
[JsonSerializable(typeof(IAsyncEnumerable<OrderListDto>))]
[JsonSerializable(typeof(ReviewDto))]
[JsonSerializable(typeof(ReviewListDto))]
[JsonSerializable(typeof(List<ReviewDto>))]
[JsonSerializable(typeof(List<ReviewListDto>))]
[JsonSerializable(typeof(IAsyncEnumerable<ReviewListDto>))]
[JsonSerializable(typeof(Product))]
[JsonSerializable(typeof(List<Product>))]
[JsonSerializable(typeof(Category))]
[JsonSerializable(typeof(List<Category>))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
[JsonSerializable(typeof(Review))]
[JsonSerializable(typeof(List<Review>))]
[JsonSerializable(typeof(OrderItem))]
[JsonSerializable(typeof(List<OrderItem>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(int))]
internal partial class ApexShopJsonContext : JsonSerializerContext
{
}
