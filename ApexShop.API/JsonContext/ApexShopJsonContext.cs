using System.Text.Json.Serialization;
using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Entities;

namespace ApexShop.API.JsonContext;

/// <summary>
/// JSON serializer context for ApexShop API using source generators.
/// This enables AOT (Ahead-Of-Time) compilation and improved performance through compile-time JSON serialization.
/// Generates TypeInfoResolver for System.Text.Json with compile-time optimizations.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
// DTOs
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(ProductListDto))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(List<ProductListDto>))]
[JsonSerializable(typeof(CategoryDto))]
[JsonSerializable(typeof(CategoryListDto))]
[JsonSerializable(typeof(List<CategoryDto>))]
[JsonSerializable(typeof(List<CategoryListDto>))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserListDto))]
[JsonSerializable(typeof(List<UserDto>))]
[JsonSerializable(typeof(List<UserListDto>))]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(OrderListDto))]
[JsonSerializable(typeof(List<OrderDto>))]
[JsonSerializable(typeof(List<OrderListDto>))]
[JsonSerializable(typeof(ReviewDto))]
[JsonSerializable(typeof(ReviewListDto))]
[JsonSerializable(typeof(List<ReviewDto>))]
[JsonSerializable(typeof(List<ReviewListDto>))]
// Entities
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
// Utility types
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(int))]
// Benchmark/API result types
[JsonSerializable(typeof(PaginatedResult<ProductListDto>))]
[JsonSerializable(typeof(PaginatedResult<CategoryListDto>))]
[JsonSerializable(typeof(PaginatedResult<UserListDto>))]
[JsonSerializable(typeof(PaginatedResult<OrderListDto>))]
[JsonSerializable(typeof(PaginatedResult<ReviewListDto>))]
[JsonSerializable(typeof(BulkCreateResult))]
[JsonSerializable(typeof(BulkCreateResultGeneric))]
public partial class ApexShopJsonContext : JsonSerializerContext
{
}
