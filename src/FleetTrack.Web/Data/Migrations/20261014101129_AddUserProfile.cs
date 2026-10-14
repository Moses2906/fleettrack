using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetTrack.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FULL_NAME",
                table: "ASP_NET_USERS",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IS_ACTIVE",
                table: "ASP_NET_USERS",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FULL_NAME",
                table: "ASP_NET_USERS");

            migrationBuilder.DropColumn(
                name: "IS_ACTIVE",
                table: "ASP_NET_USERS");
        }
    }
}
