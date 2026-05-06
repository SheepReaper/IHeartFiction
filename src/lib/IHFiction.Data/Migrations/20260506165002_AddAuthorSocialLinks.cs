using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "author_social_links",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    author_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_author_social_links", x => new { x.author_id, x.type });
                    table.ForeignKey(
                        name: "fk_author_social_links_authors_author_id",
                        column: x => x.author_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "author_social_links",
                schema: "ihfiction.dev2");
        }
    }
}
