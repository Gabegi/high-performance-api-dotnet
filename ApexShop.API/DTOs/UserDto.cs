namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed user view (GET by ID) - NEVER includes PasswordHash for security
/// </summary>
public record UserDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastLoginDate
);

/// <summary>
/// Lightweight DTO for user lists (GET all) - minimal fields for performance and privacy
/// </summary>
public record UserListDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive
);
