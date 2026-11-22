using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.SqliteDataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddListingHouseRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_house_rule_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    quiet_hours = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    guest_policy = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    cooking_rules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    parking_rules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_house_rule_versions", x => x.id);
                    table.CheckConstraint("ck_listing_house_rule_versions_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "FK_listing_house_rule_versions_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_house_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quiet_hours = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    guest_policy = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    cooking_rules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    parking_rules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_house_rules", x => x.id);
                    table.CheckConstraint("ck_listing_house_rules_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "FK_listing_house_rules_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_house_rule_versions_listing_version",
                table: "listing_house_rule_versions",
                columns: new[] { "listing_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_house_rules_listing",
                table: "listing_house_rules",
                column: "listing_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_house_rule_versions");

            migrationBuilder.DropTable(
                name: "listing_house_rules");
        }
    }
}
