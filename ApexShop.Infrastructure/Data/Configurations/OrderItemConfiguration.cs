using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(oi => oi.Id);

        // Integer optimization with validation
        builder.Property(oi => oi.Quantity)
            .HasColumnType("smallint") // 2 bytes instead of 4
            .IsRequired();

        builder.HasCheckConstraint(
            "CK_OrderItems_Quantity_Positive",
            "\"Quantity\" > 0"
        );

        // Decimals with validation
        builder.Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasCheckConstraint(
            "CK_OrderItems_UnitPrice_NonNegative",
            "\"UnitPrice\" >= 0"
        );

        // Computed column - Avoid redundant storage
        builder.Property(oi => oi.TotalPrice)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)")
            .HasComputedColumnSql("\"Quantity\" * \"UnitPrice\"", stored: true) // Stored for indexing if needed
            .IsRequired();

        // Relationship with Order
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Product
        builder.HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optimized Indexes
        // 1. Order items lookup (covered by FK)
        builder.HasIndex(oi => oi.OrderId)
            .HasDatabaseName("IX_OrderItems_OrderId");

        // 2. Product sales analysis
        builder.HasIndex(oi => new { oi.ProductId, oi.Quantity })
            .HasDatabaseName("IX_OrderItems_Product_Quantity");
    }
}
