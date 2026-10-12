using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.RemoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileTransferLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileTransferLog",
                schema: "remotedesk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTransferLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileTransferLog_DeviceId_StartedUtc",
                schema: "remotedesk",
                table: "FileTransferLog",
                columns: new[] { "DeviceId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileTransferLog_UserId_StartedUtc",
                schema: "remotedesk",
                table: "FileTransferLog",
                columns: new[] { "UserId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileTransferLog",
                schema: "remotedesk");
        }
    }
}
