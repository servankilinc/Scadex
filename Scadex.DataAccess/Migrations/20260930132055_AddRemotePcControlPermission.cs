using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRemotePcControlPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Category", "Code", "CreateDateUtc", "CreatedBy", "DisplayName", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 11, "RemoteDesk", "RemotePcControl", null, null, "PC'yi uzaktan kontrol et", null, null });

            migrationBuilder.InsertData(
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 11, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 11);
        }
    }
}
