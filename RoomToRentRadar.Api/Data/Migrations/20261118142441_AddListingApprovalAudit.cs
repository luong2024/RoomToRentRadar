using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingApprovalAudit : Migration
    {
        // SQLite applies AlterColumn and check-constraint changes by rebuilding the
        // table, which drops the custom append-only triggers; they are recreated at
        // the end of both directions.
        private const string CreateNoUpdateTriggerSql =
            """
            CREATE TRIGGER "trg_moderation_audit_logs_no_update"
            BEFORE UPDATE ON "moderation_audit_logs"
            BEGIN
                SELECT RAISE(ABORT, 'moderation audit log is append-only');
            END;
            """;

        private const string CreateNoDeleteTriggerSql =
            """
            CREATE TRIGGER "trg_moderation_audit_logs_no_delete"
            BEFORE DELETE ON "moderation_audit_logs"
            BEGIN
                SELECT RAISE(ABORT, 'moderation audit log is append-only');
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_delete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_update\";");

            migrationBuilder.DropCheckConstraint(
                name: "ck_moderation_audit_logs_action",
                table: "moderation_audit_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "moderation_audit_logs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddCheckConstraint(
                name: "ck_moderation_audit_logs_action",
                table: "moderation_audit_logs",
                sql: "action IN ('claim', 'release', 'resolve', 'approve')");

            migrationBuilder.Sql(CreateNoUpdateTriggerSql);
            migrationBuilder.Sql(CreateNoDeleteTriggerSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_delete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_moderation_audit_logs_no_update\";");

            migrationBuilder.DropCheckConstraint(
                name: "ck_moderation_audit_logs_action",
                table: "moderation_audit_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "moderation_audit_logs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_moderation_audit_logs_action",
                table: "moderation_audit_logs",
                sql: "action IN ('claim', 'release', 'resolve')");

            migrationBuilder.Sql(CreateNoUpdateTriggerSql);
            migrationBuilder.Sql(CreateNoDeleteTriggerSql);
        }
    }
}
