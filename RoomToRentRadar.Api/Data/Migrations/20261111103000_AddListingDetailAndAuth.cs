using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RoomToRentRadar.Api.Data;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111103000_AddListingDetailAndAuth")]
    public partial class AddListingDetailAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "deposit",
                table: "listings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "room_type",
                table: "listings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "apartment");

            migrationBuilder.AddColumn<string>(
                name: "landlord_name",
                table: "listings",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "landlord_phone",
                table: "listings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    password_salt = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_token_hash",
                table: "auth_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_user_id",
                table: "auth_sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropColumn(
                name: "deposit",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "room_type",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "landlord_name",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "landlord_phone",
                table: "listings");
        }
    }
}
