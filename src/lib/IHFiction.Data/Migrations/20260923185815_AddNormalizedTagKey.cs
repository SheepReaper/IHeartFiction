using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedTagKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "normalized_key",
                schema: "ihfiction.dev2",
                table: "tags",
                type: "character varying(152)",
                maxLength: 152,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ihfiction.dev2"."tags"
                SET "normalized_key" = concat_ws(':',
                    regexp_replace(lower("category"), '[^[:alnum:]]', '', 'g'),
                    CASE WHEN "subcategory" IS NULL OR btrim("subcategory") = '' THEN NULL
                         ELSE regexp_replace(lower("subcategory"), '[^[:alnum:]]', '', 'g') END,
                    regexp_replace(lower("value"), '[^[:alnum:]]', '', 'g'));

                DO $$
                DECLARE duplicate record;
                BEGIN
                    FOR duplicate IN
                        SELECT duplicate_tag."id" AS duplicate_id, canonical."id" AS canonical_id
                        FROM "ihfiction.dev2"."tags" duplicate_tag
                        JOIN LATERAL (
                            SELECT candidate."id"
                            FROM "ihfiction.dev2"."tags" candidate
                            WHERE candidate."normalized_key" = duplicate_tag."normalized_key"
                              AND candidate."discriminator" = 'CanonicalTag'
                            ORDER BY candidate."id"
                            LIMIT 1
                        ) canonical ON TRUE
                        WHERE duplicate_tag."discriminator" = 'CanonicalTag'
                          AND duplicate_tag."id" <> canonical."id"
                    LOOP
                        INSERT INTO "ihfiction.dev2"."tag_work" ("tags_id", "works_id")
                        SELECT duplicate.canonical_id, links."works_id"
                        FROM "ihfiction.dev2"."tag_work" links
                        WHERE links."tags_id" = duplicate.duplicate_id
                        ON CONFLICT DO NOTHING;

                        DELETE FROM "ihfiction.dev2"."tag_work"
                        WHERE "tags_id" = duplicate.duplicate_id;

                        UPDATE "ihfiction.dev2"."tags"
                        SET "canonical_tag_id" = duplicate.canonical_id
                        WHERE "canonical_tag_id" = duplicate.duplicate_id;

                        UPDATE "ihfiction.dev2"."tags"
                        SET "discriminator" = 'SynonymTag',
                            "canonical_tag_id" = duplicate.canonical_id
                        WHERE "id" = duplicate.duplicate_id;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_key",
                schema: "ihfiction.dev2",
                table: "tags",
                type: "character varying(152)",
                maxLength: 152,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(152)",
                oldMaxLength: 152,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_normalized_key",
                schema: "ihfiction.dev2",
                table: "tags",
                column: "normalized_key",
                unique: true,
                filter: "\"discriminator\" = 'CanonicalTag'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_tags_normalized_key",
                schema: "ihfiction.dev2",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "normalized_key",
                schema: "ihfiction.dev2",
                table: "tags");
        }
    }
}
