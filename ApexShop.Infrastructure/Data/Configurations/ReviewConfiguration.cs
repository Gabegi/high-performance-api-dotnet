using ApexShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexShop.Infrastructure.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        // Rating with validation constraint
        builder.Property(r => r.Rating)
            .HasColumnType("smallint") // PostgreSQL doesn't have tinyint, use smallint (2 bytes)
            .IsRequired();

        builder.HasCheckConstraint(
            "CK_Reviews_Rating_Range",
            "\"Rating\" >= 1 AND \"Rating\" <= 5"
        );

        // Comment with validation
        builder.Property(r => r.Comment)
            .HasMaxLength(2000)
            .HasColumnType("varchar(2000)"); // Unicode support (PostgreSQL varchar is UTF-8)

        builder.HasCheckConstraint(
            "CK_Reviews_Comment_NotEmpty",
            "\"Comment\" IS NULL OR LENGTH(TRIM(\"Comment\")) > 0"
        );

        // DateTime optimization
        builder.Property(r => r.CreatedDate)
            .HasColumnType("timestamp(3)")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Boolean optimization
        builder.Property(r => r.IsVerifiedPurchase)
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        // Relationship with Product
        // ✅ CASCADE - Reviews belong to product (consider changing to RESTRICT)
        builder.HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        // Reason: Reviews lose meaning without product context
        // Warning: Deletes all reviews when product deleted - may lose valuable UGC
        // Alternative: Use RESTRICT and implement product soft delete (IsActive=false)

        // Relationship with User
        // ❌ RESTRICT - Preserve review integrity (NEVER CASCADE)
        builder.HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        // Reason: Product ratings must remain accurate, prevents rating manipulation,
        //         preserves social proof, maintains review authenticity
        // Action: Anonymize reviewer info when user deleted

        // Optimized Indexes
        // 1. User's review history
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_Reviews_UserId");

        // 2. Product reviews by rating (top reviews)
        builder.HasIndex(r => new { r.ProductId, r.Rating })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Reviews_Product_Rating");

        // 3. Recent product reviews
        builder.HasIndex(r => new { r.ProductId, r.CreatedDate })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Reviews_Product_Recent");

        // 4. Verified reviews only (filtered index)
        builder.HasIndex(r => new { r.ProductId, r.Rating })
            .HasFilter("\"IsVerifiedPurchase\" = true")
            .IsDescending(false, true)
            .HasDatabaseName("IX_Reviews_Product_Rating_Verified");
    }
}
