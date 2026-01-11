using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityLevelToTenantMailbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityLevel",
                table: "Tenants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FullService");

            migrationBuilder.AddColumn<string>(
                name: "SecurityLevel",
                table: "MailboxUploads",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FullService");

            migrationBuilder.AddColumn<string>(
                name: "SecurityLevel",
                table: "Mailboxes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FullService");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityLevel",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecurityLevel",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "SecurityLevel",
                table: "Mailboxes");
        }
    }
}
