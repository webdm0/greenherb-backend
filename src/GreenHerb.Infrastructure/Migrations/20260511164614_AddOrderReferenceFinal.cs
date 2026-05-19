using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenHerb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReferenceFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderReference",
                table: "Orders",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderReference",
                table: "Orders",
                column: "OrderReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderReference",
                table: "Orders");
        }
    }
}
