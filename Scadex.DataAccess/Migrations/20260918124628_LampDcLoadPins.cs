using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class LampDcLoadPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ComponentTemplatePin",
                keyColumn: "Id",
                keyValue: new Guid("7e300000-0000-0007-0008-000000000003"));

            migrationBuilder.UpdateData(
                table: "ComponentTemplatePin",
                keyColumn: "Id",
                keyValue: new Guid("7e300000-0000-0007-0008-000000000001"),
                columns: new[] { "Function", "Name", "RelativeX", "VoltageLevel" },
                values: new object[] { 3, "+12V", 0.32142857142857145, 1 });

            migrationBuilder.UpdateData(
                table: "ComponentTemplatePin",
                keyColumn: "Id",
                keyValue: new Guid("7e300000-0000-0007-0008-000000000002"),
                columns: new[] { "Function", "Name", "RelativeX", "VoltageLevel" },
                values: new object[] { 4, "GND", 0.6785714285714286, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ComponentTemplatePin",
                keyColumn: "Id",
                keyValue: new Guid("7e300000-0000-0007-0008-000000000001"),
                columns: new[] { "Function", "Name", "RelativeX", "VoltageLevel" },
                values: new object[] { 14, "L", 0.25, 3 });

            migrationBuilder.UpdateData(
                table: "ComponentTemplatePin",
                keyColumn: "Id",
                keyValue: new Guid("7e300000-0000-0007-0008-000000000002"),
                columns: new[] { "Function", "Name", "RelativeX", "VoltageLevel" },
                values: new object[] { 15, "N", 0.5, 3 });

            migrationBuilder.InsertData(
                table: "ComponentTemplatePin",
                columns: new[] { "Id", "ChannelNumber", "ComponentTemplateId", "CreateDateUtc", "CreatedBy", "Direction", "Function", "Name", "RelativeX", "RelativeY", "Side", "UpdateDateUtc", "UpdatedBy", "VoltageLevel" },
                values: new object[] { new Guid("7e300000-0000-0007-0008-000000000003"), null, new Guid("7e200000-0000-0000-0007-000000000008"), null, null, 0, 16, "PE", 0.75, 0.87058823529411766, 3, null, null, 3 });
        }
    }
}
