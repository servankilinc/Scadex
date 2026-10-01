using Microsoft.EntityFrameworkCore;
using Scadex.RemoteDesk.Model.Entities;

namespace Scadex.RemoteDesk.DataAccess;

/// <summary> Modülün kendi şeması (<c>remotedesk</c>) ve migration geçmişi. Scadex çekirdek referansları (DeviceId, UserId) FK değildir </summary>
public class RemoteDeskDbContext : DbContext
{
    public const string Schema = "remotedesk";

    public RemoteDeskDbContext(DbContextOptions<RemoteDeskDbContext> options) : base(options)
    {
    }

    public DbSet<ScreenSession> ScreenSessions { get; set; }
    public DbSet<ScreenViewLog> ScreenViewLogs { get; set; }
    public DbSet<RemoteControlSession> RemoteControlSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ScreenSession>(s =>
        {
            s.ToTable("ScreenSession");
            s.HasKey(s => s.Id);
            s.Property(s => s.Id).ValueGeneratedNever();
            s.Property(s => s.MediaPath).HasMaxLength(64).IsRequired();
            s.Property(s => s.FailureReason).HasMaxLength(256);
            s.HasMany<ScreenViewLog>().WithOne(v => v.ScreenSession).HasForeignKey(v => v.ScreenSessionId).OnDelete(DeleteBehavior.Restrict);

            // Kural: (PC, monitör) başına en fazla bir AÇIK oturum. Açık = Created..Stopping (1-4). Filtreli index NOT IN kabul etmez.
            s.HasIndex(s => new { s.DeviceId, s.MonitorIndex }).IsUnique().HasFilter("[Status] IN (1, 2, 3, 4)");
            s.HasIndex(s => s.CreatedUtc);
        });

        modelBuilder.Entity<ScreenViewLog>(v =>
        {
            v.ToTable("ScreenViewLog");
            v.HasKey(v => v.Id);

            // Denetim sorguları: bir PC'nin / bir kullanıcının izleme geçmişi.
            v.HasIndex(v => new { v.DeviceId, v.StartedUtc });
            v.HasIndex(v => new { v.UserId, v.StartedUtc });
        });

        modelBuilder.Entity<RemoteControlSession>(c =>
        {
            c.ToTable("RemoteControlSession");
            c.HasKey(c => c.Id);
            c.Property(c => c.Id).ValueGeneratedNever();

            // Denetim sorguları: bir PC'nin / bir kullanıcının kontrol geçmişi.
            c.HasIndex(c => new { c.DeviceId, c.StartedUtc });
            c.HasIndex(c => new { c.UserId, c.StartedUtc });
        });
    }
}
