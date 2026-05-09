using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHFiction.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSystemPhase1 : Migration
    {
        private static readonly string[] DeviceAuthorFollowColumns = ["device_id", "author_id"];
        private static readonly string[] DeviceNotificationDeliveryColumns = ["device_id", "notification_id"];
        private static readonly string[] DeviceStoryFollowColumns = ["device_id", "story_id"];
        private static readonly string[] UserAuthorFollowColumns = ["user_id", "author_id"];
        private static readonly string[] UserNotificationDeliveryColumns = ["user_id", "notification_id"];
        private static readonly string[] UserStoryFollowColumns = ["user_id", "story_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "device_author_follows",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    author_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_author_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_author_follows_authors_author_id",
                        column: x => x.author_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_push_subscriptions",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    p256dh_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    auth_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_successful_delivery_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_push_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_story_follows",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    story_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_story_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_story_follows_stories_story_id",
                        column: x => x.story_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    notification_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    target_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    event_occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    author_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    story_id = table.Column<string>(type: "character varying(26)", nullable: true),
                    chapter_id = table.Column<string>(type: "character varying(26)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_authors_author_id",
                        column: x => x.author_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_notifications_stories_story_id",
                        column: x => x.story_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_author_follows",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    user_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    author_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_author_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_author_follows_authors_author_id",
                        column: x => x.author_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_author_follows_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_push_subscriptions",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    user_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    p256dh_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    auth_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_successful_delivery_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_follows",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    user_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    story_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_story_follows_stories_story_id",
                        column: x => x.story_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_story_follows_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_notification_deliveries",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    notification_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_notification_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_deliveries",
                schema: "ihfiction.dev2",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", nullable: false),
                    notification_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    user_id = table.Column<string>(type: "character varying(26)", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_notification_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_notification_deliveries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "ihfiction.dev2",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_author_follows_author_id",
                schema: "ihfiction.dev2",
                table: "device_author_follows",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_author_follows_device_id_author_id",
                schema: "ihfiction.dev2",
                table: "device_author_follows",
                columns: DeviceAuthorFollowColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_notification_deliveries_device_id_notification_id",
                schema: "ihfiction.dev2",
                table: "device_notification_deliveries",
                columns: DeviceNotificationDeliveryColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_notification_deliveries_notification_id",
                schema: "ihfiction.dev2",
                table: "device_notification_deliveries",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_push_subscriptions_device_id",
                schema: "ihfiction.dev2",
                table: "device_push_subscriptions",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_push_subscriptions_endpoint",
                schema: "ihfiction.dev2",
                table: "device_push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_story_follows_device_id_story_id",
                schema: "ihfiction.dev2",
                table: "device_story_follows",
                columns: DeviceStoryFollowColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_story_follows_story_id",
                schema: "ihfiction.dev2",
                table: "device_story_follows",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_author_id",
                schema: "ihfiction.dev2",
                table: "notifications",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_chapter_id",
                schema: "ihfiction.dev2",
                table: "notifications",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_notification_key",
                schema: "ihfiction.dev2",
                table: "notifications",
                column: "notification_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_story_id",
                schema: "ihfiction.dev2",
                table: "notifications",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_author_follows_author_id",
                schema: "ihfiction.dev2",
                table: "user_author_follows",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_author_follows_user_id_author_id",
                schema: "ihfiction.dev2",
                table: "user_author_follows",
                columns: UserAuthorFollowColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_deliveries_notification_id",
                schema: "ihfiction.dev2",
                table: "user_notification_deliveries",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_deliveries_user_id_notification_id",
                schema: "ihfiction.dev2",
                table: "user_notification_deliveries",
                columns: UserNotificationDeliveryColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_push_subscriptions_endpoint",
                schema: "ihfiction.dev2",
                table: "user_push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_push_subscriptions_user_id",
                schema: "ihfiction.dev2",
                table: "user_push_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_follows_story_id",
                schema: "ihfiction.dev2",
                table: "user_story_follows",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_follows_user_id_story_id",
                schema: "ihfiction.dev2",
                table: "user_story_follows",
                columns: UserStoryFollowColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "device_author_follows",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "device_notification_deliveries",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "device_push_subscriptions",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "device_story_follows",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "user_author_follows",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "user_notification_deliveries",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "user_push_subscriptions",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "user_story_follows",
                schema: "ihfiction.dev2");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "ihfiction.dev2");
        }
    }
}
