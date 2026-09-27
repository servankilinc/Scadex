using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMediaGatewayRecordRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordRoot",
                table: "MediaGatewaySettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordRoot",
                table: "MediaGatewaySettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "MediaGatewaySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "RecordRoot",
                value: "C:\\Scadex\\mediamtx-records");
        }
    }
}
