using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxSourceFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFormat",
                table: "MailboxUploads",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "mbox");

            migrationBuilder.AddColumn<string>(
                name: "SourceFormat",
                table: "Mailboxes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "mbox");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFormat",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "SourceFormat",
                table: "Mailboxes");
        }
    }
}
