using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketingApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "user"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "contact_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_groups_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "info"),
                    related_entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_smtp_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false, defaultValue: 587),
                    smtp_username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smtp_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    smtp_from_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    smtp_from_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    smtp_enable_ssl = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    smtp_use_default_credentials = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    smtp_timeout = table.Column<int>(type: "integer", nullable: false, defaultValue: 30000),
                    whats_app_api_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    whats_app_phone_number_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    whats_app_business_account_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sms_api_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sms_api_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sms_sender_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notify_on_campaign_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_on_message_failed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notification_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_smtp_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_smtp_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    whats_app_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_contacts_contact_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "contact_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_contacts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_contacts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sent_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaigns_contact_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "contact_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaigns_message_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "message_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaigns_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_messages_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_messages_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_messages_campaign_id",
                table: "campaign_messages",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_messages_campaign_id_status",
                table: "campaign_messages",
                columns: new[] { "campaign_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_messages_contact_id",
                table: "campaign_messages",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_messages_status",
                table: "campaign_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_group_id",
                table: "campaigns",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_status",
                table: "campaigns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_template_id",
                table: "campaigns",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_user_id",
                table: "campaigns",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_contact_groups_user_id",
                table: "contact_groups",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_email",
                table: "contacts",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_group_id",
                table: "contacts",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_phone",
                table: "contacts",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_user_id",
                table: "contacts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_templates_user_id",
                table: "message_templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_is_read",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_smtp_settings_user_id",
                table: "user_smtp_settings",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "campaign_messages");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_smtp_settings");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "message_templates");

            migrationBuilder.DropTable(
                name: "contact_groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
