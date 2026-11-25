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
            // SQLite cannot alter a CHECK constraint in place, so rebuild the table in the same copy.
            // Legacy "user" accounts that own at least one listing become landlords so they keep
            // access to listing management; other legacy accounts remain renters (students).
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_users" (
                    "id" TEXT NOT NULL CONSTRAINT "PK_users" PRIMARY KEY,
                    "created_at" TEXT NOT NULL,
                    "display_name" TEXT NOT NULL,
                    "email" TEXT NOT NULL,
                    "password_hash" TEXT NOT NULL,
                    "password_salt" TEXT NOT NULL,
                    "role" TEXT NOT NULL DEFAULT 'student',
                    CONSTRAINT "ck_users_role" CHECK (role IN ('student', 'landlord', 'moderator', 'admin'))
                );
                INSERT INTO "ef_temp_users" ("id", "created_at", "display_name", "email", "password_hash", "password_salt", "role")
                SELECT "id", "created_at", "display_name", "email", "password_hash", "password_salt",
                    CASE
                        WHEN "role" <> 'user' THEN "role"
                        WHEN EXISTS (SELECT 1 FROM "listings" WHERE "listings"."owner_id" = "users"."id") THEN 'landlord'
                        ELSE 'student'
                    END
                FROM "users";
                DROP TABLE "users";
                ALTER TABLE "ef_temp_users" RENAME TO "users";
                CREATE UNIQUE INDEX "ix_users_email" ON "users" ("email");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old role set has no landlord distinction; both account types become renters.
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_users" (
                    "id" TEXT NOT NULL CONSTRAINT "PK_users" PRIMARY KEY,
                    "created_at" TEXT NOT NULL,
                    "display_name" TEXT NOT NULL,
                    "email" TEXT NOT NULL,
                    "password_hash" TEXT NOT NULL,
                    "password_salt" TEXT NOT NULL,
                    "role" TEXT NOT NULL DEFAULT 'user',
                    CONSTRAINT "ck_users_role" CHECK (role IN ('user', 'moderator', 'admin'))
                );
                INSERT INTO "ef_temp_users" ("id", "created_at", "display_name", "email", "password_hash", "password_salt", "role")
                SELECT "id", "created_at", "display_name", "email", "password_hash", "password_salt",
                    CASE WHEN "role" IN ('student', 'landlord') THEN 'user' ELSE "role" END
                FROM "users";
                DROP TABLE "users";
                ALTER TABLE "ef_temp_users" RENAME TO "users";
                CREATE UNIQUE INDEX "ix_users_email" ON "users" ("email");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
