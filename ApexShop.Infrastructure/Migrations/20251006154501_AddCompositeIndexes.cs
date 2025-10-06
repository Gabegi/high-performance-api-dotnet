using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProductId_Rating",
                table: "Reviews",
                columns: new[] { "ProductId", "Rating" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_Price",
                table: "Products",
                columns: new[] { "CategoryId", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_OrderDate",
                table: "Orders",
                columns: new[] { "UserId", "OrderDate" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProductId_Rating",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId_Price",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_OrderDate",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");
        }
    }
}
