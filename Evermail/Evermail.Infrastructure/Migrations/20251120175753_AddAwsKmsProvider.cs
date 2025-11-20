using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAwsKmsProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwsAccountId",
                table: "TenantEncryptionSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwsExternalId",
                table: "TenantEncryptionSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwsIamRoleArn",
                table: "TenantEncryptionSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwsKmsKeyArn",
                table: "TenantEncryptionSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwsRegion",
                table: "TenantEncryptionSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "TenantEncryptionSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AzureKeyVault");

            migrationBuilder.AddColumn<string>(
                name: "ProviderMetadata",
                table: "TenantEncryptionSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("UPDATE TenantEncryptionSettings SET Provider = 'AzureKeyVault' WHERE Provider IS NULL OR Provider = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwsAccountId",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "AwsExternalId",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "AwsIamRoleArn",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "AwsKmsKeyArn",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "AwsRegion",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "TenantEncryptionSettings");

            migrationBuilder.DropColumn(
                name: "ProviderMetadata",
                table: "TenantEncryptionSettings");
        }
    }
}
