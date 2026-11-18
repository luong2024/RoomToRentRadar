using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingReportsAndModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "user");

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "listings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "listing_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reporter_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    claimed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    claimed_until_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_reports", x => x.id);
                    table.CheckConstraint("ck_listing_reports_details_length", "details IS NULL OR length(details) <= 1000");
                    table.CheckConstraint("ck_listing_reports_reason", "reason IN ('spam', 'wrong_info', 'scam', 'offensive', 'already_rented')");
                    table.CheckConstraint("ck_listing_reports_status", "status IN ('open', 'in_review', 'resolved', 'dismissed')");
                    table.ForeignKey(
                        name: "FK_listing_reports_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_listing_reports_users_claimed_by_user_id",
                        column: x => x.claimed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_listing_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    resolution_action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    report_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    previous_report_status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    next_report_status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_audit_logs", x => x.id);
                    table.CheckConstraint("ck_moderation_audit_logs_action", "action IN ('claim', 'release', 'resolve')");
                    table.CheckConstraint("ck_moderation_audit_logs_next_status", "next_report_status IS NULL OR next_report_status IN ('open', 'in_review', 'resolved', 'dismissed')");
                    table.CheckConstraint("ck_moderation_audit_logs_previous_status", "previous_report_status IS NULL OR previous_report_status IN ('open', 'in_review', 'resolved', 'dismissed')");
                    table.CheckConstraint("ck_moderation_audit_logs_resolution_action", "resolution_action IS NULL OR resolution_action IN ('dismiss', 'hide_listing', 'remove_listing', 'warn_owner')");
                    table.ForeignKey(
                        name: "FK_moderation_audit_logs_listing_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "listing_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_audit_logs_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('user', 'moderator', 'admin')");

            migrationBuilder.CreateIndex(
                name: "IX_listing_reports_claimed_by_user_id",
                table: "listing_reports",
                column: "claimed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_reports_listing_id",
                table: "listing_reports",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "IX_listing_reports_reporter_id",
                table: "listing_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_reports_status_listing_id",
                table: "listing_reports",
                columns: new[] { "status", "listing_id" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_audit_logs_actor_created_at",
                table: "moderation_audit_logs",
                columns: new[] { "actor_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_audit_logs_listing_created_at",
                table: "moderation_audit_logs",
                columns: new[] { "listing_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_audit_logs_report_id",
                table: "moderation_audit_logs",
                column: "report_id");

            // SQLite enforces uniqueness while the report is active (open or in
            // review), so a user can report the listing again only after the
            // previous report was resolved or dismissed.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "ix_listing_reports_listing_reporter_open"
                ON "listing_reports" ("listing_id", "reporter_id")
                WHERE "status" IN ('open', 'in_review');
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "trg_moderation_audit_logs_no_update"
                BEFORE UPDATE ON "moderation_audit_logs"
                BEGIN
                    SELECT RAISE(ABORT, 'moderation audit log is append-only');
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "trg_moderation_audit_logs_no_delete"
                BEFORE DELETE ON "moderation_audit_logs"
                BEGIN
                    SELECT RAISE(ABORT, 'moderation audit log is append-only');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_delete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_update\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"ix_listing_reports_listing_reporter_open\";");

            migrationBuilder.DropTable(
                name: "moderation_audit_logs");

            migrationBuilder.DropTable(
                name: "listing_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "version",
                table: "listings");
        }
    }
}
