using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ControlCardMacIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialHash",
                table: "ControlCard");

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "ControlCard",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "ControlCard");

            migrationBuilder.AddColumn<string>(
                name: "CredentialHash",
                table: "ControlCard",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
