using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceExternalCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Device_CabinetId_ExternalCode",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "ExternalCode",
                table: "Device");

            migrationBuilder.CreateIndex(
                name: "IX_Device_CabinetId",
                table: "Device",
                column: "CabinetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Device_CabinetId",
                table: "Device");

            migrationBuilder.AddColumn<string>(
                name: "ExternalCode",
                table: "Device",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Device_CabinetId_ExternalCode",
                table: "Device",
                columns: new[] { "CabinetId", "ExternalCode" },
                unique: true,
                filter: "[ExternalCode] IS NOT NULL AND [IsActive] = 1");
        }
    }
}
