using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentityCardId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityCardId",
                table: "User",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56"),
                column: "IdentityCardId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_User_IdentityCardId",
                table: "User",
                column: "IdentityCardId",
                unique: true,
                filter: "[IdentityCardId] IS NOT NULL AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_IdentityCardId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IdentityCardId",
                table: "User");
        }
    }
}
