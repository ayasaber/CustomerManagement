using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerManagement.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Story14RegistrationCentricCustomerProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationUserId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ApplicationUserId",
                table: "Customers",
                column: "ApplicationUserId",
                unique: true,
                filter: "[ApplicationUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_ApplicationUserId",
                table: "Customers",
                column: "ApplicationUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_ApplicationUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ApplicationUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Customers");
        }
    }
}
