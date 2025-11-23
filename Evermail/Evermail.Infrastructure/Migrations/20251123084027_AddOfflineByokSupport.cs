using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineByokSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfflineBundleVersion",
                table: "TenantEncryptionSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflineKeyChecksum",
                table: "TenantEncryptionSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineKeyCreatedAt",
                table: "TenantEncryptionSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflineMasterKeyCiphertext",
                table: "TenantEncryptionSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflineTenantLabel",
                table: "TenantEncryptionSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfflineBundleVersion",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "OfflineKeyChecksum",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "OfflineKeyCreatedAt",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "OfflineMasterKeyCiphertext",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "OfflineTenantLabel",
                table: "TenantEncryptionSettings");
        }
    }
}
