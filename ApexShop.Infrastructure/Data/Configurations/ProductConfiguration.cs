using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        // Strings
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("varchar(200)"); // Unicode support (PostgreSQL varchar is UTF-8)

        builder.Property(p => p.Description)
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)");

        // Decimals with validation
        builder.Property(p => p.Price)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasCheckConstraint(
            "CK_Products_Price_NonNegative",
            "\"Price\" >= 0"
        );

        // Integer optimizations with validation
        builder.Property(p => p.Stock)
            .HasColumnType("smallint") // 2 bytes instead of 4
            .IsRequired();

        builder.HasCheckConstraint(
            "CK_Products_Stock_NonNegative",
            "\"Stock\" >= 0"
        );

        builder.Property(p => p.CategoryId)
            .HasColumnType("smallint") // PostgreSQL doesn't have tinyint, use smallint (2 bytes)
            .IsRequired();

        // DateTime optimization - timestamp(3) for millisecond precision
        builder.Property(p => p.CreatedDate)
            .HasColumnType("timestamp(3)")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(p => p.UpdatedDate)
            .HasColumnType("timestamp(3)");

        // Boolean optimizations - boolean NOT NULL with defaults
        builder.Property(p => p.IsActive)
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.IsFeatured)
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        // Concurrency control - Prevent lost updates (PostgreSQL uses xmin system column)
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Relationship with Category
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optimized Indexes
        // 1. Category browsing with price sorting
        builder.HasIndex(p => new { p.CategoryId, p.Price })
            .HasDatabaseName("IX_Products_Category_Price");

        // 2. Product search by name
        builder.HasIndex(p => p.Name)
            .HasDatabaseName("IX_Products_Name");

        // 3. Active products only (filtered index)
        builder.HasIndex(p => new { p.CategoryId, p.Price })
            .HasFilter("\"IsActive\" = true")
            .HasDatabaseName("IX_Products_Category_Price_ActiveOnly");

        // 4. Featured products (filtered index)
        builder.HasIndex(p => new { p.IsFeatured, p.CreatedDate })
            .HasFilter("\"IsActive\" = true AND \"IsFeatured\" = true")
            .IsDescending(false, true)
            .HasDatabaseName("IX_Products_Featured_Recent");
    }
}
