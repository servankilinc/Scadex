using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.RemoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteControlSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteControlSession",
                schema: "remotedesk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndReason = table.Column<int>(type: "int", nullable: true),
                    InputEventCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteControlSession", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteControlSession_DeviceId_StartedUtc",
                schema: "remotedesk",
                table: "RemoteControlSession",
                columns: new[] { "DeviceId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteControlSession_UserId_StartedUtc",
                schema: "remotedesk",
                table: "RemoteControlSession",
                columns: new[] { "UserId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteControlSession",
                schema: "remotedesk");
        }
    }
}
