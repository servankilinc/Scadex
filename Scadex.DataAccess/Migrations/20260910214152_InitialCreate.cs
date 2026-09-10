using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Scadex.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraCaptureSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    SnapshotCacheSeconds = table.Column<int>(type: "int", nullable: false),
                    CaptureRoot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaptureRetentionDays = table.Column<int>(type: "int", nullable: false),
                    MaxClipDurationSec = table.Column<int>(type: "int", nullable: false),
                    ClipFinalizeGraceMs = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraCaptureSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaGatewaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WebRtcPublicBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenTtlSeconds = table.Column<int>(type: "int", nullable: false),
                    SourceOnDemandCloseAfter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RtspTransport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordRoot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaGatewaySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequesterId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectArchives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequesterId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsImmutable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cabinet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    LocationDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GsmIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NetworkIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceStatusId = table.Column<int>(type: "int", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScadaBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScadaIsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ScadaCommandTimeoutMs = table.Column<int>(type: "int", nullable: false),
                    ScadaLastIngestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cabinet_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cabinet_DeviceStatus_DeviceStatusId",
                        column: x => x.DeviceStatusId,
                        principalTable: "DeviceStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComponentTemplate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceTypeId = table.Column<int>(type: "int", nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "bit", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BackgroundImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentTemplate_DeviceType_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpirationUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TTL = table.Column<int>(type: "int", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Camera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RtspPort = table.Column<int>(type: "int", nullable: false),
                    HttpPort = table.Column<int>(type: "int", nullable: false),
                    HttpsPort = table.Column<int>(type: "int", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainStreamChannel = table.Column<int>(type: "int", nullable: false),
                    SubStreamChannel = table.Column<int>(type: "int", nullable: false),
                    MainStreamEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SubStreamEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotChannel = table.Column<int>(type: "int", nullable: false),
                    MonitoringPort = table.Column<int>(type: "int", nullable: true),
                    DeviceStatusId = table.Column<int>(type: "int", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PingIntervalSec = table.Column<int>(type: "int", nullable: false),
                    IsMonitoringEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastConnectionError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Camera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Camera_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Camera_DeviceStatus_DeviceStatusId",
                        column: x => x.DeviceStatusId,
                        principalTable: "DeviceStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanvasSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GridSize = table.Column<int>(type: "int", nullable: false),
                    SnapToGrid = table.Column<bool>(type: "bit", nullable: false),
                    BackgroundVariant = table.Column<int>(type: "int", nullable: false),
                    GridColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinZoom = table.Column<double>(type: "float", nullable: false),
                    MaxZoom = table.Column<double>(type: "float", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasSettings_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagramAnnotation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoordinateX = table.Column<double>(type: "float", nullable: false),
                    CoordinateY = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Rotation = table.Column<double>(type: "float", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Shape = table.Column<int>(type: "int", nullable: false),
                    FontColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FontSize = table.Column<double>(type: "float", nullable: false),
                    IsBold = table.Column<bool>(type: "bit", nullable: false),
                    BorderColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagramAnnotation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagramAnnotation_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComponentTemplatePin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChannelNumber = table.Column<int>(type: "int", nullable: true),
                    Function = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    VoltageLevel = table.Column<int>(type: "int", nullable: true),
                    RelativeX = table.Column<double>(type: "float", nullable: false),
                    RelativeY = table.Column<double>(type: "float", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentTemplatePin", x => x.Id);
                    table.CheckConstraint("CK_ComponentTemplatePin_RelativeX", "[RelativeX] >= 0.0 AND [RelativeX] <= 1.0");
                    table.CheckConstraint("CK_ComponentTemplatePin_RelativeY", "[RelativeY] >= 0.0 AND [RelativeY] <= 1.0");
                    table.ForeignKey(
                        name: "FK_ComponentTemplatePin_ComponentTemplate_ComponentTemplateId",
                        column: x => x.ComponentTemplateId,
                        principalTable: "ComponentTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Device",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceStatusId = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExternalCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoordinateX = table.Column<double>(type: "float", nullable: false),
                    CoordinateY = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: true),
                    Height = table.Column<double>(type: "float", nullable: true),
                    Rotation = table.Column<double>(type: "float", nullable: false),
                    ZIndex = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Device", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Device_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Device_ComponentTemplate_ComponentTemplateId",
                        column: x => x.ComponentTemplateId,
                        principalTable: "ComponentTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Device_DeviceStatus_DeviceStatusId",
                        column: x => x.DeviceStatusId,
                        principalTable: "DeviceStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CameraCapture",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSec = table.Column<int>(type: "int", nullable: true),
                    RelativePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraCapture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraCapture_Camera_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Camera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CameraCapture_User_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IoChannel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelNumber = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValueUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoChannel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoChannel_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IoChannel_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChannelEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelEvent_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChannelEvent_IoChannel_IoChannelId",
                        column: x => x.IoChannelId,
                        principalTable: "IoChannel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCommand",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommandType = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCommand_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceCommand_IoChannel_IoChannelId",
                        column: x => x.IoChannelId,
                        principalTable: "IoChannel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceCommand_User_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentTemplatePinId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IoChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChannelNumber = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Function = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    VoltageLevel = table.Column<int>(type: "int", nullable: true),
                    RelativeX = table.Column<double>(type: "float", nullable: false),
                    RelativeY = table.Column<double>(type: "float", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pin", x => x.Id);
                    table.CheckConstraint("CK_Pin_RelativeX", "[RelativeX] >= 0.0 AND [RelativeX] <= 1.0");
                    table.CheckConstraint("CK_Pin_RelativeY", "[RelativeY] >= 0.0 AND [RelativeY] <= 1.0");
                    table.ForeignKey(
                        name: "FK_Pin_ComponentTemplatePin_ComponentTemplatePinId",
                        column: x => x.ComponentTemplatePinId,
                        principalTable: "ComponentTemplatePin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pin_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pin_IoChannel_IoChannelId",
                        column: x => x.IoChannelId,
                        principalTable: "IoChannel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CabinetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WireType = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LineStyle = table.Column<int>(type: "int", nullable: false),
                    StrokeWidth = table.Column<double>(type: "float", nullable: false),
                    Routing = table.Column<int>(type: "int", nullable: false),
                    WaypointsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connection", x => x.Id);
                    table.CheckConstraint("CK_Connection_DistinctPins", "[SourcePinId] <> [TargetPinId]");
                    table.ForeignKey(
                        name: "FK_Connection_Cabinet_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Connection_Pin_SourcePinId",
                        column: x => x.SourcePinId,
                        principalTable: "Pin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Connection_Pin_TargetPinId",
                        column: x => x.TargetPinId,
                        principalTable: "Pin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CameraCaptureSettings",
                columns: new[] { "Id", "CaptureRetentionDays", "CaptureRoot", "ClipFinalizeGraceMs", "CreateDateUtc", "CreatedBy", "MaxClipDurationSec", "SnapshotCacheSeconds", "SnapshotTimeoutMs", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { 1, 30, "uploads/captures", 3000, null, null, 600, 3, 5000, null, null });

            migrationBuilder.InsertData(
                table: "Company",
                columns: new[] { "Id", "CreateDateUtc", "CreatedBy", "Description", "IsActive", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[] { new Guid("1a86b7a5-b6ed-436b-b4ce-13eec3a57a0b"), null, null, "", true, "System", null, null });

            migrationBuilder.InsertData(
                table: "DeviceStatus",
                columns: new[] { "Id", "Color", "CreateDateUtc", "CreatedBy", "Description", "Icon", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { 0, "#6B7280", null, null, "Cihaza ulasilamiyor.", "wifi-off", "Offline", null, null },
                    { 1, "#22C55E", null, null, "Cihaz calisiyor ve haberlesiyor.", "wifi", "Online", null, null },
                    { 2, "#F59E0B", null, null, "Cihaz calisiyor ancak dikkat gerektiren bir durum var.", "alert-triangle", "Warning", null, null },
                    { 3, "#EF4444", null, null, "Kritik ariza; mudahale gerekiyor.", "alert-octagon", "Critical", null, null },
                    { 4, "#3B82F6", null, null, "Bakim modunda; alarmlari bastirilir.", "wrench", "Maintenance", null, null }
                });

            migrationBuilder.InsertData(
                table: "DeviceType",
                columns: new[] { "Id", "Category", "CreateDateUtc", "CreatedBy", "Name", "UpdateDateUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Module", null, null, "ControlModule", null, null },
                    { 2, "Module", null, null, "InputModule", null, null },
                    { 3, "Module", null, null, "OutputModule", null, null },
                    { 4, "Module", null, null, "LedModule", null, null },
                    { 5, "Passive", null, null, "TerminalBlock", null, null },
                    { 6, "Field", null, null, "Sensor", null, null },
                    { 7, "Field", null, null, "Peripheral", null, null },
                    { 8, "Power", null, null, "PowerSupply", null, null },
                    { 9, "Measurement", null, null, "MeasurementDevice", null, null },
                    { 10, "Field", null, null, "CardReader", null, null },
                    { 11, "Power", null, null, "Mains", null, null },
                    { 12, "Power", null, null, "CircuitBreaker", null, null }
                });

            migrationBuilder.InsertData(
                table: "MediaGatewaySettings",
                columns: new[] { "Id", "ApiBaseUrl", "ApiTimeoutMs", "CreateDateUtc", "CreatedBy", "RecordRoot", "RtspTransport", "SourceOnDemandCloseAfter", "TokenTtlSeconds", "UpdateDateUtc", "UpdatedBy", "WebRtcPublicBaseUrl" },
                values: new object[] { 1, "http://127.0.0.1:9997", 30000, null, null, "C:\\Scadex\\mediamtx-records", "tcp", "10s", 60, null, null, "http://127.0.0.1:8889" });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Category", "Code", "CreateDateUtc", "CreatedBy", "DisplayName", "UpdateDateUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { 0, "Diagram", "ViewDiagram", null, null, "Diyagrami goruntule", null, null },
                    { 1, "Diagram", "EditDiagram", null, null, "Diyagrami duzenle", null, null },
                    { 2, "Control", "ControlOutput", null, null, "Cikis sur (role / kilit / siren)", null, null },
                    { 3, "Alarm", "AcknowledgeAlarm", null, null, "Alarm kabul et", null, null },
                    { 4, "Admin", "ManageUsers", null, null, "Kullanici yonet", null, null },
                    { 5, "Admin", "ConfigureSystem", null, null, "Sistem ayarlarini yapilandir", null, null },
                    { 6, "Diagram", "ViewCamera", null, null, "Kamera goruntule", null, null },
                    { 7, "Data", "ExportData", null, null, "Veri disari aktar", null, null },
                    { 8, "Admin", "ManageWorkflow", null, null, "Is akisi yonet", null, null },
                    { 9, "Access", "ManageAccessCards", null, null, "Gecis kartlarini yonet", null, null }
                });

            migrationBuilder.InsertData(
                table: "Role",
                columns: new[] { "Id", "ConcurrencyStamp", "CreateDateUtc", "CreatedBy", "IsActive", "IsImmutable", "Name", "NormalizedName", "UpdateDateUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1f20c152-530e-4064-a39c-bbbed341fe84"), "1f20c152-530e-4064-a39c-bbbed341fe84", null, null, true, true, "Owner", "OWNER", null, null },
                    { new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"), "7138ec51-4f9e-4afd-b61b-5a9a4584f5da", null, null, true, true, "Admin", "ADMIN", null, null },
                    { new Guid("b370875e-34cd-4b79-891c-93ae38f99d11"), "b370875e-34cd-4b79-891c-93ae38f99d11", null, null, true, true, "User", "USER", null, null },
                    { new Guid("cd6040ef-dacc-4678-9a85-154f12581cff"), "cd6040ef-dacc-4678-9a85-154f12581cff", null, null, true, true, "Manager", "MANAGER", null, null }
                });

            migrationBuilder.InsertData(
                table: "ComponentTemplate",
                columns: new[] { "Id", "BackgroundColor", "BackgroundImageUrl", "CreateDateUtc", "CreatedBy", "DeviceTypeId", "Height", "IsActive", "IsSystemTemplate", "Name", "UpdateDateUtc", "UpdatedBy", "Width" },
                values: new object[,]
                {
                    { new Guid("7e000000-0000-0000-0000-000000000001"), "#DBEAFE", null, null, null, 1, 170.0, true, true, "Kontrol Modulu", null, null, 220.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000002"), "#DCFCE7", null, null, null, 2, 260.0, true, true, "8 Kanal Giris Karti", null, null, 200.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000003"), "#FEE2E2", null, null, null, 3, 260.0, true, true, "8 Kanal Role Cikis Karti", null, null, 200.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000004"), "#FEF9C3", null, null, null, 4, 240.0, true, true, "8 Kanal LED Karti", null, null, 180.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000005"), "#E2E8F0", null, null, null, 5, 200.0, true, true, "Klemens Blogu", null, null, 140.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000006"), "#E0E7FF", null, null, null, 6, 110.0, true, true, "Sensor (3 Telli)", null, null, 140.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000008"), "#FFEDD5", null, null, null, 8, 140.0, true, true, "Guc Kaynagi 220AC / 12DC", null, null, 190.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000009"), "#CCFBF1", null, null, null, 9, 200.0, true, true, "4 Kanal Analog Giris Karti", null, null, 200.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000011"), "#FECACA", null, null, null, 11, 120.0, true, true, "Sebeke Girisi", null, null, 150.0 },
                    { new Guid("7e000000-0000-0000-0000-000000000012"), "#FED7AA", null, null, null, 12, 90.0, true, true, "Sigorta / Devre Kesici", null, null, 130.0 }
                });

            migrationBuilder.InsertData(
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 0, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 1, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 2, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 3, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 4, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 5, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 6, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 7, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 8, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") },
                    { 9, new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da") }
                });

            migrationBuilder.InsertData(
                table: "User",
                columns: new[] { "Id", "AccessFailedCount", "CompanyId", "ConcurrencyStamp", "CreateDateUtc", "CreatedBy", "Email", "EmailConfirmed", "FullName", "IsActive", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UpdateDateUtc", "UpdatedBy", "UserName" },
                values: new object[] { new Guid("3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56"), 0, new Guid("1a86b7a5-b6ed-436b-b4ce-13eec3a57a0b"), "3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56", null, null, "admin@Scadex.local", true, "System Administrator", true, true, null, "ADMIN@Scadex.LOCAL", "ADMIN", "AQAAAAIAAYagAAAAEPl0XbKqwLMRDvmoUpWzRIoURp+GWrBerfyKXrgX5OM9WFYLNUGb+GEKCmo6Fqfl/w==", null, false, "5NDWQZ7JHFXK3MTPRV2Y6BCA4EGSU8LO", false, null, null, "admin" });

            migrationBuilder.InsertData(
                table: "ComponentTemplatePin",
                columns: new[] { "Id", "ChannelNumber", "ComponentTemplateId", "CreateDateUtc", "CreatedBy", "Direction", "Function", "Name", "RelativeX", "RelativeY", "Side", "UpdateDateUtc", "UpdatedBy", "VoltageLevel" },
                values: new object[,]
                {
                    { new Guid("7e100000-0000-0000-0001-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000001"), null, null, 2, 7, "RJ45", 0.0, 0.16666666666666666, 0, null, null, 5 },
                    { new Guid("7e100000-0000-0000-0001-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000001"), null, null, 2, 5, "RS485-A", 0.0, 0.5, 0, null, null, 5 },
                    { new Guid("7e100000-0000-0000-0001-000000000003"), null, new Guid("7e000000-0000-0000-0000-000000000001"), null, null, 2, 6, "RS485-B", 0.0, 0.83333333333333337, 0, null, null, 5 },
                    { new Guid("7e100000-0000-0000-0001-000000000004"), null, new Guid("7e000000-0000-0000-0000-000000000001"), null, null, 0, 3, "+12V", 1.0, 0.25, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0001-000000000005"), null, new Guid("7e000000-0000-0000-0000-000000000001"), null, null, 0, 4, "GND", 1.0, 0.75, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0002-000000000001"), 1, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN1", 0.0, 0.0625, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000002"), 2, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN2", 0.0, 0.1875, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000003"), 3, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN3", 0.0, 0.3125, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000004"), 4, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN4", 0.0, 0.4375, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000005"), 5, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN5", 0.0, 0.5625, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000006"), 6, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN6", 0.0, 0.6875, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000007"), 7, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN7", 0.0, 0.8125, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000008"), 8, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 10, "IN8", 0.0, 0.9375, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0002-000000000009"), null, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 3, "+12V", 1.0, 0.25, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0002-000000000010"), null, new Guid("7e000000-0000-0000-0000-000000000002"), null, null, 0, 4, "GND", 1.0, 0.75, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0003-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 0, 3, "+12V", 0.0, 0.25, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0003-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 0, 4, "GND", 0.0, 0.75, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0003-000000000003"), 1, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT1", 1.0, 0.0625, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000004"), 2, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT2", 1.0, 0.1875, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000005"), 3, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT3", 1.0, 0.3125, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000006"), 4, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT4", 1.0, 0.4375, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000007"), 5, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT5", 1.0, 0.5625, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000008"), 6, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT6", 1.0, 0.6875, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000009"), 7, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT7", 1.0, 0.8125, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0003-000000000010"), 8, new Guid("7e000000-0000-0000-0000-000000000003"), null, null, 1, 1, "OUT8", 1.0, 0.9375, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 0, 3, "+12V", 0.0, 0.25, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0004-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 0, 4, "GND", 0.0, 0.75, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0004-000000000003"), 1, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD1", 1.0, 0.0625, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000004"), 2, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD2", 1.0, 0.1875, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000005"), 3, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD3", 1.0, 0.3125, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000006"), 4, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD4", 1.0, 0.4375, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000007"), 5, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD5", 1.0, 0.5625, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000008"), 6, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD6", 1.0, 0.6875, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000009"), 7, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD7", 1.0, 0.8125, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0004-000000000010"), 8, new Guid("7e000000-0000-0000-0000-000000000004"), null, null, 1, 8, "LD8", 1.0, 0.9375, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T1", 0.0, 0.083333333333333329, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T2", 0.0, 0.25, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000003"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T3", 0.0, 0.41666666666666669, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000004"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T4", 0.0, 0.58333333333333337, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000005"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T5", 0.0, 0.75, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000006"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T6", 0.0, 0.91666666666666663, 0, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000007"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T1'", 1.0, 0.083333333333333329, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000008"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T2'", 1.0, 0.25, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000009"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T3'", 1.0, 0.41666666666666669, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000010"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T4'", 1.0, 0.58333333333333337, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000011"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T5'", 1.0, 0.75, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0005-000000000012"), null, new Guid("7e000000-0000-0000-0000-000000000005"), null, null, 2, 99, "T6'", 1.0, 0.91666666666666663, 1, null, null, null },
                    { new Guid("7e100000-0000-0000-0006-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000006"), null, null, 0, 3, "+12V", 0.0, 0.25, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0006-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000006"), null, null, 0, 4, "GND", 0.0, 0.75, 0, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0006-000000000003"), null, new Guid("7e000000-0000-0000-0000-000000000006"), null, null, 1, 11, "SIG", 1.0, 0.5, 1, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0008-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000008"), null, null, 0, 14, "L", 0.0, 0.16666666666666666, 0, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0008-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000008"), null, null, 0, 15, "N", 0.0, 0.5, 0, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0008-000000000003"), null, new Guid("7e000000-0000-0000-0000-000000000008"), null, null, 0, 16, "PE", 0.0, 0.83333333333333337, 0, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0008-000000000004"), null, new Guid("7e000000-0000-0000-0000-000000000008"), null, null, 1, 3, "+12V", 1.0, 0.25, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0008-000000000005"), null, new Guid("7e000000-0000-0000-0000-000000000008"), null, null, 1, 4, "GND", 1.0, 0.75, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0009-000000000001"), 1, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 3, 12, "AI1", 0.0, 0.125, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0009-000000000002"), 2, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 3, 12, "AI2", 0.0, 0.375, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0009-000000000003"), 3, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 3, 12, "AI3", 0.0, 0.625, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0009-000000000004"), 4, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 3, 12, "AI4", 0.0, 0.875, 0, null, null, 4 },
                    { new Guid("7e100000-0000-0000-0009-000000000005"), null, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 0, 3, "+12V", 1.0, 0.25, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0009-000000000006"), null, new Guid("7e000000-0000-0000-0000-000000000009"), null, null, 0, 4, "GND", 1.0, 0.75, 1, null, null, 1 },
                    { new Guid("7e100000-0000-0000-0011-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000011"), null, null, 1, 14, "L", 1.0, 0.16666666666666666, 1, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0011-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000011"), null, null, 1, 15, "N", 1.0, 0.5, 1, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0011-000000000003"), null, new Guid("7e000000-0000-0000-0000-000000000011"), null, null, 1, 16, "PE", 1.0, 0.83333333333333337, 1, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0012-000000000001"), null, new Guid("7e000000-0000-0000-0000-000000000012"), null, null, 0, 99, "IN", 0.0, 0.5, 0, null, null, 3 },
                    { new Guid("7e100000-0000-0000-0012-000000000002"), null, new Guid("7e000000-0000-0000-0000-000000000012"), null, null, 1, 99, "OUT", 1.0, 0.5, 1, null, null, 3 }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { new Guid("7138ec51-4f9e-4afd-b61b-5a9a4584f5da"), new Guid("3f2b8c14-6d5a-4e79-9c03-8a1f7be24d56") });

            migrationBuilder.CreateIndex(
                name: "IX_Cabinet_CompanyId_Name",
                table: "Cabinet",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Cabinet_DeviceStatusId",
                table: "Cabinet",
                column: "DeviceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Camera_CabinetId_IpAddress",
                table: "Camera",
                columns: new[] { "CabinetId", "IpAddress" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Camera_CabinetId_Name",
                table: "Camera",
                columns: new[] { "CabinetId", "Name" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Camera_DeviceStatusId",
                table: "Camera",
                column: "DeviceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_CameraCapture_CameraId_CapturedAtUtc",
                table: "CameraCapture",
                columns: new[] { "CameraId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CameraCapture_RequestedByUserId",
                table: "CameraCapture",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSettings_CabinetId",
                table: "CanvasSettings",
                column: "CabinetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelEvent_CabinetId_OccurredAtUtc",
                table: "ChannelEvent",
                columns: new[] { "CabinetId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelEvent_IoChannelId_OccurredAtUtc",
                table: "ChannelEvent",
                columns: new[] { "IoChannelId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentTemplate_DeviceTypeId",
                table: "ComponentTemplate",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentTemplatePin_ComponentTemplateId_Name",
                table: "ComponentTemplatePin",
                columns: new[] { "ComponentTemplateId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connection_CabinetId",
                table: "Connection",
                column: "CabinetId");

            migrationBuilder.CreateIndex(
                name: "IX_Connection_SourcePinId_TargetPinId",
                table: "Connection",
                columns: new[] { "SourcePinId", "TargetPinId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Connection_TargetPinId",
                table: "Connection",
                column: "TargetPinId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_CabinetId_ExternalCode",
                table: "Device",
                columns: new[] { "CabinetId", "ExternalCode" },
                unique: true,
                filter: "[ExternalCode] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Device_ComponentTemplateId",
                table: "Device",
                column: "ComponentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_DeviceStatusId",
                table: "Device",
                column: "DeviceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_MacAddress",
                table: "Device",
                column: "MacAddress",
                unique: true,
                filter: "[MacAddress] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommand_DeviceId",
                table: "DeviceCommand",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommand_IoChannelId",
                table: "DeviceCommand",
                column: "IoChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommand_RequestedByUserId",
                table: "DeviceCommand",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagramAnnotation_CabinetId",
                table: "DiagramAnnotation",
                column: "CabinetId");

            migrationBuilder.CreateIndex(
                name: "IX_IoChannel_CabinetId_Direction_ChannelNumber",
                table: "IoChannel",
                columns: new[] { "CabinetId", "Direction", "ChannelNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IoChannel_DeviceId",
                table: "IoChannel",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Pin_ComponentTemplatePinId",
                table: "Pin",
                column: "ComponentTemplatePinId");

            migrationBuilder.CreateIndex(
                name: "IX_Pin_DeviceId_Name",
                table: "Pin",
                columns: new[] { "DeviceId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Pin_IoChannelId",
                table: "Pin",
                column: "IoChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Role",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "User",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_User_CompanyId",
                table: "User",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "User",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraCapture");

            migrationBuilder.DropTable(
                name: "CameraCaptureSettings");

            migrationBuilder.DropTable(
                name: "CanvasSettings");

            migrationBuilder.DropTable(
                name: "ChannelEvent");

            migrationBuilder.DropTable(
                name: "Connection");

            migrationBuilder.DropTable(
                name: "DeviceCommand");

            migrationBuilder.DropTable(
                name: "DiagramAnnotation");

            migrationBuilder.DropTable(
                name: "MediaGatewaySettings");

            migrationBuilder.DropTable(
                name: "ProjectArchives");

            migrationBuilder.DropTable(
                name: "ProjectLogs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "RolePermission");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Camera");

            migrationBuilder.DropTable(
                name: "Pin");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "ComponentTemplatePin");

            migrationBuilder.DropTable(
                name: "IoChannel");

            migrationBuilder.DropTable(
                name: "Device");

            migrationBuilder.DropTable(
                name: "Cabinet");

            migrationBuilder.DropTable(
                name: "ComponentTemplate");

            migrationBuilder.DropTable(
                name: "Company");

            migrationBuilder.DropTable(
                name: "DeviceStatus");

            migrationBuilder.DropTable(
                name: "DeviceType");
        }
    }
}
