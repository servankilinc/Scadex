using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.Signalization.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSignalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "signalization");

            migrationBuilder.CreateTable(
                name: "Authority",
                schema: "signalization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authority", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cabinet",
                schema: "signalization",
                columns: table => new
                {
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SirenIoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SirenDurationSec = table.Column<int>(type: "int", nullable: false),
                    EntrySnapshotCount = table.Column<int>(type: "int", nullable: false),
                    EntrySnapshotIntervalMs = table.Column<int>(type: "int", nullable: false),
                    AwaitingCardTimeoutSec = table.Column<int>(type: "int", nullable: false),
                    SessionMaxDurationMin = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinet", x => x.CabinetId);
                });

            migrationBuilder.CreateTable(
                name: "CabinetState",
                schema: "signalization",
                columns: table => new
                {
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SirenIsOn = table.Column<bool>(type: "bit", nullable: false),
                    SirenChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSirenCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetState", x => x.CabinetId);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSession",
                schema: "signalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OuterDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OuterDoorNameSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSec = table.Column<int>(type: "int", nullable: true),
                    AwaitingCardDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxDurationDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SirenRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SirenOffDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SirenReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OuterDoor",
                schema: "signalization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SwitchIoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwitchOpenValue = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuterDoor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OuterDoor_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalSchema: "signalization",
                        principalTable: "Cabinet",
                        principalColumn: "CabinetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSessionCapture",
                schema: "signalization",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    CameraCaptureId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSessionCapture", x => new { x.SessionId, x.CameraCaptureId });
                    table.ForeignKey(
                        name: "FK_OperatorSessionCapture_OperatorSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "signalization",
                        principalTable: "OperatorSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSessionEvent",
                schema: "signalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InnerDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CardIdRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CameraCaptureId = table.Column<long>(type: "bigint", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSessionEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorSessionEvent_OperatorSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "signalization",
                        principalTable: "OperatorSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSessionOperator",
                schema: "signalization",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AuthorityNameSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CardIdRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FirstCardAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCardAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSessionOperator", x => new { x.SessionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_OperatorSessionOperator_OperatorSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "signalization",
                        principalTable: "OperatorSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InnerDoor",
                schema: "signalization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OuterDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwitchIoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwitchOpenValue = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LockIoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnlockTurnsOn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InnerDoor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InnerDoor_Authority_AuthorityId",
                        column: x => x.AuthorityId,
                        principalSchema: "signalization",
                        principalTable: "Authority",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InnerDoor_OuterDoor_OuterDoorId",
                        column: x => x.OuterDoorId,
                        principalSchema: "signalization",
                        principalTable: "OuterDoor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InnerDoorState",
                schema: "signalization",
                columns: table => new
                {
                    InnerDoorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_Authority_Name",
                schema: "signalization",
                table: "Authority",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Authority_RoleId",
                schema: "signalization",
                table: "Authority",
                column: "RoleId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InnerDoor_AuthorityId",
                schema: "signalization",
                table: "InnerDoor",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_InnerDoor_OuterDoorId",
                schema: "signalization",
                table: "InnerDoor",
                column: "OuterDoorId");

            migrationBuilder.CreateIndex(
                name: "IX_InnerDoor_SwitchIoChannelId",
                schema: "signalization",
                table: "InnerDoor",
                column: "SwitchIoChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSession_CabinetId_StartedAtUtc",
                schema: "signalization",
                table: "OperatorSession",
                columns: new[] { "CabinetId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSession_OuterDoorId",
                schema: "signalization",
                table: "OperatorSession",
                column: "OuterDoorId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSession_StartedAtUtc",
                schema: "signalization",
                table: "OperatorSession",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessionEvent_SessionId_OccurredAtUtc",
                schema: "signalization",
                table: "OperatorSessionEvent",
                columns: new[] { "SessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessionOperator_UserId",
                schema: "signalization",
                table: "OperatorSessionOperator",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OuterDoor_CabinetId",
                schema: "signalization",
                table: "OuterDoor",
                column: "CabinetId");

            migrationBuilder.CreateIndex(
                name: "IX_OuterDoor_SwitchIoChannelId",
                schema: "signalization",
                table: "OuterDoor",
                column: "SwitchIoChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabinetState",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "InnerDoorState",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OperatorSessionCapture",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OperatorSessionEvent",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OperatorSessionOperator",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "InnerDoor",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OperatorSession",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "Authority",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "OuterDoor",
                schema: "signalization");

            migrationBuilder.DropTable(
                name: "Cabinet",
                schema: "signalization");
        }
    }
}
