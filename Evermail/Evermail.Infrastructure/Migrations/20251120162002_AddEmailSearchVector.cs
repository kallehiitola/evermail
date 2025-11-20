using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchVector",
                table: "EmailMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE EmailMessages
                SET SearchVector = LTRIM(RTRIM(CONCAT_WS(' ',
                    NULLIF(Subject, ''),
                    NULLIF(FromName, ''),
                    NULLIF(FromAddress, ''),
                    NULLIF(RecipientsSearch, ''),
                    NULLIF(TextBody, ''),
                    NULLIF(HtmlBody, '')
                )));
                """);

            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes fi
                        JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'EmailMessages'
                    )
                    BEGIN
                        DROP FULLTEXT INDEX ON EmailMessages;
                    END;

                    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                    BEGIN
                        DROP FULLTEXT CATALOG EmailSearchCatalog;
                    END;

                    CREATE FULLTEXT CATALOG EmailSearchCatalog AS DEFAULT;

                    CREATE FULLTEXT INDEX ON EmailMessages(
                        SearchVector LANGUAGE 1033
                    )
                    KEY INDEX PK_EmailMessages
                    ON EmailSearchCatalog
                    WITH CHANGE_TRACKING AUTO;
                END;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes fi
                        JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'EmailMessages'
                    )
                    BEGIN
                        DROP FULLTEXT INDEX ON EmailMessages;
                    END;

                    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                    BEGIN
                        DROP FULLTEXT CATALOG EmailSearchCatalog;
                    END;

                    CREATE FULLTEXT CATALOG EmailSearchCatalog AS DEFAULT;

                    CREATE FULLTEXT INDEX ON EmailMessages(
                        Subject LANGUAGE 1033,
                        TextBody LANGUAGE 1033,
                        HtmlBody LANGUAGE 1033,
                        RecipientsSearch LANGUAGE 1033,
                        FromName LANGUAGE 1033,
                        FromAddress LANGUAGE 1033
                    )
                    KEY INDEX PK_EmailMessages
                    ON EmailSearchCatalog
                    WITH CHANGE_TRACKING AUTO;
                END;
                """,
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "EmailMessages");
        }
    }
}
