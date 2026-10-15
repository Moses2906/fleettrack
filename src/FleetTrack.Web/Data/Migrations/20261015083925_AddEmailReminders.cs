using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetTrack.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RECEIVES_REMINDER_EMAILS",
                table: "ASP_NET_USERS",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "REMINDERS_SENT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    KIND = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VEHICLE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DUE_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    MILESTONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    SENT_AT_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    RECIPIENT_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMINDERS_SENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REMINDERS_SENT_VEHICLES_VEHICLE_ID",
                        column: x => x.VEHICLE_ID,
                        principalTable: "VEHICLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REMINDERS_SENT_KIND_VEHICLE_ID_DUE_DATE_MILESTONE",
                table: "REMINDERS_SENT",
                columns: new[] { "KIND", "VEHICLE_ID", "DUE_DATE", "MILESTONE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMINDERS_SENT_VEHICLE_ID",
                table: "REMINDERS_SENT",
                column: "VEHICLE_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REMINDERS_SENT");

            migrationBuilder.DropColumn(
                name: "RECEIVES_REMINDER_EMAILS",
                table: "ASP_NET_USERS");
        }
    }
}
