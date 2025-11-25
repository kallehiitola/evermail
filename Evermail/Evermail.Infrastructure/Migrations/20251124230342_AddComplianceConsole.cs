using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceConsole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "UserDeletionJobs",
                type: "uniqueidentifier",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "UserDataExports",
                type: "uniqueidentifier",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "UserDataExports",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
UPDATE UserDataExports SET RequestedByUserId = UserId
WHERE RequestedByUserId = '00000000-0000-0000-0000-000000000000';
""");

            migrationBuilder.Sql("""
UPDATE UserDeletionJobs SET RequestedByUserId = UserId
WHERE RequestedByUserId = '00000000-0000-0000-0000-000000000000';
""");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionJobs_TenantId_RequestedByUserId",
                table: "UserDeletionJobs",
                columns: new[] { "TenantId", "RequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDataExports_TenantId_RequestedByUserId",
                table: "UserDataExports",
                columns: new[] { "TenantId", "RequestedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDeletionJobs_TenantId_RequestedByUserId",
                table: "UserDeletionJobs");

            migrationBuilder.DropIndex(
                name: "IX_UserDataExports_TenantId_RequestedByUserId",
                table: "UserDataExports");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "UserDeletionJobs");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "UserDataExports");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "UserDataExports");
        }
    }
}
