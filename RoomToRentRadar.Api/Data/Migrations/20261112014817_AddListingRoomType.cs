using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomToRentRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingRoomType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "room_type",
                table: "listings",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "single_room");

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_room_type",
                table: "listings",
                columns: new[] { "status", "room_type" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_listings_room_type",
                table: "listings",
                sql: "room_type IN ('single_room', 'shared_room', 'studio', 'apartment', 'house')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_listings_status_room_type",
                table: "listings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_listings_room_type",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "room_type",
                table: "listings");
        }
    }
}
