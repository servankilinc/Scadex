using Microsoft.EntityFrameworkCore;
using Scadex.Signalization.Entities;

namespace Scadex.Signalization.Data;

/// <summary>
/// Scadex Core tabloları (Cabinet, IoChannel, User, Camera...) referanslar FK DEGIL
/// </summary>
public class SignalizationDbContext : DbContext
{
    public const string Schema = "signalization";

    public SignalizationDbContext(DbContextOptions<SignalizationDbContext> options) : base(options)
    {
    }

    public DbSet<SignalAuthority> Authorities { get; set; }
    public DbSet<SignalCabinet> Cabinets { get; set; }
    public DbSet<SignalCabinetState> CabinetStates { get; set; }
    public DbSet<SignalOuterDoor> OuterDoors { get; set; }
    public DbSet<SignalInnerDoor> InnerDoors { get; set; }
    public DbSet<SignalInnerDoorState> InnerDoorStates { get; set; }
    public DbSet<OperatorSession> OperatorSessions { get; set; }
    public DbSet<OperatorSessionOperator> OperatorSessionOperators { get; set; }
    public DbSet<OperatorSessionEvent> OperatorSessionEvents { get; set; }
    public DbSet<OperatorSessionCapture> OperatorSessionCaptures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SignalAuthority>(a =>
        {
            a.ToTable("Authority");
            a.HasKey(a => a.Id);
            a.Property(a => a.Id).ValueGeneratedNever();
            a.Property(a => a.Name).HasMaxLength(64).IsRequired();

            // Restriction: Bir rol en fazla bir aktif kuruma baglanabilir; ayni adda iki aktif kurum olamaz.
            a.HasIndex(a => a.RoleId).IsUnique().HasFilter("[IsActive] = 1");
            a.HasIndex(a => a.Name).IsUnique().HasFilter("[IsActive] = 1");
        });

        modelBuilder.Entity<SignalCabinet>(c =>
        {
            c.ToTable("Cabinet");
            c.HasKey(c => c.CabinetId);
            c.Property(c => c.CabinetId).ValueGeneratedNever();
            c.HasMany(c => c.OuterDoors).WithOne(d => d.Cabinet).HasForeignKey(d => d.CabinetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SignalCabinetState>(s =>
        {
            s.ToTable("CabinetState");
            s.HasKey(s => s.CabinetId);
            s.Property(s => s.CabinetId).ValueGeneratedNever();
        });

        modelBuilder.Entity<SignalOuterDoor>(d =>
        {
            d.ToTable("OuterDoor");
            d.HasKey(d => d.Id);
            d.Property(d => d.Id).ValueGeneratedNever();
            d.Property(d => d.Name).HasMaxLength(128).IsRequired();
            d.Property(d => d.SwitchOpenValue).HasMaxLength(16).IsRequired();
            d.HasMany(d => d.InnerDoors).WithOne(i => i.OuterDoor).HasForeignKey(i => i.OuterDoorId).OnDelete(DeleteBehavior.Restrict);

            // Motorun sicak sorgusu: gelen kanal hangi dis kapinin anahtari?
            d.HasIndex(d => d.SwitchIoChannelId);
        });

        modelBuilder.Entity<SignalInnerDoor>(d =>
        {
            d.ToTable("InnerDoor");
            d.HasKey(d => d.Id);
            d.Property(d => d.Id).ValueGeneratedNever();
            d.Property(d => d.Name).HasMaxLength(128).IsRequired();
            d.Property(d => d.SwitchOpenValue).HasMaxLength(16).IsRequired();
            d.HasOne(d => d.Authority).WithMany().HasForeignKey(d => d.AuthorityId).OnDelete(DeleteBehavior.Restrict);
            d.HasOne(d => d.State).WithOne().HasForeignKey<SignalInnerDoorState>(s => s.InnerDoorId).OnDelete(DeleteBehavior.Cascade);

            d.HasIndex(d => d.SwitchIoChannelId);
        });

        modelBuilder.Entity<SignalInnerDoorState>(s =>
        {
            s.ToTable("InnerDoorState");
            s.HasKey(s => s.InnerDoorId);
            s.Property(s => s.InnerDoorId).ValueGeneratedNever();
        });

        modelBuilder.Entity<OperatorSession>(s =>
        {
            s.ToTable("OperatorSession");
            s.HasKey(s => s.Id);
            s.Property(s => s.OuterDoorNameSnapshot).HasMaxLength(128).IsRequired();
            s.Ignore(s => s.HasActiveSirenRequest);
            s.HasMany(s => s.Operators).WithOne(o => o.Session).HasForeignKey(o => o.SessionId).OnDelete(DeleteBehavior.Restrict);
            s.HasMany(s => s.Events).WithOne(e => e.Session).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Restrict);
            s.HasMany(s => s.Captures).WithOne(c => c.Session).HasForeignKey(c => c.SessionId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Bir dis kapinin ayni anda en fazla bir ACIK oturumu olabilir. Yaris durumunda iki olay
            // ayni anda oturum acmaya calisirsa yalnizca biri kazanir (motor kabin bazinda sirali olsa da sema da zorlar).
            s.HasIndex(s => s.OuterDoorId).IsUnique().HasFilter("[EndedAtUtc] IS NULL");

            s.HasIndex(s => new { s.CabinetId, s.StartedAtUtc });
            s.HasIndex(s => s.StartedAtUtc);
        });

        modelBuilder.Entity<OperatorSessionOperator>(o =>
        {
            o.ToTable("OperatorSessionOperator");
            o.HasKey(o => new { o.SessionId, o.UserId });
            o.Property(o => o.FullNameSnapshot).HasMaxLength(256).IsRequired();
            o.Property(o => o.AuthorityNameSnapshot).HasMaxLength(64).IsRequired();
            o.Property(o => o.CardIdRaw).HasMaxLength(64).IsRequired();
            o.HasIndex(o => o.UserId);
        });

        modelBuilder.Entity<OperatorSessionEvent>(e =>
        {
            e.ToTable("OperatorSessionEvent");
            e.HasKey(e => e.Id);
            e.Property(e => e.CardIdRaw).HasMaxLength(64);
            e.Property(e => e.Detail).HasMaxLength(512);
            e.HasIndex(e => new { e.SessionId, e.OccurredAtUtc });
        });

        modelBuilder.Entity<OperatorSessionCapture>(c =>
        {
            c.ToTable("OperatorSessionCapture");
            c.HasKey(c => new { c.SessionId, c.CameraCaptureId });
        });
    }
}
