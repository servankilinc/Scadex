using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.Signalization.Data.Migrations
{
    /// <inheritdoc />
    public partial class OuterDoorLighting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LightIoChannelId",
                schema: "signalization",
                table: "OuterDoor",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OuterDoorState",
                schema: "signalization",
                columns: table => new
                {
                    OuterDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LightIsOn = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuterDoorState", x => x.OuterDoorId);
                    table.ForeignKey(
                        name: "FK_OuterDoorState_OuterDoor_OuterDoorId",
                        column: x => x.OuterDoorId,
                        principalSchema: "signalization",
                        principalTable: "OuterDoor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OuterDoorState",
                schema: "signalization");

            migrationBuilder.DropColumn(
                name: "LightIoChannelId",
                schema: "signalization",
                table: "OuterDoor");
        }
    }
}
