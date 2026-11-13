using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.OrderService.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentIdempotencyKey",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentIdempotencyKey",
                table: "Orders");
        }
    }
}
