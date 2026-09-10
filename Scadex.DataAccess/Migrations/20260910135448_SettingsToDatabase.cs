using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SettingsToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraCaptureSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    SnapshotCacheSeconds = table.Column<int>(type: "int", nullable: false),
                    CaptureRoot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaptureRetentionDays = table.Column<int>(type: "int", nullable: false),
                    MaxClipDurationSec = table.Column<int>(type: "int", nullable: false),
                    ClipFinalizeGraceMs = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraCaptureSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaGatewaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WebRtcPublicBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenTtlSeconds = table.Column<int>(type: "int", nullable: false),
                    SourceOnDemandCloseAfter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RtspTransport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordRoot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaGatewaySettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CameraCaptureSettings",
                columns: new[] { "Id", "CaptureRetentionDays", "CaptureRoot", "ClipFinalizeGraceMs", "CreateDateUtc", "CreatedBy", "MaxClipDurationSec", "SnapshotCacheSeconds", "SnapshotTimeoutMs", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 1, 30, "uploads/captures", 3000, null, null, 600, 3, 5000, null, null });

            migrationBuilder.InsertData(
                table: "MediaGatewaySettings",
                columns: new[] { "Id", "ApiBaseUrl", "ApiTimeoutMs", "CreateDateUtc", "CreatedBy", "RecordRoot", "RtspTransport", "SourceOnDemandCloseAfter", "TokenTtlSeconds", "UpdateDateUtc", "UpdatedBy", "WebRtcPublicBaseUrl" },
                values: new object[] { 1, "http://127.0.0.1:9997", 30000, null, null, "C:\\Scadex\\mediamtx-records", "tcp", "10s", 60, null, null, "http://127.0.0.1:8889" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraCaptureSettings");

            migrationBuilder.DropTable(
                name: "MediaGatewaySettings");
        }
    }
}
