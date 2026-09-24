using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DeviceMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMonitoringEnabled",
                table: "Device",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastConnectionError",
                table: "Device",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonitoringPort",
                table: "Device",
                type: "int",
                nullable: true);

            // Mevcut satirlar icin 60 sn (DeviceDraft varsayilaniyla ayni). Izleme varsayilan KAPALI
            // oldugu icin bu deger operator izlemeyi acana kadar kullanilmaz; 0 ise validator'in
            // (5..86400) disinda kalir ve cihaz ilk kayitta 400 alirdi.
            migrationBuilder.AddColumn<int>(
                name: "PingIntervalSec",
                table: "Device",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "IsMonitorable",
                table: "ComponentTemplate",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0001-000000000001"),
                column: "IsMonitorable",
                value: true);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0002-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0003-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0004-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0005-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0006-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000002"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000003"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000004"),
                column: "IsMonitorable",
                value: true);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000005"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000006"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000007"),
                column: "IsMonitorable",
                value: true);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000008"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0007-000000000009"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0008-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0008-000000000002"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0012-000000000001"),
                column: "IsMonitorable",
                value: false);

            migrationBuilder.UpdateData(
                table: "ComponentTemplate",
                keyColumn: "Id",
                keyValue: new Guid("7e200000-0000-0000-0012-000000000002"),
                column: "IsMonitorable",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMonitoringEnabled",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "LastConnectionError",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "MonitoringPort",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "PingIntervalSec",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "IsMonitorable",
                table: "ComponentTemplate");
        }
    }
}
