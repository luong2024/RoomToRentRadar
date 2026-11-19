using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingFavourites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_favourites",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    listing_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_favourites", x => new { x.user_id, x.listing_id });
                    table.ForeignKey(
                        name: "FK_listing_favourites_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_listing_favourites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_listing_favourites_listing_id",
                table: "listing_favourites",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_favourites_user_created",
                table: "listing_favourites",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_favourites");
        }
    }
}
