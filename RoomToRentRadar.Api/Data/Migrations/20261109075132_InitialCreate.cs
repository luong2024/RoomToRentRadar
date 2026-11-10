using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    price = table.Column<long>(type: "INTEGER", nullable: false),
                    area_m2 = table.Column<double>(type: "REAL", nullable: false),
                    bedrooms = table.Column<int>(type: "INTEGER", nullable: false),
                    address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    lat = table.Column<double>(type: "REAL", nullable: false),
                    lng = table.Column<double>(type: "REAL", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listings", x => x.id);
                    table.CheckConstraint("ck_listings_area_non_negative", "area_m2 >= 0");
                    table.CheckConstraint("ck_listings_bedrooms_non_negative", "bedrooms >= 0");
                    table.CheckConstraint("ck_listings_price_non_negative", "price >= 0");
                    table.CheckConstraint("ck_listings_status", "status IN ('draft', 'published', 'hidden', 'removed')");
                });

            migrationBuilder.CreateTable(
                name: "listing_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    thumb_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    width = table.Column<int>(type: "INTEGER", nullable: false),
                    height = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    alt = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_cover = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_photos", x => x.id);
                    table.CheckConstraint("ck_listing_photos_height_positive", "height > 0");
                    table.CheckConstraint("ck_listing_photos_kind", "kind IN ('photo', 'panorama')");
                    table.CheckConstraint("ck_listing_photos_sort_order_non_negative", "sort_order >= 0");
                    table.CheckConstraint("ck_listing_photos_width_positive", "width > 0");
                    table.ForeignKey(
                        name: "FK_listing_photos_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_photos_listing_cover_sort_id",
                table: "listing_photos",
                columns: new[] { "listing_id", "is_cover", "sort_order", "id" },
                descending: new[] { false, true, false, false });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_area_m2_desc_id",
                table: "listings",
                columns: new[] { "status", "area_m2", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_bedrooms",
                table: "listings",
                columns: new[] { "status", "bedrooms" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_created_at_id",
                table: "listings",
                columns: new[] { "status", "created_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_price_desc_id",
                table: "listings",
                columns: new[] { "status", "price", "id" },
                descending: new[] { false, true, false });

            // EF collapses indexes over the same properties, even when the sort
            // direction differs, so the ascending price index is created here.
            migrationBuilder.CreateIndex(
                name: "ix_listings_status_price_id",
                table: "listings",
                columns: new[] { "status", "price", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_photos");

            migrationBuilder.DropTable(
                name: "listings");
        }
    }
}
