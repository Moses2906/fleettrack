using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetTrack.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "INVOICE_CONTENT_TYPE",
                table: "SERVICE_RECORDS",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "INVOICE_FILE_NAME",
                table: "SERVICE_RECORDS",
                type: "NVARCHAR2(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "INVOICE_FILE_SIZE_BYTES",
                table: "SERVICE_RECORDS",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "INVOICE_SHA256",
                table: "SERVICE_RECORDS",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "INVOICE_STORED_FILE_NAME",
                table: "SERVICE_RECORDS",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "INVOICE_CONTENT_TYPE",
                table: "SERVICE_RECORDS");

            migrationBuilder.DropColumn(
                name: "INVOICE_FILE_NAME",
                table: "SERVICE_RECORDS");

            migrationBuilder.DropColumn(
                name: "INVOICE_FILE_SIZE_BYTES",
                table: "SERVICE_RECORDS");

            migrationBuilder.DropColumn(
                name: "INVOICE_SHA256",
                table: "SERVICE_RECORDS");

            migrationBuilder.DropColumn(
                name: "INVOICE_STORED_FILE_NAME",
                table: "SERVICE_RECORDS");
        }
    }
}
