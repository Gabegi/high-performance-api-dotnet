using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCategoryIdToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Change Categories.Id from smallint to integer
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Categories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            // Change Products.CategoryId from smallint to integer
            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Change Products.CategoryId back to smallint
            migrationBuilder.AlterColumn<short>(
                name: "CategoryId",
                table: "Products",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            // Rollback: Change Categories.Id back to smallint
            migrationBuilder.AlterColumn<short>(
                name: "Id",
                table: "Categories",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
