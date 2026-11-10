using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingAmenitiesAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_amenities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_amenities", x => x.id);
                    table.CheckConstraint("ck_listing_amenities_code", "code IN ('wifi', 'air_conditioner', 'parking', 'washing_machine', 'refrigerator', 'kitchen', 'private_bathroom', 'balcony', 'elevator', 'security', 'gym', 'pool', 'pet_allowed')");
                    table.CheckConstraint("ck_listing_amenities_sort_order_non_negative", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_listing_amenities_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_rules", x => x.id);
                    table.CheckConstraint("ck_listing_rules_sort_order_non_negative", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_listing_rules_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_amenities_listing_code",
                table: "listing_amenities",
                columns: new[] { "listing_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_amenities_listing_sort_order_id",
                table: "listing_amenities",
                columns: new[] { "listing_id", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_listing_rules_listing_sort_order_id",
                table: "listing_rules",
                columns: new[] { "listing_id", "sort_order", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_amenities");

            migrationBuilder.DropTable(
                name: "listing_rules");
        }
    }
}
