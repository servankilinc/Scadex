using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scadex.Model.Entities;
using Scadex.Model.Enums;
using Scadex.Model.ProjectEntities;
using Dir = Scadex.Model.Enums.EntityEnums.PinDirection;
using Fn = Scadex.Model.Enums.EntityEnums.PinFunction;
using Side = Scadex.Model.Enums.EntityEnums.HandleSide;
using Volt = Scadex.Model.Enums.EntityEnums.VoltageLevel;

namespace Scadex.DataAccess.Contexts;

public class AppDbContext : IdentityDbContext<User, Role, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<Cabinet> Cabinets { get; set; }
    public override DbSet<User> Users { get; set; }
    public override DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<DeviceCommand> DeviceCommands { get; set; }
    public DbSet<Connection> Connections { get; set; }
    public DbSet<IoChannel> IoChannels { get; set; }
    public DbSet<Pin> Pins { get; set; }
    public DbSet<CanvasSettings> CanvasSettings { get; set; }
    public DbSet<ComponentTemplate> ComponentTemplates { get; set; }
    public DbSet<ComponentTemplatePin> ComponentTemplatePins { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<DiagramAnnotation> DiagramAnnotations { get; set; }
    public DbSet<DeviceStatus> DeviceStatuses { get; set; }
    public DbSet<DeviceType> DeviceTypes { get; set; }
    public DbSet<Camera> Cameras { get; set; }
    public DbSet<CameraCapture> CameraCaptures { get; set; }
    public DbSet<ChannelEvent> ChannelEvents { get; set; }
    public DbSet<MediaGatewaySetting> MediaGatewaySettings { get; set; }
    public DbSet<CameraCaptureSetting> CameraCaptureSettings { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<Archive> Archives { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(c =>
        {
            c.ToTable("Company");
            c.HasKey(c => c.Id);
            c.HasMany(c => c.Cabinets).WithOne(c => c.Company).HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
            c.HasMany(c => c.Users).WithOne(u => u.Company).HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Cabinet>(c =>
        {
            c.ToTable("Cabinet");
            c.HasKey(c => c.Id);
            c.HasMany(c => c.Devices).WithOne(d => d.Cabinet).HasForeignKey(d => d.CabinetId).OnDelete(DeleteBehavior.Restrict);
            c.HasMany(c => c.DiagramAnnotations).WithOne(d => d.Cabinet).HasForeignKey(d => d.CabinetId).OnDelete(DeleteBehavior.Restrict);
            c.HasMany(c => c.Connections).WithOne(c => c.Cabinet).HasForeignKey(c => c.CabinetId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Aynı isimde aktif iki kabin olamaz; pasif kabin olabilir.
            c.HasIndex(c => new { c.CompanyId, c.Name }).IsUnique().HasFilter("[IsActive] = 1");
        });
        modelBuilder.Entity<User>(u =>
        {
            u.ToTable("User");
            u.HasKey(u => u.Id);
            u.HasMany(u => u.DeviceCommands).WithOne(d => d.RequesterUser).HasForeignKey(d => d.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            u.HasMany(u => u.RefreshTokens).WithOne(r => r.User).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

            u.Property(u => u.IdentityCardId).HasMaxLength(64);
            // Restriction: Bir kart en fazla bir aktif kullanıcıya ait olabilir; pasif kullanıcının kartı serbesttir (devredilebilir).
            u.HasIndex(u => u.IdentityCardId).IsUnique().HasFilter("[IdentityCardId] IS NOT NULL AND [IsActive] = 1");
        });
        modelBuilder.Entity<Role>(r =>
        {
            r.ToTable("Role");
            r.HasKey(r => r.Id);
            r.HasMany(r => r.RolePermissions).WithOne(r => r.Role).HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RolePermission>(r =>
        {
            r.ToTable("RolePermission");
            r.HasKey(r => new { r.RoleId, r.PermissionId });
        });
        modelBuilder.Entity<Permission>(p =>
        {
            p.ToTable("Permission");
            p.HasKey(p => p.Id);
            // Id'ler Permission enum degerlerine sabitlenmistir
            p.Property(p => p.Id).ValueGeneratedNever();
            p.HasMany(p => p.RolePermissions).WithOne(r => r.Permission).HasForeignKey(r => r.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DeviceCommand>(d =>
        {
            d.ToTable("DeviceCommand");
            d.HasKey(d => d.Id);
            d.HasQueryFilter(f => !f.IsDeleted);
        });
        modelBuilder.Entity<Connection>(c =>
        {
            c.ToTable("Connection", t =>
            {
                // Restriction: Db tarafindan saglanacak kisit: ayni pin kendisine baglanamaz.
                t.HasCheckConstraint("CK_Connection_DistinctPins", "[SourcePinId] <> [TargetPinId]");
            });
            c.HasKey(c => c.Id);
            c.HasIndex(c => c.CabinetId);

            // Restriction: Silinmemiş iki tane pin bağlantısı en fazla bir tane olabilir.
            c.HasIndex(c => new { c.SourcePinId, c.TargetPinId }).IsUnique().HasFilter("[IsDeleted] = 0");

            c.HasQueryFilter(f => !f.IsDeleted);
        });
        modelBuilder.Entity<IoChannel>(i =>
        {
            i.ToTable("IoChannel");
            i.HasKey(i => i.Id);

            i.HasMany(i => i.Pins).WithOne(p => p.IoChannel).HasForeignKey(p => p.IoChannelId).OnDelete(DeleteBehavior.Restrict);
            i.HasMany(i => i.DeviceCommands).WithOne(d => d.IoChannel).HasForeignKey(d => d.IoChannelId).OnDelete(DeleteBehavior.Restrict);
            i.HasOne(i => i.Cabinet).WithMany().HasForeignKey(i => i.CabinetId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Bir kabinde ayni kanal numarasi en fazla bir tane olabilir; pasif kanallar serbesttir.
            i.HasIndex(i => new { i.CabinetId, i.Direction, i.ChannelNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

            i.HasQueryFilter(f => !f.IsDeleted);
        });
        modelBuilder.Entity<Pin>(p =>
        {
            p.ToTable("Pin", t =>
            {
                // Restriction: RelativeX/Y birimi: sablonun Width/Height'inin 0..1 normalize kesri. 
                t.HasCheckConstraint("CK_Pin_RelativeX", "[RelativeX] >= 0.0 AND [RelativeX] <= 1.0");
                t.HasCheckConstraint("CK_Pin_RelativeY", "[RelativeY] >= 0.0 AND [RelativeY] <= 1.0");
            });
            p.HasKey(p => p.Id);
            p.HasMany(p => p.SourcePinConnections).WithOne(c => c.SourcePin).HasForeignKey(c => c.SourcePinId).OnDelete(DeleteBehavior.Restrict);
            p.HasMany(p => p.TargetPinConnections).WithOne(c => c.TargetPin).HasForeignKey(c => c.TargetPinId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Ayni cihazda ayni isimde pin en fazla bir tane olabilir; pasif pinler serbesttir.
            p.HasIndex(p => new { p.DeviceId, p.Name }).IsUnique().HasFilter("[IsDeleted] = 0");

            p.HasQueryFilter(f => !f.IsDeleted);
        });
        modelBuilder.Entity<CanvasSettings>(c =>
        {
            c.ToTable("CanvasSettings");
            c.HasKey(c => c.Id);
            c.HasOne(c => c.Cabinet).WithOne(c => c.CanvasSettings).HasForeignKey<CanvasSettings>(c => c.CabinetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ComponentTemplate>(c =>
        {
            c.ToTable("ComponentTemplate");
            c.HasKey(c => c.Id);

            c.Property(c => c.BackgroundColor).HasMaxLength(32).IsRequired();

            c.HasMany(c => c.ComponentTemplatePins).WithOne(c => c.ComponentTemplate).HasForeignKey(c => c.ComponentTemplateId).OnDelete(DeleteBehavior.Cascade);
            c.HasMany(c => c.Devices).WithOne(d => d.ComponentTemplate).HasForeignKey(d => d.ComponentTemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ComponentTemplatePin>(c =>
        {
            c.ToTable("ComponentTemplatePin", t =>
            {
                // Restriction: RelativeX/Y birimi: sablonun Width/Height'inin 0..1 normalize kesri. 
                t.HasCheckConstraint("CK_ComponentTemplatePin_RelativeX", "[RelativeX] >= 0.0 AND [RelativeX] <= 1.0");
                t.HasCheckConstraint("CK_ComponentTemplatePin_RelativeY", "[RelativeY] >= 0.0 AND [RelativeY] <= 1.0");
            });
            c.HasKey(c => c.Id);
            c.HasMany(c => c.Pins).WithOne(p => p.ComponentTemplatePin).HasForeignKey(p => p.ComponentTemplatePinId).OnDelete(DeleteBehavior.Restrict);

            c.HasIndex(c => new { c.ComponentTemplateId, c.Name }).IsUnique();
        });
        modelBuilder.Entity<Device>(d =>
        {
            d.ToTable("Device");
            d.HasKey(d => d.Id);
            d.HasMany(d => d.IoChannels).WithOne(i => i.Device).HasForeignKey(i => i.DeviceId).OnDelete(DeleteBehavior.Restrict);
            d.HasMany(d => d.Pins).WithOne(p => p.Device).HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Restrict);
            d.HasMany(d => d.DeviceCommands).WithOne(d => d.Device).HasForeignKey(d => d.DeviceId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Ayni kabinde ayni ExternalCode en fazla bir tane olabilir; pasif cihazlar serbesttir.
            d.HasIndex(d => new { d.CabinetId, d.ExternalCode }).IsUnique().HasFilter("[ExternalCode] IS NOT NULL AND [IsActive] = 1");

            // Restriction: Bir MAC adresi en fazla bir aktif cihaza ait olabilir; pasif cihazlar serbesttir.
            d.HasIndex(d => d.MacAddress).IsUnique().HasFilter("[MacAddress] IS NOT NULL AND [IsActive] = 1");
        });
        modelBuilder.Entity<DiagramAnnotation>(d =>
        {
            d.ToTable("DiagramAnnotation");
            d.HasKey(d => d.Id);
        });
        modelBuilder.Entity<Camera>(c =>
        {
            c.ToTable("Camera");
            c.HasKey(c => c.Id);
            c.HasOne(c => c.Cabinet).WithMany(c => c.Cameras).HasForeignKey(c => c.CabinetId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(c => c.DeviceStatus).WithMany(d => d.Cameras).HasForeignKey(c => c.DeviceStatusId).OnDelete(DeleteBehavior.Restrict);
            c.HasMany(c => c.Captures).WithOne(p => p.Camera).HasForeignKey(p => p.CameraId).OnDelete(DeleteBehavior.Restrict);

            // Restriction: Ayni kabinde ayni IP adresi en fazla bir tane olabilir; pasif kameralar serbesttir.
            c.HasIndex(c => new { c.CabinetId, c.IpAddress }).IsUnique().HasFilter("[IsActive] = 1");

            // Restriction: Ayni kabinde ayni isim en fazla bir tane olabilir; pasif kameralar serbesttir.
            c.HasIndex(c => new { c.CabinetId, c.Name }).IsUnique().HasFilter("[IsActive] = 1");
        });
        modelBuilder.Entity<CameraCapture>(p =>
        {
            p.ToTable("CameraCapture");
            p.HasKey(p => p.Id);
            p.HasOne(p => p.RequestedByUser).WithMany().HasForeignKey(p => p.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            p.HasIndex(p => new { p.CameraId, p.CapturedAtUtc });
        });
        modelBuilder.Entity<ChannelEvent>(e =>
        {
            e.ToTable("ChannelEvent");
            e.HasKey(e => e.Id);
            e.HasOne(e => e.IoChannel).WithMany(i => i.ChannelEvents).HasForeignKey(e => e.IoChannelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(e => e.Cabinet).WithMany(c => c.ChannelEvents).HasForeignKey(e => e.CabinetId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(e => new { e.CabinetId, e.OccurredAtUtc });
            e.HasIndex(e => new { e.IoChannelId, e.OccurredAtUtc });
        });
        modelBuilder.Entity<DeviceStatus>(d =>
        {
            d.ToTable("DeviceStatus");
            d.HasKey(d => d.Id);
            // Id'ler DeviceStatus enum degerlerine sabitlenmistir
            d.Property(d => d.Id).ValueGeneratedNever();
            d.HasMany(d => d.Cabinets).WithOne(c => c.DeviceStatus).HasForeignKey(c => c.DeviceStatusId).OnDelete(DeleteBehavior.Restrict);
            d.HasMany(d => d.Devices).WithOne(d => d.DeviceStatus).HasForeignKey(d => d.DeviceStatusId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DeviceType>(d =>
        {
            d.ToTable("DeviceType");
            d.HasKey(d => d.Id);
            // Id'ler DeviceType enum degerlerine sabitlenmistir
            d.Property(d => d.Id).ValueGeneratedNever();
            d.HasMany(d => d.ComponentTemplates).WithOne(c => c.DeviceType).HasForeignKey(c => c.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RefreshToken>(r =>
        {
            r.HasKey(r => r.Id);
            r.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Log>(l =>
        {
            l.ToTable("ProjectLogs");
            l.HasKey(l => l.Id);
        });
        modelBuilder.Entity<Archive>(a =>
        {
            a.ToTable("ProjectArchives");
            a.HasKey(a => a.Id);
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims");
        });
        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins");
        });
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });
        modelBuilder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UserRoles");
        });
        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        SeedData(modelBuilder);
    }


    private static void SeedData(ModelBuilder modelBuilder)
    {
        #region Company
        modelBuilder.Entity<Company>().HasData(
            new Company
            {
                Id = new Guid("1a86b7a5-b6ed-436b-b4ce-13eec3a57a0b"),
                Name = "System",
                Description = "",
                IsActive = true,
            }
        );
        #endregion

        #region DEVICE STATUS
        // Renk ve ikon frontend'in rozet/durum gostergesini cizebilmesi icindir.
        modelBuilder.Entity<DeviceStatus>().HasData(
            new DeviceStatus
            {
                Id = (int)EntityEnums.DeviceStatus.Offline,
                Name = nameof(EntityEnums.DeviceStatus.Offline),
                Color = "#6B7280",
                Icon = "wifi-off",
                Description = "Cihaza ulasilamiyor."
            },
            new DeviceStatus
            {
                Id = (int)EntityEnums.DeviceStatus.Online,
                Name = nameof(EntityEnums.DeviceStatus.Online),
                Color = "#22C55E",
                Icon = "wifi",
                Description = "Cihaz calisiyor ve haberlesiyor."
            },
            new DeviceStatus
            {
                Id = (int)EntityEnums.DeviceStatus.Warning,
                Name = nameof(EntityEnums.DeviceStatus.Warning),
                Color = "#F59E0B",
                Icon = "alert-triangle",
                Description = "Cihaz calisiyor ancak dikkat gerektiren bir durum var."
            },
            new DeviceStatus
            {
                Id = (int)EntityEnums.DeviceStatus.Critical,
                Name = nameof(EntityEnums.DeviceStatus.Critical),
                Color = "#EF4444",
                Icon = "alert-octagon",
                Description = "Kritik ariza; mudahale gerekiyor."
            },
            new DeviceStatus
            {
                Id = (int)EntityEnums.DeviceStatus.Maintenance,
                Name = nameof(EntityEnums.DeviceStatus.Maintenance),
                Color = "#3B82F6",
                Icon = "wrench",
                Description = "Bakim modunda; alarmlari bastirilir."
            }
        );
        #endregion

        #region DEVICE TYPE
        // Category, Toolbox'ta cihazlarin hangi grup altinda listelenecegini belirler.
        modelBuilder.Entity<DeviceType>().HasData(
            new DeviceType { Id = (int)EntityEnums.DeviceType.ControlModule, Name = nameof(EntityEnums.DeviceType.ControlModule), Category = "Module" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.InputModule, Name = nameof(EntityEnums.DeviceType.InputModule), Category = "Module" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.OutputModule, Name = nameof(EntityEnums.DeviceType.OutputModule), Category = "Module" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.LedModule, Name = nameof(EntityEnums.DeviceType.LedModule), Category = "Module" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.TerminalBlock, Name = nameof(EntityEnums.DeviceType.TerminalBlock), Category = "Passive" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.Sensor, Name = nameof(EntityEnums.DeviceType.Sensor), Category = "Field" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.Peripheral, Name = nameof(EntityEnums.DeviceType.Peripheral), Category = "Field" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.PowerSupply, Name = nameof(EntityEnums.DeviceType.PowerSupply), Category = "Power" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.MeasurementDevice, Name = nameof(EntityEnums.DeviceType.MeasurementDevice), Category = "Measurement" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.CardReader, Name = nameof(EntityEnums.DeviceType.CardReader), Category = "Field" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.Mains, Name = nameof(EntityEnums.DeviceType.Mains), Category = "Power" },
            new DeviceType { Id = (int)EntityEnums.DeviceType.CircuitBreaker, Name = nameof(EntityEnums.DeviceType.CircuitBreaker), Category = "Power" }
        );
        #endregion

        #region Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasData(new Role
            {
                Id = new Guid("b370875e-34cd-4b79-891c-93ae38f99d11"),
                Name = "User",
                NormalizedName = "USER",
                ConcurrencyStamp = new Guid("b370875e-34cd-4b79-891c-93ae38f99d11").ToString(),
                IsImmutable = true,
                IsActive = true
            },
            new Role
            {
                Id = new Guid("cd6040ef-dacc-4678-9a85-154f12581cff"),
                Name = "Manager",
                NormalizedName = "MANAGER",
                ConcurrencyStamp = new Guid("cd6040ef-dacc-4678-9a85-154f12581cff").ToString(),
                IsImmutable = true,
                IsActive = true
            },
            new Role
            {
                Id = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da").ToString(),
                IsImmutable = true,
                IsActive = true
            },
            new Role
            {
                Id = new Guid("1f20c152-530e-4064-a39c-bbbed341fe84"),
                Name = "Owner",
                NormalizedName = "OWNER",
                ConcurrencyStamp = new Guid("1f20c152-530e-4064-a39c-bbbed341fe84").ToString(),
                IsImmutable = true,
                IsActive = true
            });
        });
        #endregion

        #region PERMISSION
        modelBuilder.Entity<Permission>().HasData(
            new Permission
            {
                Id = (int)EntityEnums.Permission.ViewDiagram,
                Code = nameof(EntityEnums.Permission.ViewDiagram),
                DisplayName = "Diyagrami goruntule",
                Category = "Diagram"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.EditDiagram,
                Code = nameof(EntityEnums.Permission.EditDiagram),
                DisplayName = "Diyagrami duzenle",
                Category = "Diagram"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ControlOutput,
                Code = nameof(EntityEnums.Permission.ControlOutput),
                DisplayName = "Cikis sur (role / kilit / siren)",
                Category = "Control"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.AcknowledgeAlarm,
                Code = nameof(EntityEnums.Permission.AcknowledgeAlarm),
                DisplayName = "Alarm kabul et",
                Category = "Alarm"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ManageUsers,
                Code = nameof(EntityEnums.Permission.ManageUsers),
                DisplayName = "Kullanici yonet",
                Category = "Admin"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ConfigureSystem,
                Code = nameof(EntityEnums.Permission.ConfigureSystem),
                DisplayName = "Sistem ayarlarini yapilandir",
                Category = "Admin"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ViewCamera,
                Code = nameof(EntityEnums.Permission.ViewCamera),
                DisplayName = "Kamera goruntule",
                Category = "Diagram"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ExportData,
                Code = nameof(EntityEnums.Permission.ExportData),
                DisplayName = "Veri disari aktar",
                Category = "Data"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ManageWorkflow,
                Code = nameof(EntityEnums.Permission.ManageWorkflow),
                DisplayName = "Is akisi yonet",
                Category = "Admin"
            },
            new Permission
            {
                Id = (int)EntityEnums.Permission.ManageAccessCards,
                Code = nameof(EntityEnums.Permission.ManageAccessCards),
                DisplayName = "Gecis kartlarini yonet",
                Category = "Access"
            }
        );
        #endregion

        #region AYARLAR (tip basina TEK SATIR)
        // Degerler Business.Settings icindeki siniflarin varsayilanlariyla AYNI olmali:
        // satir yoksa saglayici appsettings.json'a duser, satir varsa buradan okunur.
        // Ikisi ayrisirsa "ayni kurulum farkli davraniyor" hatasi cikar.
        modelBuilder.Entity<MediaGatewaySetting>().HasData(new MediaGatewaySetting
        {
            Id = MediaGatewaySetting.SingleRowId,
            ApiTimeoutMs = 30000,
            ApiBaseUrl = "http://127.0.0.1:9997",
            WebRtcPublicBaseUrl = "http://127.0.0.1:8889",
            TokenTtlSeconds = 60,
            SourceOnDemandCloseAfter = "10s",
            RtspTransport = "tcp",
            RecordRoot = "C:\\Scadex\\mediamtx-records"
        });

        modelBuilder.Entity<CameraCaptureSetting>().HasData(new CameraCaptureSetting
        {
            Id = CameraCaptureSetting.SingleRowId,
            SnapshotTimeoutMs = 5000,
            SnapshotCacheSeconds = 3,
            CaptureRoot = "uploads/captures",
            CaptureRetentionDays = 30,
            MaxClipDurationSec = 600,
            ClipFinalizeGraceMs = 3000
        });
        #endregion

        #region RolePermision
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ViewDiagram
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.EditDiagram
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ControlOutput
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.AcknowledgeAlarm
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ManageUsers
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ConfigureSystem
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ViewCamera
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ExportData
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ManageWorkflow
            },
            new RolePermission
            {
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"),
                PermissionId = (int)EntityEnums.Permission.ManageAccessCards
            }
        );
        #endregion

        #region ADMIN USER
        // DIKKAT: Buradaki tum degerler SABIT olmak zorundadir.
        // PasswordHasher her cagrida rastgele salt uretir; hash'i burada hesaplarsaniz
        // her derlemede degisir, EF model degismis sanar ve sonsuz migration uretir.
        // Bu yuzden hash bir kez uretilip literal olarak yapistirilmistir.
        // Parola: Admin!2345  -- ILK GIRISTEN SONRA DEGISTIRIN.
        // Yeni hash uretmek icin: new PasswordHasher<User>().HashPassword(null!, "<parola>")
        //
        // Normalized* alanlari UPPERCASE olmalidir: UserManager.FindByNameAsync ve
        // FindByEmailAsync aramayi bu kolonlar uzerinden yapar. Bos birakilirsa
        // kullanici veritabaninda durur ama hicbir zaman giris yapamaz.
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = new Guid("3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56"),
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                Email = "admin@Scadex.local",
                NormalizedEmail = "ADMIN@Scadex.LOCAL",
                EmailConfirmed = true,
                PasswordHash = "AQAAAAIAAYagAAAAEPl0XbKqwLMRDvmoUpWzRIoURp+GWrBerfyKXrgX5OM9WFYLNUGb+GEKCmo6Fqfl/w==",
                SecurityStamp = "5NDWQZ7JHFXK3MTPRV2Y6BCA4EGSU8LO",
                ConcurrencyStamp = "3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56",
                PhoneNumberConfirmed = false,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
                AccessFailedCount = 0,
                FullName = "System Administrator",
                CompanyId = new Guid("1a86b7a5-b6ed-436b-b4ce-13eec3a57a0b"),
                IsActive = true
            }
        );

        // Kullaniciyi Admin rolune bagla. Identity'nin ara tablosu bir entity degil,
        // bu yuzden IdentityUserRole<Guid> uzerinden seed edilir.
        modelBuilder.Entity<IdentityUserRole<Guid>>().HasData(
            new IdentityUserRole<Guid>
            {
                UserId = new Guid("3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56"),
                RoleId = new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") // Admin
            }
        );
        #endregion

        SeedStarterTemplates(modelBuilder);
    }

    #region STARTER COMPONENT TEMPLATES
    // Palet bos acilmasin diye sistem sablonlari. Kaynak Docs/Example_Scada_Diagram.pdf'teki gercek
    // pano: Gora kontrol / giris / cikis / LED modulleri, koruma, klemens, guc kaynagi ve PDF'te
    // yalnizca etiket olarak gecen saha cihazlari. Baglanti seed edilmez.
    //
    // GORSEL = PIN SEMASI. Kutuyu BackgroundImageUrl tamamen kaplar ve pin adlari cizilmez
    // (yalnizca tooltip), bu yuzden:
    //   - Width/Height orani gorselin oraniyla BIREBIR aynidir. Oran bozulursa gorsel esner ama
    //     0..1 kesir olarak saklanan pinler esnemez ve klemensten kayar.
    //   - Pin konumu gorseldeki klemens noktasinin merkezidir: Gora PNG'lerinde olculmustur,
    //     SVG'lerde data-pin noktalariyla ayni sabittir. Gorseli degistiren seed'i de degistirir.
    // Gorseller Scadex.WebAPI/wwwroot/templates/system/ altindadir (UseStaticFiles servis eder).
    //
    // KANALLAR. IoChannel adresi (Direction, ChannelNumber) kabin genelinde tekildir ve UI'da yeniden
    // numaralandirilamaz; ayni kabine konacak sablonlarin araliklari cakisirsa kayit 400 doner.
    //   - Cikis uzayi duz ve ortaktir: role 1-16, LED 17-24 (LED n -> 16 + n).
    //   - Kontrol modulunun kart ustu IN1-2 / OUT1-2 uclari KANALSIZDIR: PDF'teki gibi giris ve
    //     cikis modulleriyle ayni kabinde 1-2 numaralari cakisirdi.
    //   - Analog: A1 sicaklik, A2 nem (referans proje eslemesi, scada_communication_guide.md
    //     bolum 5), A3 / A4 akim olcme.
    //   - Ayni adresi paylasan pinler tek kanaldir ve kanal adi OrderBy(Name) ile ILK pinin adidir.
    //     Bu yuzden grubun ana ucu ciplak adi alir (COM -> "OUT1", anot -> "LD1"), digerleri sonek.
    //
    // Id'ler DETERMINISTIK uretilir (DeviceType + sablon sirasi + pin sirasi). Rastgele Guid.NewGuid()
    // kullanilsaydi her derlemede degisir, EF model degismis sanar ve sonsuz migration uretirdi --
    // admin kullanici seed'indeki parola hash'i notuyla ayni sebep. Pin sirasi Id'nin parcasidir:
    // mevcut sablona YALNIZCA SONA pin ekleyin, yeni sablon icin yeni ordinal kullanin.

    /// <summary>
    /// Bir sablon pininin tanimi. X/Y gorselin genislik/yuksekligine gore 0..1 kesirdir;
    /// SVG sablonlarinda viewBox birimi verilir ve kesre <c>SvgTemplate</c> cevirir.
    /// </summary>
    private readonly record struct PinSpec(
        string Name,
        double X,
        double Y,
        Side Side,
        Fn Function,
        Dir Direction,
        Volt? Voltage = null,
        int? Channel = null);

    private static Guid SeedTemplateId(EntityEnums.DeviceType type, int ordinal)
        => new($"7e200000-0000-0000-{(int)type:D4}-{ordinal:D12}");

    private static Guid SeedPinId(EntityEnums.DeviceType type, int ordinal, int sequence)
        => new($"7e300000-0000-{(int)type:D4}-{ordinal:D4}-{sequence:D12}");

    /// <summary>
    /// Kanal numarali bir rolenin uc klemensi (cikis kanali n). Kart ust sirada NO-COM-NC, alt sirada
    /// NC-COM-NO dizilir; <paramref name="noFirst"/> soldaki ucun NO olup olmadigini soyler.
    /// </summary>
    private static IEnumerable<PinSpec> Relay(int n, double y, Side side, bool noFirst, double left, double middle, double right)
    {
        yield return new PinSpec($"OUT{n}", middle, y, side, Fn.COM, Dir.Output, null, n);
        yield return new PinSpec($"OUT{n} NO", noFirst ? left : right, y, side, Fn.NO, Dir.Output, null, n);
        yield return new PinSpec($"OUT{n} NC", noFirst ? right : left, y, side, Fn.NC, Dir.Output, null, n);
    }

    /// <summary>Soldan saga dizili dijital girisler: ilk noktanin numarasi <paramref name="first"/>, sonrakiler <paramref name="step"/> kadar ilerler.</summary>
    private static IEnumerable<PinSpec> Inputs(int first, int step, double y, Side side, params double[] xs)
        => xs.Select((x, i) => new PinSpec($"IN{first + i * step}", x, y, side, Fn.Signal_In, Dir.Input, Volt.DC_12V, first + i * step));

    /// <summary>LED n'in iki ucu; ikisi de cikis uzayindaki 16 + n kanalindadir.</summary>
    private static IEnumerable<PinSpec> Led(int n, double plusX, double minusX, double y, Side side)
    {
        yield return new PinSpec($"LD{n}", plusX, y, side, Fn.LED_Anode, Dir.Output, Volt.DC_12V, 16 + n);
        yield return new PinSpec($"LD{n}-", minusX, y, side, Fn.LED_Cathode, Dir.Output, Volt.DC_12V, 16 + n);
    }

    /// <summary>
    /// Sistem sablonlarinin varsayilan zemin rengi (#RRGGBB).
    ///
    /// Tip basina AYRI renk: hepsi ayni tonda oldugunda ne palet karti ne de
    /// canvas'taki kutu birbirinden ayirt edilebiliyordu — bir gucu kaynagini
    /// bir giris kartindan ayirmak icin adini okumak gerekiyordu.
    ///
    /// Tonlar acik secilir; kutu etiketi <c>readableTextColor()</c> ile
    /// hesaplandigi icin koyu renkler de calisir, ama acik zemin uzerinde pin
    /// isimleri ve durum rozetleri daha okunur kaliyor.
    /// </summary>
    private static string TypeColor(EntityEnums.DeviceType type) => type switch
    {
        EntityEnums.DeviceType.ControlModule => "#DBEAFE",      // mavi
        EntityEnums.DeviceType.InputModule => "#DCFCE7",        // yesil
        EntityEnums.DeviceType.OutputModule => "#FEE2E2",       // kirmizi
        EntityEnums.DeviceType.LedModule => "#FEF9C3",          // sari
        EntityEnums.DeviceType.TerminalBlock => "#E2E8F0",      // gri
        EntityEnums.DeviceType.Sensor => "#E0E7FF",             // indigo
        EntityEnums.DeviceType.Peripheral => "#F3E8FF",         // mor
        EntityEnums.DeviceType.PowerSupply => "#FFEDD5",        // turuncu
        EntityEnums.DeviceType.MeasurementDevice => "#CCFBF1",  // turkuaz
        EntityEnums.DeviceType.CardReader => "#FCE7F3",         // pembe
        EntityEnums.DeviceType.Mains => "#FECACA",              // koyu kirmizi
        EntityEnums.DeviceType.CircuitBreaker => "#FED7AA",     // koyu turuncu
        _ => "#F1F5F9"
    };

    private static void SeedStarterTemplates(ModelBuilder modelBuilder)
    {
        var templates = new List<ComponentTemplate>();
        var pins = new List<ComponentTemplatePin>();

        void Template(EntityEnums.DeviceType type, int ordinal, string name, double width, double height, string imageFile, PinSpec[] specs)
        {
            var templateId = SeedTemplateId(type, ordinal);
            templates.Add(new ComponentTemplate
            {
                Id = templateId,
                Name = name,
                DeviceTypeId = (int)type,
                IsSystemTemplate = true,
                Width = width,
                Height = height,
                BackgroundColor = TypeColor(type),
                BackgroundImageUrl = $"/templates/system/{imageFile}",
                IsActive = true
            });

            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                pins.Add(new ComponentTemplatePin
                {
                    Id = SeedPinId(type, ordinal, i + 1),
                    ComponentTemplateId = templateId,
                    Name = spec.Name,
                    Side = spec.Side,
                    RelativeX = spec.X,
                    RelativeY = spec.Y,
                    Function = spec.Function,
                    Direction = spec.Direction,
                    VoltageLevel = spec.Voltage,
                    ChannelNumber = spec.Channel
                });
            }
        }

        // SVG cizimlerinde pin koordinati viewBox birimindedir ve viewBox = Width x Height.
        void SvgTemplate(EntityEnums.DeviceType type, int ordinal, string name, double width, double height, string imageFile, PinSpec[] specs)
            => Template(type, ordinal, name, width, height, imageFile,
                [.. specs.Select(s => s with { X = s.X / width, Y = s.Y / height })]);

        // ---- GORA MODULLERI (Docs/assets/components PNG'leri) ----
        // Koordinatlar PNG'deki klemens noktalarinin merkezidir. Olcek k gorsel pikselini tuval birimine
        // cevirir ve klemens noktasi ~14 birim olacak sekilde secilmistir: Width x Height = piksel x k.

        // control-module.png 468x361, k = 0.65. Kart ustu I/O kanalsizdir (bolge basindaki not).
        Template(EntityEnums.DeviceType.ControlModule, 1, "Gora Kontrol Modülü", 304.2, 234.65, "control-module.png",
        [
            new("RJ45", .1623, .1854, Side.Top, Fn.RJ45, Dir.Bidirectional, Volt.Data),
            new("IN1-", .7544, .0946, Side.Top, Fn.GND, Dir.Input, Volt.DC_12V),
            new("IN1+", .7990, .0946, Side.Top, Fn.Signal_In, Dir.Input, Volt.DC_12V),
            new("IN2-", .8796, .0946, Side.Top, Fn.GND, Dir.Input, Volt.DC_12V),
            new("IN2+", .9237, .0946, Side.Top, Fn.Signal_In, Dir.Input, Volt.DC_12V),
            new("12VDC+", .0908, .8667, Side.Bottom, Fn.VCC, Dir.Input, Volt.DC_12V),
            new("12VDC-", .1705, .8668, Side.Bottom, Fn.GND, Dir.Input, Volt.DC_12V),
            new("RS485 B", .2660, .8668, Side.Bottom, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            new("RS485 A", .3452, .8668, Side.Bottom, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("OUT1 NC", .5181, .8652, Side.Bottom, Fn.NC, Dir.Output),
            new("OUT1 COM", .5972, .8652, Side.Bottom, Fn.COM, Dir.Output),
            new("OUT1 NO", .6763, .8652, Side.Bottom, Fn.NO, Dir.Output),
            new("OUT2 NC", .7553, .8652, Side.Bottom, Fn.NC, Dir.Output),
            new("OUT2 COM", .8345, .8652, Side.Bottom, Fn.COM, Dir.Output),
            new("OUT2 NO", .9136, .8652, Side.Bottom, Fn.NO, Dir.Output)
        ]);

        // output-module.png 1302x415, k = 0.55. Role n -> cikis kanali n.
        const double outTop = .1046, outBottom = .8816;
        Template(EntityEnums.DeviceType.OutputModule, 1, "Gora Çıkış Modülü (15 Röle)", 716.1, 228.25, "output-module.png",
        [
            new("RS485-1 A", .1110, outTop, Side.Top, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("RS485-1 B", .1455, outTop, Side.Top, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            new("RS485-2 A", .1790, outTop, Side.Top, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("RS485-2 B", .2135, outTop, Side.Top, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            // Ust sira soldan saga OUT15..OUT9: NO-COM-NC.
            .. Relay(15, outTop, Side.Top, noFirst: true, .2746, .3084, .3423),
            .. Relay(14, outTop, Side.Top, noFirst: true, .3755, .4093, .4431),
            .. Relay(13, outTop, Side.Top, noFirst: true, .4750, .5088, .5426),
            .. Relay(12, outTop, Side.Top, noFirst: true, .5744, .6083, .6421),
            .. Relay(11, outTop, Side.Top, noFirst: true, .6739, .7078, .7416),
            .. Relay(10, outTop, Side.Top, noFirst: true, .7734, .8072, .8411),
            .. Relay(9, outTop, Side.Top, noFirst: true, .8729, .9067, .9405),
            new("12VDC-", .0436, outBottom, Side.Bottom, Fn.GND, Dir.Input, Volt.DC_12V),
            new("12VDC+", .0774, outBottom, Side.Bottom, Fn.VCC, Dir.Input, Volt.DC_12V),
            // Alt sira soldan saga OUT1..OUT8: NC-COM-NO.
            .. Relay(1, outBottom, Side.Bottom, noFirst: false, .1732, .2070, .2408),
            .. Relay(2, outBottom, Side.Bottom, noFirst: false, .2746, .3084, .3423),
            .. Relay(3, outBottom, Side.Bottom, noFirst: false, .3754, .4092, .4431),
            .. Relay(4, outBottom, Side.Bottom, noFirst: false, .4750, .5088, .5426),
            .. Relay(5, outBottom, Side.Bottom, noFirst: false, .5744, .6082, .6421),
            .. Relay(6, outBottom, Side.Bottom, noFirst: false, .6739, .7077, .7416),
            .. Relay(7, outBottom, Side.Bottom, noFirst: false, .7734, .8072, .8411),
            .. Relay(8, outBottom, Side.Bottom, noFirst: false, .8729, .9067, .9405)
        ]);

        // input-module.png 1280x408, k = 0.55. IN n -> giris kanali n; analog kanallar bolge basinda.
        // Etiketsiz 4 uclu sensor klemensinin sirasi (+V, GND, sicaklik, nem) VARSAYIMDIR.
        // Akim olcme: kartta soldaki cift "IN 2", sagdaki "IN 1" yazar; ciftin uclari T1 / T1' kuralindadir.
        const double inTop = .1191, inBottom = .8810;
        Template(EntityEnums.DeviceType.InputModule, 1, "Gora Giriş Modülü (24 DI + 4 AI)", 704, 224.4, "input-module.png",
        [
            new("RS485-1 A", .1108, inTop, Side.Top, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("RS485-1 B", .1453, inTop, Side.Top, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            new("RS485-2 A", .1926, inTop, Side.Top, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("RS485-2 B", .2262, inTop, Side.Top, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            new("SENSOR +V", .2806, inTop, Side.Top, Fn.VCC, Dir.Output, Volt.DC_12V),
            new("SENSOR GND", .3144, inTop, Side.Top, Fn.GND, Dir.Output, Volt.DC_12V),
            new("SICAKLIK", .3482, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 1),
            new("NEM", .3822, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 2),
            new("AKIM2", .4497, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 4),
            new("AKIM2'", .4835, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 4),
            new("AKIM1", .5174, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 3),
            new("AKIM1'", .5513, inTop, Side.Top, Fn.Analog_In, Dir.AnalogInput, null, 3),
            new("+V1", .6392, inTop, Side.Top, Fn.VCC, Dir.Output, Volt.DC_12V),
            new("+V2", .6730, inTop, Side.Top, Fn.VCC, Dir.Output, Volt.DC_12V),
            // Ust sira soldan saga IN24..IN17.
            .. Inputs(24, -1, inTop, Side.Top, .7206, .7544, .7882, .8221, .8559, .8898, .9237, .9575),
            new("12VDC-", .0434, inBottom, Side.Bottom, Fn.GND, Dir.Input, Volt.DC_12V),
            new("12VDC+", .0772, inBottom, Side.Bottom, Fn.VCC, Dir.Input, Volt.DC_12V),
            new("+V3", .2334, inBottom, Side.Bottom, Fn.VCC, Dir.Output, Volt.DC_12V),
            new("+V4", .2674, inBottom, Side.Bottom, Fn.VCC, Dir.Output, Volt.DC_12V),
            .. Inputs(1, 1, inBottom, Side.Bottom, .3143, .3480, .3818, .4158, .4496, .4834, .5174, .5512),
            new("+V5", .6398, inBottom, Side.Bottom, Fn.VCC, Dir.Output, Volt.DC_12V),
            new("+V6", .6737, inBottom, Side.Bottom, Fn.VCC, Dir.Output, Volt.DC_12V),
            .. Inputs(9, 1, inBottom, Side.Bottom, .7198, .7536, .7875, .8214, .8552, .8891, .9230, .9568)
        ]);

        // led-module.png 501x393, k = 0.57. LED n -> cikis kanali 16 + n (rolelerle ayni duz uzay).
        Template(EntityEnums.DeviceType.LedModule, 1, "Gora LED Modülü (8 Kanal)", 285.57, 224.01, "led-module.png",
        [
            new("RS485 A", .0817, .1194, Side.Top, Fn.RS485_POS, Dir.Bidirectional, Volt.Data),
            new("RS485 B", .1667, .1194, Side.Top, Fn.RS485_NEG, Dir.Bidirectional, Volt.Data),
            // Ust sira soldan saga LD8..LD5: (-, +).
            .. Led(8, .4160, .3310, .1194, Side.Top),
            .. Led(7, .5821, .4990, .1194, Side.Top),
            .. Led(6, .7481, .6651, .1194, Side.Top),
            .. Led(5, .9141, .8311, .1194, Side.Top),
            new("12VDC-", .0823, .8821, Side.Bottom, Fn.GND, Dir.Input, Volt.DC_12V),
            new("12VDC+", .1660, .8821, Side.Bottom, Fn.VCC, Dir.Input, Volt.DC_12V),
            // Alt sira soldan saga LD1..LD4: (+, -).
            .. Led(1, .3358, .4189, .8803, Side.Bottom),
            .. Led(2, .5018, .5849, .8803, Side.Bottom),
            .. Led(3, .6680, .7509, .8803, Side.Bottom),
            .. Led(4, .8340, .9190, .8803, Side.Bottom)
        ]);

        // ---- PANO DONANIMI VE SAHA CIHAZLARI (2D SVG cizimleri) ----
        // Koordinatlar SVG'deki data-pin noktalarinin merkezidir (viewBox birimi). Saha cihazlarinin
        // gerilimi PDF'te beslendikleri role etiketinden gelir: siren, makbuz yazici, banknot kasasi 24V;
        // kilit, POS, bozuk para kasasi, bilgisayar 12V. Saha cihazinin kanali yoktur; kanal onu suren
        // ya da okuyan modulun ucundadir.
        PinSpec[] DcLoadPins(string plusName, Volt volt, double plusX, double gndX, double y) =>
        [
            new(plusName, plusX, y, Side.Bottom, Fn.VCC, Dir.Input, volt),
            new("GND", gndX, y, Side.Bottom, Fn.GND, Dir.Input, volt)
        ];

        PinSpec[] PsuPins(string plusName, Volt volt) =>
        [
            new("L", 30, 145, Side.Bottom, Fn.Line_L, Dir.Input, Volt.AC_220V),
            new("N", 66, 145, Side.Bottom, Fn.Neutral_N, Dir.Input, Volt.AC_220V),
            new("PE", 102, 145, Side.Bottom, Fn.Earth_PE, Dir.Input, Volt.AC_220V),
            new("GND1", 150, 145, Side.Bottom, Fn.GND, Dir.Output, volt),
            new("GND2", 186, 145, Side.Bottom, Fn.GND, Dir.Output, volt),
            new($"{plusName}1", 222, 145, Side.Bottom, Fn.VCC, Dir.Output, volt),
            new($"{plusName}2", 258, 145, Side.Bottom, Fn.VCC, Dir.Output, volt)
        ];

        SvgTemplate(EntityEnums.DeviceType.TerminalBlock, 1, "Klemens Bloğu (8'li)", 100, 320, "terminal-block-8.svg",
        [
            // T1 <-> T1' ayni klemensin iki yuzudur; PDF'te 12V / 24V dagitim barasi olarak kullanilir.
            .. Enumerable.Range(1, 8).Select(i => new PinSpec($"T{i}", 25, i * 40 - 20, Side.Left, Fn.General, Dir.Bidirectional)),
            .. Enumerable.Range(1, 8).Select(i => new PinSpec($"T{i}'", 75, i * 40 - 20, Side.Right, Fn.General, Dir.Bidirectional))
        ]);

        SvgTemplate(EntityEnums.DeviceType.Sensor, 1, "Kapı Sensörü (Manyetik Kontak)", 200, 120, "door-contact.svg",
        [
            new("COM", 60, 100, Side.Bottom, Fn.COM, Dir.Output),
            new("NO", 100, 100, Side.Bottom, Fn.NO, Dir.Output),
            new("NC", 140, 100, Side.Bottom, Fn.NC, Dir.Output)
        ]);

        SvgTemplate(EntityEnums.DeviceType.Peripheral, 1, "Siren", 140, 170, "siren.svg", DcLoadPins("+24V", Volt.DC_24V, 45, 95, 148));
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 2, "Elektromanyetik Kilit", 220, 120, "maglock.svg", DcLoadPins("+12V", Volt.DC_12V, 90, 130, 100));
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 3, "Makbuz Yazıcı", 160, 170, "receipt-printer.svg", DcLoadPins("+24V", Volt.DC_24V, 55, 105, 148));
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 4, "POS Cihazı", 150, 200, "pos-terminal.svg",
            [.. DcLoadPins("+12V", Volt.DC_12V, 35, 75, 178), new("RJ45", 115, 178, Side.Bottom, Fn.RJ45, Dir.Bidirectional, Volt.Data)]);
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 5, "Bozuk Para Kasası", 160, 170, "coin-acceptor.svg", DcLoadPins("+12V", Volt.DC_12V, 55, 105, 148));
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 6, "Banknot Kasası", 160, 190, "bill-acceptor.svg", DcLoadPins("+24V", Volt.DC_24V, 55, 105, 168));
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 7, "Bilgisayar", 200, 150, "computer.svg",
            [.. DcLoadPins("+12V", Volt.DC_12V, 50, 90, 128), new("RJ45", 150, 128, Side.Bottom, Fn.RJ45, Dir.Bidirectional, Volt.Data)]);
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 8, "Lamba", 140, 170, "lamp.svg",
        [
            new("L", 35, 148, Side.Bottom, Fn.Line_L, Dir.Input, Volt.AC_220V),
            new("N", 70, 148, Side.Bottom, Fn.Neutral_N, Dir.Input, Volt.AC_220V),
            new("PE", 105, 148, Side.Bottom, Fn.Earth_PE, Dir.Input, Volt.AC_220V)
        ]);
        SvgTemplate(EntityEnums.DeviceType.Peripheral, 9, "Yönlendirme LED'i", 110, 140, "guide-led.svg",
        [
            new("LED+", 35, 118, Side.Bottom, Fn.LED_Anode, Dir.Input, Volt.DC_12V),
            new("LED-", 75, 118, Side.Bottom, Fn.LED_Cathode, Dir.Input, Volt.DC_12V)
        ]);

        SvgTemplate(EntityEnums.DeviceType.PowerSupply, 1, "Güç Kaynağı 220VAC / 12VDC", 280, 170, "psu-12v.svg", PsuPins("+12V", Volt.DC_12V));
        SvgTemplate(EntityEnums.DeviceType.PowerSupply, 2, "Güç Kaynağı 220VAC / 24VDC", 280, 170, "psu-24v.svg", PsuPins("+24V", Volt.DC_24V));

        // PDF'te "SEBEKE" kacak akim rolesidir; "220V CIKIS" ve "LAMBA" otomatik sigortadir.
        SvgTemplate(EntityEnums.DeviceType.CircuitBreaker, 1, "Kaçak Akım Rölesi 2P", 100, 200, "rcd-2p.svg",
        [
            new("L-IN", 30, 24, Side.Top, Fn.Line_L, Dir.Input, Volt.AC_220V),
            new("N-IN", 70, 24, Side.Top, Fn.Neutral_N, Dir.Input, Volt.AC_220V),
            new("L-OUT", 30, 176, Side.Bottom, Fn.Line_L, Dir.Output, Volt.AC_220V),
            new("N-OUT", 70, 176, Side.Bottom, Fn.Neutral_N, Dir.Output, Volt.AC_220V)
        ]);
        SvgTemplate(EntityEnums.DeviceType.CircuitBreaker, 2, "Otomatik Sigorta 1P", 60, 200, "mcb-1p.svg",
        [
            new("L-IN", 30, 24, Side.Top, Fn.Line_L, Dir.Input, Volt.AC_220V),
            new("L-OUT", 30, 176, Side.Bottom, Fn.Line_L, Dir.Output, Volt.AC_220V)
        ]);

        modelBuilder.Entity<ComponentTemplate>().HasData(templates);
        modelBuilder.Entity<ComponentTemplatePin>().HasData(pins);
    }
    #endregion
}
