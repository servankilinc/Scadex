using Microsoft.EntityFrameworkCore;
using Scadex.CameraManagement.Model.Entities;

namespace Scadex.CameraManagement.DataAccess;

/// <summary>
/// Modülün kendi şeması (<c>cameramanagement</c>) ve migration geçmişi. Scadex çekirdek referansları (<c>CameraId</c> = cihaz Id'si,
/// <c>RequestedByUserId</c>) FK değildir. Tablolar 2026-10-29'a kadar çekirdekteydi (<c>dbo</c>); modülün ilk migration'ı onları veriyle
/// birlikte bu şemaya TAŞIR (<c>ALTER SCHEMA … TRANSFER</c>), yeniden yaratmaz — kolon tipleri ve index adları bu yüzden çekirdektekiyle aynıdır.
/// </summary>
public class CameraManagementDbContext : DbContext
{
    public const string Schema = "cameramanagement";

    public CameraManagementDbContext(DbContextOptions<CameraManagementDbContext> options) : base(options)
    {
    }

    public DbSet<CameraCapture> CameraCaptures { get; set; }
    public DbSet<CameraCaptureSetting> CameraCaptureSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<CameraCapture>(p =>
        {
            p.ToTable("CameraCapture");
            p.HasKey(p => p.Id);
            p.HasIndex(p => new { p.CameraId, p.CapturedAtUtc });
            // Çekirdekteyken FK'nin index'iydi; tabloyla birlikte taşınır (kimin çektiği sorgusu için de işe yarar).
            p.HasIndex(p => p.RequestedByUserId);
        });

        // Ayar: tip başına TEK SATIR. Değerler Model.Settings.CameraCaptureSettings varsayılanlarıyla AYNI olmalı: satır yoksa servis
        // sınıf varsayılanlarına düşer. Medya geçidi ayarları burada DEĞİL, appsettings.json > MediaGateway'dedir (mediamtx.yml ile elle senkron).
        modelBuilder.Entity<CameraCaptureSetting>(s =>
        {
            s.ToTable("CameraCaptureSettings");
            s.HasKey(s => s.Id);
            s.HasData(new CameraCaptureSetting
            {
                Id = CameraCaptureSetting.SingleRowId,
                SnapshotTimeoutMs = 5000,
                SnapshotCacheSeconds = 3,
                CaptureRoot = "uploads/captures",
                CaptureRetentionDays = 30,
                MaxClipDurationSec = 600,
                ClipFinalizeGraceMs = 3000
            });
        });
    }
}
