using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.CameraManagement.Data.Migrations
{
    /// <summary>
    /// <b>Elle yazıldı (2026-10-29):</b> tablolar YARATILMAZ, çekirdekteki <c>dbo</c> şemasından veriyle birlikte TAŞINIR — kayıtlar,
    /// index'ler, ayar satırı ve Signalization'daki oturum↔kare bağları (<c>CameraCaptureId</c>) korunur. Her kurulumda geçerlidir: çekirdeğin
    /// migration'ları tabloları (yeni kurulumda da) <c>dbo</c> altında yaratır ve FK'lerini <c>MoveCameraCapturesToCameraManagement</c> düşürür.
    /// Sıra: önce çekirdek (<c>AppDbContext</c>), sonra bu.
    /// </summary>
    public partial class InitialCameraManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cameramanagement");

            migrationBuilder.Sql("ALTER SCHEMA [cameramanagement] TRANSFER [dbo].[CameraCapture];");
            migrationBuilder.Sql("ALTER SCHEMA [cameramanagement] TRANSFER [dbo].[CameraCaptureSettings];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER SCHEMA [dbo] TRANSFER [cameramanagement].[CameraCapture];");
            migrationBuilder.Sql("ALTER SCHEMA [dbo] TRANSFER [cameramanagement].[CameraCaptureSettings];");
        }
    }
}
