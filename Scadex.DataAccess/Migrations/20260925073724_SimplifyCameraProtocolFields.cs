using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <summary>
    /// Kameranin marka ozelindeki alanlari (port, kanal, akim bayraklari) ICameraProtocolProfile'a tasindi;
    /// serbest metin Manufacturer yerini CameraBrand enum'una (int) birakti.
    /// </summary>
    /// <remarks>
    /// ELLE DUZENLENDI: EF, SubStreamChannel'i (int) Brand olarak yeniden adlandirmayi onerdi — mevcut kayitlarin markasi 102 olurdu.
    /// Brand ayri eklenir ve varsayilani 1'dir (Hikvision): bugune kadar her kayit fiilen Hikvision profiline cozuluyordu
    /// (tek profil + eslesmeyen uretici icin sessiz geri dusus), dolayisiyla davranis korunur.
    /// </remarks>
    public partial class SimplifyCameraProtocolFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Brand",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "RtspPort",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "HttpPort",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "HttpsPort",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "MainStreamChannel",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "SubStreamChannel",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "MainStreamEnabled",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "SubStreamEnabled",
                table: "Camera");

            migrationBuilder.DropColumn(
                name: "SnapshotChannel",
                table: "Camera");
        }

        /// <inheritdoc />
        /// <remarks> Kolonlar eski Hikvision varsayilanlariyla geri gelir. </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Camera",
                type: "nvarchar(max)",
                nullable: true);

            // Nullable kolonda DEFAULT mevcut satirlari doldurmaz; acikca yazilir.
            migrationBuilder.Sql("UPDATE [Camera] SET [Manufacturer] = N'Hikvision'");

            migrationBuilder.AddColumn<int>(
                name: "RtspPort",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 554);

            migrationBuilder.AddColumn<int>(
                name: "HttpPort",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "HttpsPort",
                table: "Camera",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MainStreamChannel",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 101);

            migrationBuilder.AddColumn<int>(
                name: "SubStreamChannel",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 102);

            migrationBuilder.AddColumn<bool>(
                name: "MainStreamEnabled",
                table: "Camera",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubStreamEnabled",
                table: "Camera",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotChannel",
                table: "Camera",
                type: "int",
                nullable: false,
                defaultValue: 101);

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Camera");
        }
    }
}
