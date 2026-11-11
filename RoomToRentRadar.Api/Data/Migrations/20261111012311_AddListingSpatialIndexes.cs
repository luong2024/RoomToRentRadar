using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingSpatialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_listings_status_lat_lng",
                table: "listings",
                columns: new[] { "status", "lat", "lng" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_lng",
                table: "listings",
                columns: new[] { "status", "lng" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_listings_status_lat_lng",
                table: "listings");

            migrationBuilder.DropIndex(
                name: "ix_listings_status_lng",
                table: "listings");
        }
    }
}
