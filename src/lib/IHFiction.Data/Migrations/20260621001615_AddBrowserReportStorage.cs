using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrowserReportStorage : Migration
    {
        private static readonly string[] BrowserReportPayloadUniqueColumns = ["report_type", "payload_hash"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "browser_report_payloads",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    report_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    report_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    summary_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    summary_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    blocked_resource = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_file = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    line_number = table.Column<int>(type: "integer", nullable: true),
                    column_number = table.Column<int>(type: "integer", nullable: true),
                    disposition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    last_user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_browser_report_payloads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "csp_violation_reports",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    effective_directive = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    blocked_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    document_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_file = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    disposition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    original_policy = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    last_user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_sample = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_line_number = table.Column<int>(type: "integer", nullable: true),
                    last_column_number = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_csp_violation_reports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_browser_report_payloads_report_type_payload_hash",
                schema: "ihfiction.dev2",
                table: "browser_report_payloads",
                columns: BrowserReportPayloadUniqueColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_csp_violation_reports_fingerprint",
                schema: "ihfiction.dev2",
                table: "csp_violation_reports",
                column: "fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "browser_report_payloads",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "csp_violation_reports",
                schema: "ihfiction.dev2");
        }
    }
}
