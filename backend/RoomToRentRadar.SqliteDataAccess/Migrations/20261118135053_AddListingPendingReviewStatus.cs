using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.SqliteDataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddListingPendingReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_listings_status",
                table: "listings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_listings_status",
                table: "listings",
                sql: "status IN ('draft', 'pending_review', 'published', 'hidden', 'removed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_listings_status",
                table: "listings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_listings_status",
                table: "listings",
                sql: "status IN ('draft', 'published', 'hidden', 'removed')");
        }
    }
}
