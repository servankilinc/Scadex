using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <summary>
    /// Kamera çekimleri CameraManagement modülüne geçti (2026-10-29). <b>Elle yazıldı:</b> EF modelden çıkan iki entity için
    /// <c>DropTable</c> üretirdi — tablolar burada SİLİNMEZ, yalnızca çekirdeğe bağlayan FK'leri düşer. Tabloları veriyle birlikte
    /// <c>cameramanagement</c> şemasına modülün ilk migration'ı taşır (<c>CameraManagementDbContext</c>, <c>ALTER SCHEMA … TRANSFER</c>);
    /// bu yüzden sıra: önce bu (çekirdek), sonra modül. Index'ler tabloyla birlikte taşınır, modülün modelinde de vardır.
    /// </summary>
    public partial class MoveCameraCapturesToCameraManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Modülde çekirdek referansları FK değildir (CameraId = cihaz Id'si, RequestedByUserId = kullanıcı).
            migrationBuilder.DropForeignKey(
                name: "FK_CameraCapture_Camera_CameraId",
                table: "CameraCapture");

            migrationBuilder.DropForeignKey(
                name: "FK_CameraCapture_User_RequestedByUserId",
                table: "CameraCapture");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Modülün migration'ı geri alınıp tablolar dbo'ya dönmüş olmalı (modül Down'u). Dönmediyse FK'ler bulunamayan tabloya eklenemez.
            migrationBuilder.AddForeignKey(
                name: "FK_CameraCapture_Camera_CameraId",
                table: "CameraCapture",
                column: "CameraId",
                principalTable: "Camera",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CameraCapture_User_RequestedByUserId",
                table: "CameraCapture",
                column: "RequestedByUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
