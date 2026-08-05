using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkReadCounts : Migration
    {
        private static readonly string[] ReaderHistoryColumns = ["reader_key", "last_read_at"];
        private static readonly string[] UniqueReaderColumns = ["work_id", "reader_key"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<int>(
                name: "read_count",
                schema: "ihfiction.dev2",
                table: "works",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "work_reads",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    work_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    reader_key = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                    is_counted = table.Column<bool>(type: "boolean", nullable: false),
                    first_read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_reads", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_reads_works_work_id",
                        column: x => x.work_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_reads_reader_key_last_read_at",
                schema: "ihfiction.dev2",
                table: "work_reads",
                columns: ReaderHistoryColumns);

            migrationBuilder.CreateIndex(
                name: "ix_work_reads_work_id_reader_key",
                schema: "ihfiction.dev2",
                table: "work_reads",
                columns: UniqueReaderColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "work_reads",
                schema: "ihfiction.dev2");

            migrationBuilder.DropColumn(
                name: "read_count",
                schema: "ihfiction.dev2",
                table: "works");
        }
    }
}