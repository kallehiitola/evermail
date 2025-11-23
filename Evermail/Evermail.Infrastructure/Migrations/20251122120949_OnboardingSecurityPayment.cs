using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingSecurityPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentAcknowledgedAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentAcknowledgedByUserId",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityPreference",
                table: "Tenants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "QuickStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentAcknowledgedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PaymentAcknowledgedByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityPreference",
                table: "Tenants");
        }
    }
}
