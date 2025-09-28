using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations;

/// <inheritdoc />
internal sealed partial class RenameWorks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Drop all FKs referencing 'work'
        migrationBuilder.DropForeignKey(
            name: "fk_anthology_story_work_anthologies_id",
            schema: "ihfiction.dev2",
            table: "anthology_story");

        migrationBuilder.DropForeignKey(
            name: "fk_author_work_work_works_id",
            schema: "ihfiction.dev2",
            table: "author_work");

        migrationBuilder.DropForeignKey(
            name: "fk_tag_work_work_works_id",
            schema: "ihfiction.dev2",
            table: "tag_work");

        migrationBuilder.DropForeignKey(
            name: "fk_work_authors_owner_id",
            schema: "ihfiction.dev2",
            table: "work");

        migrationBuilder.DropForeignKey(
            name: "fk_work_work_book_id",
            schema: "ihfiction.dev2",
            table: "work");

        migrationBuilder.DropForeignKey(
            name: "fk_work_work_story_id",
            schema: "ihfiction.dev2",
            table: "work");


        // 2. Rename table
        migrationBuilder.RenameTable(
            name: "work",
            schema: "ihfiction.dev2",
            newName: "works",
            newSchema: "ihfiction.dev2");

        // 3. Rename PK constraint (PostgreSQL)
        migrationBuilder.Sql("ALTER TABLE \"ihfiction.dev2\".works RENAME CONSTRAINT pk_work TO pk_works;");

        // 4. Rename indexes
        migrationBuilder.RenameIndex(
            name: "ix_work_title",
            schema: "ihfiction.dev2",
            table: "works",
            newName: "ix_works_title");

        migrationBuilder.RenameIndex(
            name: "ix_work_story_id",
            schema: "ihfiction.dev2",
            table: "works",
            newName: "ix_works_story_id");

        migrationBuilder.RenameIndex(
            name: "ix_work_owner_id",
            schema: "ihfiction.dev2",
            table: "works",
            newName: "ix_works_owner_id");

        migrationBuilder.RenameIndex(
            name: "ix_work_book_id",
            schema: "ihfiction.dev2",
            table: "works",
            newName: "ix_works_book_id");

        // 5. Re-add FKs referencing 'works'
        migrationBuilder.AddForeignKey(
            name: "fk_anthology_story_anthologies_anthologies_id",
            schema: "ihfiction.dev2",
            table: "anthology_story",
            column: "anthologies_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "works",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_author_work_works_works_id",
            schema: "ihfiction.dev2",
            table: "author_work",
            column: "works_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "works",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_tag_work_works_works_id",
            schema: "ihfiction.dev2",
            table: "tag_work",
            column: "works_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "works",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_works_authors_owner_id",
            schema: "ihfiction.dev2",
            table: "works",
            column: "owner_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_works_works_book_id",
            schema: "ihfiction.dev2",
            table: "works",
            column: "book_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "works",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "fk_works_works_story_id",
            schema: "ihfiction.dev2",
            table: "works",
            column: "story_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "works",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_anthology_story_anthologies_anthologies_id",
            schema: "ihfiction.dev2",
            table: "anthology_story");

        migrationBuilder.DropForeignKey(
            name: "fk_author_work_works_works_id",
            schema: "ihfiction.dev2",
            table: "author_work");

        migrationBuilder.DropForeignKey(
            name: "fk_tag_work_works_works_id",
            schema: "ihfiction.dev2",
            table: "tag_work");

        migrationBuilder.DropForeignKey(
            name: "fk_works_authors_owner_id",
            schema: "ihfiction.dev2",
            table: "works");

        migrationBuilder.DropForeignKey(
            name: "fk_works_works_book_id",
            schema: "ihfiction.dev2",
            table: "works");

        migrationBuilder.DropForeignKey(
            name: "fk_works_works_story_id",
            schema: "ihfiction.dev2",
            table: "works");

        migrationBuilder.RenameTable(
            name: "works",
            schema: "ihfiction.dev2",
            newName: "work",
            newSchema: "ihfiction.dev2");

        // 3. Rename PK constraint (PostgreSQL)
        migrationBuilder.Sql("ALTER TABLE \"ihfiction.dev2\".works RENAME CONSTRAINT pk_works TO pk_work;");

        migrationBuilder.RenameIndex(
            name: "ix_works_title",
            schema: "ihfiction.dev2",
            table: "work",
            newName: "ix_work_title");

        migrationBuilder.RenameIndex(
            name: "ix_works_story_id",
            schema: "ihfiction.dev2",
            table: "work",
            newName: "ix_work_story_id");

        migrationBuilder.RenameIndex(
            name: "ix_works_owner_id",
            schema: "ihfiction.dev2",
            table: "work",
            newName: "ix_work_owner_id");

        migrationBuilder.RenameIndex(
            name: "ix_works_book_id",
            schema: "ihfiction.dev2",
            table: "work",
            newName: "ix_work_book_id");

        migrationBuilder.AddForeignKey(
            name: "fk_anthology_story_work_anthologies_id",
            schema: "ihfiction.dev2",
            table: "anthology_story",
            column: "anthologies_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "work",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_author_work_work_works_id",
            schema: "ihfiction.dev2",
            table: "author_work",
            column: "works_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "work",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_tag_work_work_works_id",
            schema: "ihfiction.dev2",
            table: "tag_work",
            column: "works_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "work",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_work_authors_owner_id",
            schema: "ihfiction.dev2",
            table: "work",
            column: "owner_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_work_work_book_id",
            schema: "ihfiction.dev2",
            table: "work",
            column: "book_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "work",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "fk_work_work_story_id",
            schema: "ihfiction.dev2",
            table: "work",
            column: "story_id",
            principalSchema: "ihfiction.dev2",
            principalTable: "work",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }
}
