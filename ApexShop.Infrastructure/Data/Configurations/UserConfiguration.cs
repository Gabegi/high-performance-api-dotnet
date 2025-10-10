using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        // Strings
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("varchar(255)");
            // Note: PostgreSQL is case-sensitive by default. Use ILIKE or LOWER() for case-insensitive searches

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnType("varchar(100)"); // Unicode support (PostgreSQL varchar is UTF-8)

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnType("varchar(100)"); // Unicode support (PostgreSQL varchar is UTF-8)

        // Ensure application uses BCrypt/Argon2 - increased length for future algorithms
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("varchar(500)");

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(30) // Increased for international formats with extensions
            .HasColumnType("varchar(30)");

        // DateTime optimization
        builder.Property(u => u.CreatedDate)
            .HasColumnType("timestamp(3)")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(u => u.LastLoginDate)
            .HasColumnType("timestamp(3)");

        // Boolean optimization
        builder.Property(u => u.IsActive)
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        // Concurrency control - Prevent lost updates (PostgreSQL uses xmin system column)
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email_Unique");

        // Filtered index - Only index inactive users (more efficient than full boolean index)
        builder.HasIndex(u => u.IsActive)
            .HasFilter("\"IsActive\" = false")
            .HasDatabaseName("IX_Users_Inactive");
    }
}
