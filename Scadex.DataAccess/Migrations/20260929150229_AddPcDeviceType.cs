using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPcDeviceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SIRA ELLE DUZELTILDI: once tip, sonra sablon (FK_ComponentTemplate_DeviceType). EF tersini uretiyor.
            migrationBuilder.InsertData(
                table: "DeviceType",
                columns: new[] { "Id", "Category", "CreateDateUtc", "CreatedBy", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 13, "Field", null, null, "Pc", null, null });

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000007"),
                columns: new[] { "BackgroundColor", "DeviceTypeId" },
                values: new object[] { "#E0F2FE", 13 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SIRA ELLE DUZELTILDI: once sablon geri, sonra tip silinir.
            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000007"),
                columns: new[] { "BackgroundColor", "DeviceTypeId" },
                values: new object[] { "#F3E8FF", 7 });

            migrationBuilder.DeleteData(
                table: "DeviceType",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
