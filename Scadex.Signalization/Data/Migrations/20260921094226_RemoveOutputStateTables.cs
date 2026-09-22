using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.Signalization.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOutputStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabinetState",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "InnerDoorState",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OuterDoorState",
                schema: "signalization");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CabinetState",
                schema: "signalization",
                columns: table => new
                {
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSirenCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SirenChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SirenIsOn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetState", x => x.CabinetId);
                });

            migrationBuilder.CreateTable(
                name: "InnerDoorState",
                schema: "signalization",
                columns: table => new
                {
                    InnerDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    LastCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InnerDoorState", x => x.InnerDoorId);
                    table.ForeignKey(
                        name: "FK_InnerDoorState_InnerDoor_InnerDoorId",
                        column: x => x.InnerDoorId,
                        principalSchema: "signalization",
                        principalTable: "InnerDoor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuterDoorState",
                schema: "signalization",
                columns: table => new
                {
                    OuterDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LightIsOn = table.Column<bool>(type: "bit", nullable: false)
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
    }
}
