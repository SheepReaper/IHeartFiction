using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillBookOwnerAuthorLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                """
                INSERT INTO "ihfiction.dev2"."author_work" (authors_id, works_id)
                SELECT w.owner_id, w.id
                FROM "ihfiction.dev2"."work" w
                WHERE w.discriminator = 'Book'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ihfiction.dev2"."author_work" aw
                      WHERE aw.authors_id = w.owner_id
                        AND aw.works_id = w.id
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Intentionally no-op: this migration backfills missing links for existing data.
        }
    }
}
