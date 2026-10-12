using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCriticalDeviceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DeviceStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "DeviceStatus",
                keyColumn: "Id",
                keyValue: 0,
                column: "Color",
                value: "#EF4444");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DeviceStatus",
                keyColumn: "Id",
                keyValue: 0,
                column: "Color",
                value: "#6B7280");

            migrationBuilder.InsertData(
                table: "DeviceStatus",
                columns: new[] { "Id", "Color", "CreateDateUtc", "CreatedBy", "Description", "Icon", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 3, "#EF4444", null, null, "Kritik ariza; mudahale gerekiyor.", "alert-octagon", "Critical", null, null });
        }
    }
}
