using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 3, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });

            migrationBuilder.DeleteData(
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });

            migrationBuilder.DeleteData(
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 8);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Category", "Code", "CreateDateUtc", "CreatedBy", "DisplayName", "UpdateDateUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { 3, "Alarm", "AcknowledgeAlarm", null, null, "Alarm kabul et", null, null },
                    { 7, "Data", "ExportData", null, null, "Veri dışarı aktar", null, null },
                    { 8, "Admin", "ManageWorkflow", null, null, "İş akışı yönet", null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 3, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 7, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 8, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") }
                });
        }
    }
}
