using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameLampToLightingLed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000008"),
                column: "Name",
                value: "Aydınlatma LED'i");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000008"),
                column: "Name",
                value: "Lamba");
        }
    }
}
