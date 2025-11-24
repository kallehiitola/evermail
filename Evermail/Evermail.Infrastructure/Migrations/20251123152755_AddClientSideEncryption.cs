using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSideEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionMetadataJson",
                table: "MailboxUploads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionScheme",
                table: "MailboxUploads",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClientEncrypted",
                table: "MailboxUploads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionMetadataJson",
                table: "Mailboxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionScheme",
                table: "Mailboxes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClientEncrypted",
                table: "Mailboxes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionMetadataJson",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "EncryptionScheme",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "IsClientEncrypted",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "EncryptionMetadataJson",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "EncryptionScheme",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "IsClientEncrypted",
                table: "Mailboxes");
        }
    }
}
