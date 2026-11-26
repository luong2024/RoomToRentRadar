using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.SqliteDataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registration_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    password_salt = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    otp_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    otp_salt = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    attempt_count = table.Column<int>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_challenges", x => x.id);
                    table.CheckConstraint("ck_registration_challenges_role", "role IN ('student', 'landlord')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_registration_challenges_email",
                table: "registration_challenges",
                column: "email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_challenges");
        }
    }
}
