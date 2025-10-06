using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.ShippingAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.TrackingNumber)
            .HasMaxLength(100);

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.OrderDate)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationship with User
        // Restrict: Preserve order history for accounting, analytics, and compliance
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        // Composite index for user order history (newest first)
        builder.HasIndex(o => new { o.UserId, o.OrderDate })
            .IsDescending(false, true)  // UserId ASC, OrderDate DESC
            .HasDatabaseName("IX_Orders_UserId_OrderDate");
        builder.HasIndex(o => o.OrderDate);
        builder.HasIndex(o => o.Status);
    }
}
