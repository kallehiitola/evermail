using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG EmailSearchCatalog AS DEFAULT;
                END
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes fi
                    JOIN sys.objects o ON fi.object_id = o.object_id
                    WHERE o.name = 'EmailMessages'
                )
                BEGIN
                    CREATE FULLTEXT INDEX ON EmailMessages(
                        Subject LANGUAGE 1033,
                        TextBody LANGUAGE 1033,
                        FromName LANGUAGE 1033,
                        FromAddress LANGUAGE 1033
                    )
                    KEY INDEX PK_EmailMessages
                    ON EmailSearchCatalog
                    WITH CHANGE_TRACKING AUTO;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes fi
                    JOIN sys.objects o ON fi.object_id = o.object_id
                    WHERE o.name = 'EmailMessages'
                )
                BEGIN
                    DROP FULLTEXT INDEX ON EmailMessages;
                END
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                BEGIN
                    DROP FULLTEXT CATALOG EmailSearchCatalog;
                END
                """);
        }
    }
}
