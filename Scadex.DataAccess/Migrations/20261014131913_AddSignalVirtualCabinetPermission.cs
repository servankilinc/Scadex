using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalVirtualCabinetPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 0,
                column: "DisplayName",
                value: "Diyagramı izle");

            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 1,
                column: "DisplayName",
                value: "Diyagramı düzenle");

            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Category", "DisplayName" },
                values: new object[] { "Diagram", "Diyagramdan komut gönder (röle / kilit / siren)" });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Category", "Code", "CreateDateUtc", "CreatedBy", "DisplayName", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 15, "Signalization", "SignalVirtualCabinet", null, null, "Sanal kabini aç ve komut gönder", null, null });

            migrationBuilder.InsertData(
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 15, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 15, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 0,
                column: "DisplayName",
                value: "Diyagrami goruntule");

            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 1,
                column: "DisplayName",
                value: "Diyagrami duzenle");

            migrationBuilder.UpdateData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Category", "DisplayName" },
                values: new object[] { "Control", "Cikis sur (role / kilit / siren)" });
        }
    }
}
