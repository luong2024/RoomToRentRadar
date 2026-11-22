using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.SqliteDataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedSearchesAndAlertRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    saved_search_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_searches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    area_json = table.Column<string>(type: "TEXT", nullable: false),
                    alert_state = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    alerts_activated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_searches", x => x.id);
                    table.CheckConstraint("ck_saved_searches_alert_state", "alert_state IN ('off', 'active', 'paused')");
                    table.ForeignKey(
                        name: "FK_saved_searches_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_search_alert_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    saved_search_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    delivered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_search_alert_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_search_alert_logs_saved_searches_saved_search_id",
                        column: x => x.saved_search_id,
                        principalTable: "saved_searches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_created_at",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_saved_search_alert_logs_search_created_at",
                table: "saved_search_alert_logs",
                columns: new[] { "saved_search_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_saved_search_alert_logs_search_listing",
                table: "saved_search_alert_logs",
                columns: new[] { "saved_search_id", "listing_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_alert_state",
                table: "saved_searches",
                column: "alert_state");

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_user_id",
                table: "saved_searches",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "saved_search_alert_logs");

            migrationBuilder.DropTable(
                name: "saved_searches");
        }
    }
}
