using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        // Integer optimization - smallint for category Id (PostgreSQL doesn't have tinyint)
        builder.Property(c => c.Id)
            .HasColumnType("smallint")
            .IsRequired();

        // Strings
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnType("varchar(100)"); // Unicode support (PostgreSQL varchar is UTF-8)

        builder.Property(c => c.Description)
            .HasMaxLength(500)
            .HasColumnType("varchar(500)");

        // DateTime optimization
        builder.Property(c => c.CreatedDate)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // Boolean optimization
        builder.Property(c => c.IsActive)
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        // Optimized Indexes
        // 1. Category name search
        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Categories_Name");

        // 2. Active categories only (filtered index)
        builder.HasIndex(c => c.Name)
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_Categories_Name_ActiveOnly");
    }
}
