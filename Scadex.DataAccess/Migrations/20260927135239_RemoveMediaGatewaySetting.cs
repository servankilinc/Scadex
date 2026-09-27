using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMediaGatewaySetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaGatewaySettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaGatewaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RtspTransport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceOnDemandCloseAfter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenTtlSeconds = table.Column<int>(type: "int", nullable: false),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebRtcPublicBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaGatewaySettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MediaGatewaySettings",
                columns: new[] { "Id", "ApiBaseUrl", "ApiTimeoutMs", "CreateDateUtc", "CreatedBy", "RtspTransport", "SourceOnDemandCloseAfter", "TokenTtlSeconds", "UpdateDateUtc", "UpdatedBy", "WebRtcPublicBaseUrl" },
                values: new object[] { 1, "http://127.0.0.1:9997", 30000, null, null, "tcp", "10s", 60, null, null, "http://127.0.0.1:8889" });
        }
    }
}
