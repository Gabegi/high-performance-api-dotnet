using ApexShop.Domain.Entities;
using ApexShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        // Enum stored as smallint (2 bytes vs 4 bytes for int or 50 bytes for varchar)
        // Note: PostgreSQL doesn't have tinyint, smallint is the smallest integer type
        builder.Property(o => o.Status)
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(OrderStatus.Pending)
            .IsRequired();

        // Strings
        builder.Property(o => o.ShippingAddress)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("varchar(500)"); // Unicode support (PostgreSQL varchar is UTF-8)

        builder.Property(o => o.TrackingNumber)
            .HasMaxLength(100)
            .HasColumnType("varchar(100)"); // ASCII is sufficient for tracking numbers

        // Decimals
        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // DateTime optimization - timestamp(3) for millisecond precision
        builder.Property(o => o.OrderDate)
            .HasColumnType("timestamp(3)")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(o => o.ShippedDate)
            .HasColumnType("timestamp(3)");
            // Note: IsSparse() would save ~4 bytes per NULL but requires EF Core 8+

        builder.Property(o => o.DeliveredDate)
            .HasColumnType("timestamp(3)");
            // Note: IsSparse() would save ~4 bytes per NULL but requires EF Core 8+

        // Concurrency control - Prevent lost updates (PostgreSQL uses xmin system column)
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Relationship with User
        // ❌ RESTRICT - Preserve order history (NEVER CASCADE)
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        // Reason: Legal compliance (GDPR allows anonymization), accounting records,
        //         analytics, customer service, dispute resolution
        // Action: Implement soft delete with user anonymization instead of hard delete

        // Indexes
        // Composite index for user order history (newest first)
        builder.HasIndex(o => new { o.UserId, o.OrderDate })
            .IsDescending(false, true)  // UserId ASC, OrderDate DESC
            .HasDatabaseName("IX_Orders_UserId_OrderDate");

        builder.HasIndex(o => o.OrderDate);

        // Composite index for status-based queries (covers Status alone + Status+OrderDate queries)
        builder.HasIndex(o => new { o.Status, o.OrderDate })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_Status_OrderDate");
    }
}
