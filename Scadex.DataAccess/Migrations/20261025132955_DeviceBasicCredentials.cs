using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DeviceBasicCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PcDevice_MacAddress",
                table: "PcDevice");

            migrationBuilder.DropIndex(
                name: "IX_ControlCard_MacAddress",
                table: "ControlCard");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "PcDevice");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "ControlCard");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "ControlCard");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "ControlCard");

            migrationBuilder.AddColumn<string>(
                name: "CredentialHash",
                table: "PcDevice",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialHash",
                table: "ControlCard",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialHash",
                table: "PcDevice");

            migrationBuilder.DropColumn(
                name: "CredentialHash",
                table: "ControlCard");

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "PcDevice",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "ControlCard",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "ControlCard",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "ControlCard",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcDevice_MacAddress",
                table: "PcDevice",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ControlCard_MacAddress",
                table: "ControlCard",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL");
        }
    }
}
