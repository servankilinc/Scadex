using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.RemoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRemoteDesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "remotedesk");

            migrationBuilder.CreateTable(
                name: "ScreenSession",
                schema: "remotedesk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonitorIndex = table.Column<int>(type: "int", nullable: false),
                    MediaPath = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoppedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StopReason = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScreenViewLog",
                schema: "remotedesk",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScreenSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonitorIndex = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenViewLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenViewLog_ScreenSession_ScreenSessionId",
                        column: x => x.ScreenSessionId,
                        principalSchema: "remotedesk",
                        principalTable: "ScreenSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenSession_CreatedUtc",
                schema: "remotedesk",
                table: "ScreenSession",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenSession_DeviceId_MonitorIndex",
                schema: "remotedesk",
                table: "ScreenSession",
                columns: new[] { "DeviceId", "MonitorIndex" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenViewLog_DeviceId_StartedUtc",
                schema: "remotedesk",
                table: "ScreenViewLog",
                columns: new[] { "DeviceId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenViewLog_ScreenSessionId",
                schema: "remotedesk",
                table: "ScreenViewLog",
                column: "ScreenSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenViewLog_UserId_StartedUtc",
                schema: "remotedesk",
                table: "ScreenViewLog",
                columns: new[] { "UserId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScreenViewLog",
                schema: "remotedesk");

            migrationBuilder.DropTable(
                name: "ScreenSession",
                schema: "remotedesk");
        }
    }
}
