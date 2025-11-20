using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionProviderMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastUnwrapRequestId",
                table: "MailboxEncryptionStates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "MailboxEncryptionStates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AzureKeyVault");

            migrationBuilder.AddColumn<string>(
                name: "ProviderKeyVersion",
                table: "MailboxEncryptionStates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMetadata",
                table: "MailboxEncryptionStates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WrapRequestId",
                table: "MailboxEncryptionStates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUnwrapRequestId",
                table: "MailboxEncryptionStates");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "MailboxEncryptionStates");

            migrationBuilder.DropColumn(
                name: "ProviderKeyVersion",
                table: "MailboxEncryptionStates");

            migrationBuilder.DropColumn(
                name: "ProviderMetadata",
                table: "MailboxEncryptionStates");

            migrationBuilder.DropColumn(
                name: "WrapRequestId",
                table: "MailboxEncryptionStates");
        }
    }
}
