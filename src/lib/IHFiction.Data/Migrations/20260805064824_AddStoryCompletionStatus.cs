using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryCompletionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "completion_status",
                schema: "ihfiction.dev2",
                table: "works",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                defaultValue: "InProgress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "completion_status",
                schema: "ihfiction.dev2",
                table: "works");
        }
    }
}
