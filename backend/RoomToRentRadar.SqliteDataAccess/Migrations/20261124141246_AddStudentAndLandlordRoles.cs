using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.SqliteDataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAndLandlordRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The legacy constraint only allows the old values, so the normalization
            // runs before the rebuild with check enforcement temporarily suspended.
            // Legacy owners keep their listing-management access as landlords.
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = ON;");
            migrationBuilder.Sql(
                "UPDATE users SET role = 'landlord' WHERE role = 'user' " +
                "AND EXISTS (SELECT 1 FROM listings WHERE listings.owner_id = users.id);");
            migrationBuilder.Sql("UPDATE users SET role = 'student' WHERE role = 'user';");
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = OFF;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "student",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "user");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('student', 'landlord', 'moderator', 'admin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = ON;");
            migrationBuilder.Sql("UPDATE users SET role = 'user' WHERE role IN ('student', 'landlord');");
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = OFF;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "user",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "student");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('user', 'moderator', 'admin')");
        }
    }
}
