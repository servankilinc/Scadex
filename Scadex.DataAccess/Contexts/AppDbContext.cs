using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scadex.Model.Entities;
using Scadex.Model.Enums;
using Scadex.Model.ProjectEntities;

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

            // Restriction: Ayni cihazda ayni kanal numarasi en fazla bir tane olabilir; pasif kanallar serbesttir.
            i.HasIndex(i => new { i.DeviceId, i.ChannelNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

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
    // Palet bos acilmasin diye sistem sablonlari. IsSystemTemplate = true olanlar
    // kullanici tarafindan duzenlenmez; kullanici kendi sablonunu yazana kadar
    // diyagram editoru bunlarla calisir.
    //
    // Id'ler DETERMINISTIK uretilir (DeviceType + sira). Rastgele Guid.NewGuid()
    // kullanilsaydi her derlemede degisir, EF model degismis sanar ve sonsuz
    // migration uretirdi -- admin kullanici seed'indeki parola hash'i notuyla ayni sebep.

    /// <summary>Bir sablon pininin konumdan bagimsiz tanimi; RelativeX/Y yerlestirme sirasinda hesaplanir.</summary>
    private readonly record struct PinSpec(
        string Name,
        EntityEnums.HandleSide Side,
        EntityEnums.PinFunction Function,
        EntityEnums.PinDirection Direction,
        EntityEnums.VoltageLevel? Voltage = null,
        int? Channel = null);

    private static Guid SeedTemplateId(int deviceTypeId)
        => new($"7e000000-0000-0000-0000-{deviceTypeId:D12}");

    private static Guid SeedPinId(int deviceTypeId, int sequence)
        => new($"7e100000-0000-0000-{deviceTypeId:D4}-{sequence:D12}");

    /// <summary>Numaralandirilmis pin serisi uretir: IN1..IN8 gibi.</summary>
    private static IEnumerable<PinSpec> Series(
        string prefix, int count, EntityEnums.HandleSide side,
        EntityEnums.PinFunction function, EntityEnums.PinDirection direction,
        EntityEnums.VoltageLevel? voltage = null,
        bool numberChannels = false)
        => Enumerable.Range(1, count).Select(n => new PinSpec(
            $"{prefix}{n}", side, function, direction, voltage,
            numberChannels ? n : null));

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

        void Template(EntityEnums.DeviceType type, string name, double width, double height, params PinSpec[] specs)
        {
            var typeId = (int)type;
            var templateId = SeedTemplateId(typeId);
            templates.Add(new ComponentTemplate
            {
                Id = templateId,
                Name = name,
                DeviceTypeId = typeId,
                IsSystemTemplate = true,
                Width = width,
                Height = height,
                BackgroundColor = TypeColor(type),
                IsActive = true
            });

            // Pinler kenar bazinda esit araliklarla dagitilir: n pin icin i. pinin
            // orani (i + 0.5) / n olur, yani ilk ve son pin kenara yapismaz.
            var sequence = 0;
            foreach (var group in specs.GroupBy(s => s.Side))
            {
                var sidePins = group.ToList();
                for (var i = 0; i < sidePins.Count; i++)
                {
                    var spec = sidePins[i];
                    var offset = (i + 0.5d) / sidePins.Count;
                    pins.Add(new ComponentTemplatePin
                    {
                        Id = SeedPinId(typeId, ++sequence),
                        ComponentTemplateId = templateId,
                        Name = spec.Name,
                        Side = spec.Side,
                        RelativeX = spec.Side switch
                        {
                            EntityEnums.HandleSide.Left => 0d,
                            EntityEnums.HandleSide.Right => 1d,
                            _ => offset
                        },
                        RelativeY = spec.Side switch
                        {
                            EntityEnums.HandleSide.Top => 0d,
                            EntityEnums.HandleSide.Bottom => 1d,
                            _ => offset
                        },
                        Function = spec.Function,
                        Direction = spec.Direction,
                        VoltageLevel = spec.Voltage,
                        ChannelNumber = spec.Channel
                    });
                }
            }
        }

        Template(EntityEnums.DeviceType.ControlModule, "Kontrol Modulu", 220, 170,
            new PinSpec("RJ45", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.RJ45, EntityEnums.PinDirection.Bidirectional, EntityEnums.VoltageLevel.Data),
            new PinSpec("RS485-A", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.RS485_POS, EntityEnums.PinDirection.Bidirectional, EntityEnums.VoltageLevel.Data),
            new PinSpec("RS485-B", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.RS485_NEG, EntityEnums.PinDirection.Bidirectional, EntityEnums.VoltageLevel.Data),
            new PinSpec("+12V", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
            new PinSpec("GND", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V));

        Template(EntityEnums.DeviceType.InputModule, "8 Kanal Giris Karti", 200, 260,
            [.. Series("IN", 8, EntityEnums.HandleSide.Left, EntityEnums.PinFunction.Signal_In, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.Signal_5V, numberChannels: true),
             new PinSpec("+12V", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
             new PinSpec("GND", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V)]);

        Template(EntityEnums.DeviceType.OutputModule, "8 Kanal Role Cikis Karti", 200, 260,
            [new PinSpec("+12V", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
             new PinSpec("GND", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
             .. Series("OUT", 8, EntityEnums.HandleSide.Right, EntityEnums.PinFunction.NO, EntityEnums.PinDirection.Output, null, numberChannels: true)]);

        Template(EntityEnums.DeviceType.LedModule, "8 Kanal LED Karti", 180, 240,
            [new PinSpec("+12V", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
             new PinSpec("GND", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
             .. Series("LD", 8, EntityEnums.HandleSide.Right, EntityEnums.PinFunction.LED_Anode, EntityEnums.PinDirection.Output, null, numberChannels: true)]);

        Template(EntityEnums.DeviceType.TerminalBlock, "Klemens Blogu", 140, 200,
            [.. Series("T", 6, EntityEnums.HandleSide.Left, EntityEnums.PinFunction.General, EntityEnums.PinDirection.Bidirectional),
             // Karsi taraf: T1 <-> T1' ayni klemensin iki yuzudur.
             .. Series("T", 6, EntityEnums.HandleSide.Right, EntityEnums.PinFunction.General, EntityEnums.PinDirection.Bidirectional)
                .Select(p => p with { Name = p.Name + "'" })]);

        Template(EntityEnums.DeviceType.PowerSupply, "Guc Kaynagi 220AC / 12DC", 190, 140,
            new PinSpec("L", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.Line_L, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("N", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.Neutral_N, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("PE", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.Earth_PE, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("+12V", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.DC_12V),
            new PinSpec("GND", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.DC_12V));

        Template(EntityEnums.DeviceType.Mains, "Sebeke Girisi", 150, 120,
            new PinSpec("L", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.Line_L, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("N", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.Neutral_N, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("PE", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.Earth_PE, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.AC_220V));

        Template(EntityEnums.DeviceType.CircuitBreaker, "Sigorta / Devre Kesici", 130, 90,
            new PinSpec("IN", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.General, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.AC_220V),
            new PinSpec("OUT", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.General, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.AC_220V));

        Template(EntityEnums.DeviceType.Sensor, "Sensor (3 Telli)", 140, 110,
            new PinSpec("+12V", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.VCC, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
            new PinSpec("GND", EntityEnums.HandleSide.Left, EntityEnums.PinFunction.GND, EntityEnums.PinDirection.Input, EntityEnums.VoltageLevel.DC_12V),
            new PinSpec("SIG", EntityEnums.HandleSide.Right, EntityEnums.PinFunction.Signal_Out, EntityEnums.PinDirection.Output, EntityEnums.VoltageLevel.Signal_5V));

        modelBuilder.Entity<ComponentTemplate>().HasData(templates);
        modelBuilder.Entity<ComponentTemplatePin>().HasData(pins);
    }
    #endregion
}
