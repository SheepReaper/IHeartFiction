// Generated migration intentionally relies on the project's implicit System namespace.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentAuthentication : Migration
    {
        private static readonly string[] ProviderAssertionColumns = ["provider_issuer", "assertion_jti"];
        private static readonly string[] ProviderIdentityColumns = ["provider_issuer", "provider_subject"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "agent_access_tokens",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    registration_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    token_jti = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_access_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_assertion_replays",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    provider_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    assertion_jti = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_assertion_replays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_identity_links",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    provider_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    provider_subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    verified_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_identity_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_registrations",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    provider_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    provider_subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    verified_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    provider_client_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    identity_link_id = table.Column<string>(type: "character varying(26)", nullable: true),
                    claim_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_polled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_registrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_access_tokens_registration_id",
                schema: "ihfiction.dev2",
                table: "agent_access_tokens",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_access_tokens_token_jti",
                schema: "ihfiction.dev2",
                table: "agent_access_tokens",
                column: "token_jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_assertion_replays_expires_at",
                schema: "ihfiction.dev2",
                table: "agent_assertion_replays",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_assertion_replays_provider_issuer_assertion_jti",
                schema: "ihfiction.dev2",
                table: "agent_assertion_replays",
                columns: ProviderAssertionColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_identity_links_provider_issuer_provider_subject",
                schema: "ihfiction.dev2",
                table: "agent_identity_links",
                columns: ProviderIdentityColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_identity_links_user_id",
                schema: "ihfiction.dev2",
                table: "agent_identity_links",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_registrations_claim_token_hash",
                schema: "ihfiction.dev2",
                table: "agent_registrations",
                column: "claim_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_registrations_identity_link_id",
                schema: "ihfiction.dev2",
                table: "agent_registrations",
                column: "identity_link_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_registrations_user_code_hash",
                schema: "ihfiction.dev2",
                table: "agent_registrations",
                column: "user_code_hash",
                unique: true,
                filter: "user_code_hash IS NOT NULL AND status = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "agent_access_tokens",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "agent_assertion_replays",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "agent_identity_links",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "agent_registrations",
                schema: "ihfiction.dev2");
        }
    }
}
