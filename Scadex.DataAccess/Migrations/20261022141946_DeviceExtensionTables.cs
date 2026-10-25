using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Scadex.DataAccess.Migrations
{
    /// <summary>
    /// Device merkezli model (2026-10-22): tipe özgü alanlar 1-1 ek tablolara taşınır (ortak birincil anahtar, DeviceId).
    /// <list type="bullet">
    /// <item>Eski kök <c>Camera</c> tablosundaki her kamera AYNI Id ile bir <c>Device</c> satırı olur (şablon "IP Kamera",
    /// tip 14) ve pinleri şablondan üretilir; <c>Camera</c> yalnızca ek alanları taşır. Id korunduğu için
    /// <c>CameraCapture.CameraId</c>, <c>signalization.OuterDoor.CameraId</c> ve <c>cam_{id}</c> MediaMTX yolları değişmez.</item>
    /// <item><c>Device.MacAddress</c> → <c>ControlCard.MacAddress</c> (kontrol modülü) / <c>PcDevice.MacAddress</c> (PC).
    /// Pasif cihazların MAC'i taşınmaz: yeni index'ler <c>Device.IsActive</c>'e bakamaz.</item>
    /// </list>
    /// EF'in iskelesi veri kaybettiriyordu (eski <c>Camera.Id</c>'yi siler, <c>CabinetId</c>'yi <c>DeviceId</c> yapardı);
    /// Up/Down elle yazıldı. Sıra bağımlıdır: şablon seed'i Device eklemesinden, Device eklemesi kolon silmeden önce gelir.
    /// </summary>
    public partial class DeviceExtensionTables : Migration
    {
        // AppDbContext.SeedTemplateId(DeviceType.Camera, 1) — seed ile birebir aynı olmalı.
        private const string CameraTemplateId = "7e200000-0000-0000-0014-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 1) Kamera tipi + sistem şablonu (Device eklemesi bunlara FK ile bakar) ----
            migrationBuilder.InsertData(
                table: "DeviceType",
                columns: new[] { "Id", "Category", "CreateDateUtc", "CreatedBy", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 14, "Field", null, null, "Camera", null, null });

            migrationBuilder.InsertData(
                table: "ComponentTemplate",
                columns: new[] { "Id", "BackgroundColor", "BackgroundImageUrl", "CreateDateUtc", "CreatedBy", "DeviceTypeId", "Height", "IsActive", "IsMonitorable", "IsSystemTemplate", "Name", "UpdateDateUtc", "UpdatedBy", "Width" },
                values: new object[] { new Guid(CameraTemplateId), "#EDE9FE", "/templates/system/camera.svg", null, null, 14, 150.0, true, true, true, "IP Kamera", null, null, 200.0 });

            migrationBuilder.InsertData(
                table: "ComponentTemplatePin",
                columns: new[] { "Id", "ChannelNumber", "ComponentTemplateId", "CreateDateUtc", "CreatedBy", "Direction", "Function", "Name", "RelativeX", "RelativeY", "Side", "UpdateDateUtc", "UpdatedBy", "VoltageLevel" },
                values: new object[,]
                {
                    { new Guid("7e300000-0000-0014-0001-000000000001"), null, new Guid(CameraTemplateId), null, null, 0, 3, "+12V", 0.25, 0.85333333333333339, 3, null, null, 1 },
                    { new Guid("7e300000-0000-0014-0001-000000000002"), null, new Guid(CameraTemplateId), null, null, 0, 4, "GND", 0.45000000000000001, 0.85333333333333339, 3, null, null, 1 },
                    { new Guid("7e300000-0000-0014-0001-000000000003"), null, new Guid(CameraTemplateId), null, null, 2, 7, "RJ45", 0.75, 0.85333333333333339, 3, null, null, 5 }
                });

            // ---- 2) ControlCard / PcDevice: tablolar + MAC'in Device'tan taşınması ----
            migrationBuilder.CreateTable(
                name: "ControlCard",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlCard", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_ControlCard_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PcDevice",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcDevice", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_PcDevice_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Tipteki HER cihaz (pasif dahil) satir alir; MAC yalnizca aktif cihazdan tasinir.
            migrationBuilder.Sql(@"
INSERT INTO [ControlCard] ([DeviceId], [MacAddress], [Username], [Password])
SELECT d.[Id], CASE WHEN d.[IsActive] = 1 THEN d.[MacAddress] END, NULL, NULL
FROM [Device] d
INNER JOIN [ComponentTemplate] t ON t.[Id] = d.[ComponentTemplateId]
WHERE t.[DeviceTypeId] = 1;

INSERT INTO [PcDevice] ([DeviceId], [MacAddress])
SELECT d.[Id], CASE WHEN d.[IsActive] = 1 THEN d.[MacAddress] END
FROM [Device] d
INNER JOIN [ComponentTemplate] t ON t.[Id] = d.[ComponentTemplateId]
WHERE t.[DeviceTypeId] = 13;");

            migrationBuilder.DropIndex(
                name: "IX_Device_MacAddress",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "Device");

            migrationBuilder.CreateIndex(
                name: "IX_ControlCard_MacAddress",
                table: "ControlCard",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PcDevice_MacAddress",
                table: "PcDevice",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL");

            // ---- 3) Kameralar: Device satiri (ayni Id) + sablondan pinler ----
            // Tuvalin solunda kabin basina bir sutun; operator diyagramda yerlestirir.
            migrationBuilder.Sql($@"
INSERT INTO [Device] ([Id], [CabinetId], [ComponentTemplateId], [Name], [DeviceStatusId], [IpAddress],
    [CoordinateX], [CoordinateY], [Width], [Height], [Rotation], [ZIndex], [IsLocked], [IsVisible],
    [LastSeen], [MonitoringPort], [PingIntervalSec], [IsMonitoringEnabled], [LastConnectionError],
    [CreatedBy], [UpdatedBy], [CreateDateUtc], [UpdateDateUtc], [IsActive])
SELECT c.[Id], c.[CabinetId], '{CameraTemplateId}', c.[Name], c.[DeviceStatusId], c.[IpAddress],
    -300.0, (ROW_NUMBER() OVER (PARTITION BY c.[CabinetId] ORDER BY c.[Name]) - 1) * 180.0, NULL, NULL, 0.0, 0, 0, 1,
    c.[LastSeen], c.[MonitoringPort], c.[PingIntervalSec], c.[IsMonitoringEnabled], c.[LastConnectionError],
    c.[CreatedBy], c.[UpdatedBy], c.[CreateDateUtc], c.[UpdateDateUtc], c.[IsActive]
FROM [Camera] c;

INSERT INTO [Pin] ([Id], [ComponentTemplatePinId], [DeviceId], [IoChannelId], [ChannelNumber], [Name], [Function], [Direction],
    [VoltageLevel], [RelativeX], [RelativeY], [Side], [CreatedBy], [UpdatedBy], [CreateDateUtc], [UpdateDateUtc],
    [IsDeleted], [DeletedBy], [DeletedDateUtc])
SELECT NEWID(), tp.[Id], c.[Id], NULL, tp.[ChannelNumber], tp.[Name], tp.[Function], tp.[Direction],
    tp.[VoltageLevel], tp.[RelativeX], tp.[RelativeY], tp.[Side], NULL, NULL, SYSUTCDATETIME(), NULL,
    0, NULL, NULL
FROM [Camera] c
CROSS JOIN [ComponentTemplatePin] tp
WHERE tp.[ComponentTemplateId] = '{CameraTemplateId}';");

            // ---- 4) Camera tablosu ek tabloya daralir: Id -> DeviceId, cihaza tasinan kolonlar duser ----
            migrationBuilder.DropForeignKey(
                name: "FK_Camera_Cabinet_CabinetId",
                table: "Camera");

            migrationBuilder.DropForeignKey(
                name: "FK_Camera_DeviceStatus_DeviceStatusId",
                table: "Camera");

            migrationBuilder.DropForeignKey(
                name: "FK_CameraCapture_Camera_CameraId",
                table: "CameraCapture");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Camera",
                table: "Camera");

            migrationBuilder.DropIndex(
                name: "IX_Camera_CabinetId_IpAddress",
                table: "Camera");

            migrationBuilder.DropIndex(
                name: "IX_Camera_CabinetId_Name",
                table: "Camera");

            migrationBuilder.DropIndex(
                name: "IX_Camera_DeviceStatusId",
                table: "Camera");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Camera",
                newName: "DeviceId");

            foreach (var column in new[] { "CabinetId", "CreateDateUtc", "CreatedBy", "DeviceStatusId", "IpAddress", "IsActive",
                         "IsMonitoringEnabled", "LastConnectionError", "LastSeen", "MonitoringPort", "Name", "PingIntervalSec",
                         "UpdateDateUtc", "UpdatedBy" })
            {
                migrationBuilder.DropColumn(name: column, table: "Camera");
            }

            migrationBuilder.AddPrimaryKey(
                name: "PK_Camera",
                table: "Camera",
                column: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Camera_Device_DeviceId",
                table: "Camera",
                column: "DeviceId",
                principalTable: "Device",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CameraCapture_Camera_CameraId",
                table: "CameraCapture",
                column: "CameraId",
                principalTable: "Camera",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---- 1) Camera eski kok tabloya genisler (degerler Device'tan) ----
            migrationBuilder.DropForeignKey(
                name: "FK_Camera_Device_DeviceId",
                table: "Camera");

            migrationBuilder.DropForeignKey(
                name: "FK_CameraCapture_Camera_CameraId",
                table: "CameraCapture");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Camera",
                table: "Camera");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "Camera",
                newName: "Id");

            migrationBuilder.AddColumn<Guid>(name: "CabinetId", table: "Camera", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<string>(name: "Name", table: "Camera", type: "nvarchar(450)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "IpAddress", table: "Camera", type: "nvarchar(450)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>(name: "DeviceStatusId", table: "Camera", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "LastSeen", table: "Camera", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>(name: "MonitoringPort", table: "Camera", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PingIntervalSec", table: "Camera", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "IsMonitoringEnabled", table: "Camera", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "LastConnectionError", table: "Camera", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CreatedBy", table: "Camera", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "UpdatedBy", table: "Camera", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "CreateDateUtc", table: "Camera", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "UpdateDateUtc", table: "Camera", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IsActive", table: "Camera", type: "bit", nullable: false, defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE c SET
    c.[CabinetId] = d.[CabinetId], c.[Name] = d.[Name], c.[IpAddress] = COALESCE(d.[IpAddress], ''),
    c.[DeviceStatusId] = d.[DeviceStatusId], c.[LastSeen] = d.[LastSeen], c.[MonitoringPort] = d.[MonitoringPort],
    c.[PingIntervalSec] = d.[PingIntervalSec], c.[IsMonitoringEnabled] = d.[IsMonitoringEnabled],
    c.[LastConnectionError] = d.[LastConnectionError], c.[CreatedBy] = d.[CreatedBy], c.[UpdatedBy] = d.[UpdatedBy],
    c.[CreateDateUtc] = d.[CreateDateUtc], c.[UpdateDateUtc] = d.[UpdateDateUtc], c.[IsActive] = d.[IsActive]
FROM [Camera] c
INNER JOIN [Device] d ON d.[Id] = c.[Id];");

            // ---- 2) Kamera cihazlari diyagramdan kalkar (kablolari -> pinleri -> cihaz), sonra seed ----
            migrationBuilder.Sql($@"
DELETE cn FROM [Connection] cn
WHERE cn.[SourcePinId] IN (SELECT p.[Id] FROM [Pin] p INNER JOIN [Device] d ON d.[Id] = p.[DeviceId] WHERE d.[ComponentTemplateId] = '{CameraTemplateId}')
   OR cn.[TargetPinId] IN (SELECT p.[Id] FROM [Pin] p INNER JOIN [Device] d ON d.[Id] = p.[DeviceId] WHERE d.[ComponentTemplateId] = '{CameraTemplateId}');
DELETE p FROM [Pin] p INNER JOIN [Device] d ON d.[Id] = p.[DeviceId] WHERE d.[ComponentTemplateId] = '{CameraTemplateId}';
DELETE FROM [Device] WHERE [ComponentTemplateId] = '{CameraTemplateId}';");

            migrationBuilder.DeleteData(table: "ComponentTemplatePin", keyColumn: "Id", keyValue: new Guid("7e300000-0000-0014-0001-000000000001"));
            migrationBuilder.DeleteData(table: "ComponentTemplatePin", keyColumn: "Id", keyValue: new Guid("7e300000-0000-0014-0001-000000000002"));
            migrationBuilder.DeleteData(table: "ComponentTemplatePin", keyColumn: "Id", keyValue: new Guid("7e300000-0000-0014-0001-000000000003"));
            migrationBuilder.DeleteData(table: "ComponentTemplate", keyColumn: "Id", keyValue: new Guid(CameraTemplateId));
            migrationBuilder.DeleteData(table: "DeviceType", keyColumn: "Id", keyValue: 14);

            migrationBuilder.AddPrimaryKey(name: "PK_Camera", table: "Camera", column: "Id");

            migrationBuilder.CreateIndex(name: "IX_Camera_CabinetId_IpAddress", table: "Camera", columns: new[] { "CabinetId", "IpAddress" }, unique: true, filter: "[IsActive] = 1");
            migrationBuilder.CreateIndex(name: "IX_Camera_CabinetId_Name", table: "Camera", columns: new[] { "CabinetId", "Name" }, unique: true, filter: "[IsActive] = 1");
            migrationBuilder.CreateIndex(name: "IX_Camera_DeviceStatusId", table: "Camera", column: "DeviceStatusId");

            migrationBuilder.AddForeignKey(name: "FK_Camera_Cabinet_CabinetId", table: "Camera", column: "CabinetId", principalTable: "Cabinet", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Camera_DeviceStatus_DeviceStatusId", table: "Camera", column: "DeviceStatusId", principalTable: "DeviceStatus", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_CameraCapture_Camera_CameraId", table: "CameraCapture", column: "CameraId", principalTable: "Camera", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            // ---- 3) MAC Device'a geri doner ----
            migrationBuilder.AddColumn<string>(name: "MacAddress", table: "Device", type: "nvarchar(450)", nullable: true);

            migrationBuilder.Sql(@"
UPDATE d SET d.[MacAddress] = cc.[MacAddress] FROM [Device] d INNER JOIN [ControlCard] cc ON cc.[DeviceId] = d.[Id];
UPDATE d SET d.[MacAddress] = pc.[MacAddress] FROM [Device] d INNER JOIN [PcDevice] pc ON pc.[DeviceId] = d.[Id];");

            migrationBuilder.DropTable(name: "ControlCard");
            migrationBuilder.DropTable(name: "PcDevice");

            migrationBuilder.CreateIndex(
                name: "IX_Device_MacAddress",
                table: "Device",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL AND [IsActive] = 1");
        }
    }
}
