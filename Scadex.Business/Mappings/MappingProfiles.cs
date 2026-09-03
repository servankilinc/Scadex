using AutoMapper;
using Scadex.Model.Auth.SignUp;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.CanvasSettings.Queries;
using Scadex.Model.Dtos.ChannelEvent.Queries;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.Model.Dtos.Company.Queries;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;
using Scadex.Model.Dtos.ComponentTemplatePin.Queries;
using Scadex.Model.Dtos.Connection.Queries;
using Scadex.Model.Dtos.Device.Queries;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Dtos.DeviceStatus.Queries;
using Scadex.Model.Dtos.DeviceType.Queries;
using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using Scadex.Model.Dtos.DiagramAnnotation.Queries;
using Scadex.Model.Dtos.IoChannel.Queries;
using Scadex.Model.Dtos.Permission.Queries;
using Scadex.Model.Dtos.Pin.Queries;
using Scadex.Model.Dtos.Role.Commands;
using Scadex.Model.Dtos.Role.Queries;
using Scadex.Model.Dtos.RolePermission.Commands;
using Scadex.Model.Dtos.RolePermission.Queries;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Model.Dtos.User.Queries;
using Scadex.Model.Entities;

namespace Scadex.Business.Mappings;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        #region Company
        CreateMap<Company, CompanyDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<CompanyCreateDto, Company>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description));

        // ReverseMap KORUNUR: GetUpdateModelAsync -> ProjectTo<CompanyUpdateDto> buna bagli.
        CreateMap<CompanyUpdateDto, Company>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ReverseMap();
        #endregion

        #region Cabinet
        CreateMap<Cabinet, CabinetBaseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.LocationDescription, opt => opt.MapFrom(src => src.LocationDescription));

        CreateMap<Cabinet, CabinetDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.Company != default ? src.Company.Name : default))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.LocationDescription, opt => opt.MapFrom(src => src.LocationDescription))
            .ForMember(dest => dest.GsmIp, opt => opt.MapFrom(src => src.GsmIp))
            .ForMember(dest => dest.NetworkIp, opt => opt.MapFrom(src => src.NetworkIp))
            .ForMember(dest => dest.DeviceStatusId, opt => opt.MapFrom(src => src.DeviceStatusId))
            .ForMember(dest => dest.DeviceStatusName, opt => opt.MapFrom(src => src.DeviceStatus != default ? src.DeviceStatus.Name : default))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<CabinetCreateDto, Cabinet>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.LocationDescription, opt => opt.MapFrom(src => src.LocationDescription))
            .ForMember(dest => dest.GsmIp, opt => opt.MapFrom(src => src.GsmIp))
            .ForMember(dest => dest.NetworkIp, opt => opt.MapFrom(src => src.NetworkIp))
            .ForMember(dest => dest.ScadaBaseUrl, opt => opt.MapFrom(src => src.ScadaBaseUrl))
            .ForMember(dest => dest.ScadaCommandTimeoutMs, opt => opt.MapFrom(src => src.ScadaCommandTimeoutMs))
            .ForMember(dest => dest.ScadaIsEnabled, opt => opt.MapFrom(src => src.ScadaIsEnabled));

        CreateMap<CabinetUpdateDto, Cabinet>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.LocationDescription, opt => opt.MapFrom(src => src.LocationDescription))
            .ForMember(dest => dest.GsmIp, opt => opt.MapFrom(src => src.GsmIp))
            .ForMember(dest => dest.NetworkIp, opt => opt.MapFrom(src => src.NetworkIp))
            .ForMember(dest => dest.ScadaBaseUrl, opt => opt.MapFrom(src => src.ScadaBaseUrl))
            .ForMember(dest => dest.ScadaCommandTimeoutMs, opt => opt.MapFrom(src => src.ScadaCommandTimeoutMs))
            .ForMember(dest => dest.ScadaIsEnabled, opt => opt.MapFrom(src => src.ScadaIsEnabled))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ReverseMap();
        #endregion

        #region User
        CreateMap<SignUpRequest, User>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber));

        CreateMap<User, UserBaseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId));

        CreateMap<User, UserDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.Company != default ? src.Company.Name : default))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<UserCreateDto, User>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber));

        CreateMap<UserUpdateDto, User>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ReverseMap();
        #endregion

        #region Role
        CreateMap<Role, RoleDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<RoleCreateDto, Role>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));

        CreateMap<RoleUpdateDto, Role>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ReverseMap();
        #endregion

        #region RolePermission
        CreateMap<RolePermission, RolePermissionDto>()
            .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
            .ForMember(dest => dest.PermissionId, opt => opt.MapFrom(src => src.PermissionId))
            .ForMember(dest => dest.PermissionCode, opt => opt.MapFrom(src => src.Permission != default ? src.Permission.Code : default))
            .ForMember(dest => dest.PermissionDisplayName, opt => opt.MapFrom(src => src.Permission != default ? src.Permission.DisplayName : default))
            .ForMember(dest => dest.PermissionCategory, opt => opt.MapFrom(src => src.Permission != default ? src.Permission.Category : default));

        CreateMap<RolePermissionCreateDto, RolePermission>()
            .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
            .ForMember(dest => dest.PermissionId, opt => opt.MapFrom(src => src.PermissionId));
        #endregion

        #region Permission
        CreateMap<Permission, PermissionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Code, opt => opt.MapFrom(src => src.Code))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));
        #endregion

        #region DeviceCommand
        CreateMap<DeviceCommand, DeviceCommandDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.DeviceId))
            .ForMember(dest => dest.DeviceName, opt => opt.MapFrom(src => src.Device != default ? src.Device.Name : default))
            .ForMember(dest => dest.IoChannelId, opt => opt.MapFrom(src => src.IoChannelId))
            .ForMember(dest => dest.CommandType, opt => opt.MapFrom(src => src.CommandType))
            .ForMember(dest => dest.PayloadJson, opt => opt.MapFrom(src => src.PayloadJson))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.RequestedByUserId, opt => opt.MapFrom(src => src.RequestedByUserId))
            .ForMember(dest => dest.UserFullName, opt => opt.MapFrom(src => src.RequesterUser != default ? src.RequesterUser.FullName : default))
            .ForMember(dest => dest.SentAt, opt => opt.MapFrom(src => src.SentAt))
            .ForMember(dest => dest.RespondedAt, opt => opt.MapFrom(src => src.RespondedAt))
            .ForMember(dest => dest.ResultMessage, opt => opt.MapFrom(src => src.ResultMessage))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.DeletedDateUtc, opt => opt.MapFrom(src => src.DeletedDateUtc));

        CreateMap<DeviceCommandCreateDto, DeviceCommand>()
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.DeviceId))
            .ForMember(dest => dest.IoChannelId, opt => opt.MapFrom(src => src.IoChannelId))
            .ForMember(dest => dest.CommandType, opt => opt.MapFrom(src => src.CommandType))
            .ForMember(dest => dest.PayloadJson, opt => opt.MapFrom(src => src.PayloadJson))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.RequestedByUserId, opt => opt.MapFrom(src => src.RequestedByUserId))
            .ForMember(dest => dest.SentAt, opt => opt.MapFrom(src => src.SentAt))
            .ForMember(dest => dest.RespondedAt, opt => opt.MapFrom(src => src.RespondedAt))
            .ForMember(dest => dest.ResultMessage, opt => opt.MapFrom(src => src.ResultMessage));

        CreateMap<DeviceCommandUpdateDto, DeviceCommand>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.PayloadJson, opt => opt.MapFrom(src => src.PayloadJson))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.SentAt, opt => opt.MapFrom(src => src.SentAt))
            .ForMember(dest => dest.RespondedAt, opt => opt.MapFrom(src => src.RespondedAt))
            .ForMember(dest => dest.ResultMessage, opt => opt.MapFrom(src => src.ResultMessage))
            .ReverseMap();
        #endregion

        #region Connection
        CreateMap<Connection, ConnectionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.SourcePinId, opt => opt.MapFrom(src => src.SourcePinId))
            .ForMember(dest => dest.TargetPinId, opt => opt.MapFrom(src => src.TargetPinId))
            .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Label))
            .ForMember(dest => dest.WireType, opt => opt.MapFrom(src => src.WireType))
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Color))
            .ForMember(dest => dest.LineStyle, opt => opt.MapFrom(src => src.LineStyle))
            .ForMember(dest => dest.StrokeWidth, opt => opt.MapFrom(src => src.StrokeWidth))
            .ForMember(dest => dest.WaypointsJson, opt => opt.MapFrom(src => src.WaypointsJson))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.DeletedDateUtc, opt => opt.MapFrom(src => src.DeletedDateUtc));
        #endregion

        #region IoChannel
        CreateMap<IoChannel, IoChannelDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.DeviceId))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.ChannelNumber))
            .ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction))
            .ForMember(dest => dest.IsEnabled, opt => opt.MapFrom(src => src.IsEnabled))
            .ForMember(dest => dest.CurrentValue, opt => opt.MapFrom(src => src.CurrentValue))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.ValueUpdatedAt, opt => opt.MapFrom(src => src.ValueUpdatedAt))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.DeletedDateUtc, opt => opt.MapFrom(src => src.DeletedDateUtc));
        #endregion

        #region Pin
        CreateMap<Pin, PinDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.RelativeX, opt => opt.MapFrom(src => src.RelativeX))
            .ForMember(dest => dest.RelativeY, opt => opt.MapFrom(src => src.RelativeY))
            .ForMember(dest => dest.IoChannelId, opt => opt.MapFrom(src => src.IoChannelId))
            .ForMember(dest => dest.IoChannelName, opt => opt.MapFrom(src => src.IoChannel != default ? src.IoChannel.Name : default))
            .ForMember(dest => dest.Function, opt => opt.MapFrom(src => src.Function))
            .ForMember(dest => dest.VoltageLevel, opt => opt.MapFrom(src => src.VoltageLevel))
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.DeviceId))
            .ForMember(dest => dest.DeviceName, opt => opt.MapFrom(src => src.Device != default ? src.Device.Name : default))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.DeletedDateUtc, opt => opt.MapFrom(src => src.DeletedDateUtc));

        CreateMap<Pin, PinDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.RelativeX, opt => opt.MapFrom(src => src.RelativeX))
            .ForMember(dest => dest.RelativeY, opt => opt.MapFrom(src => src.RelativeY))
            .ForMember(dest => dest.Function, opt => opt.MapFrom(src => src.Function))
            .ForMember(dest => dest.VoltageLevel, opt => opt.MapFrom(src => src.VoltageLevel))
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.DeviceId));
        #endregion

        #region CanvasSettings
        CreateMap<CanvasSettings, CanvasSettingsDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.GridSize, opt => opt.MapFrom(src => src.GridSize))
            .ForMember(dest => dest.SnapToGrid, opt => opt.MapFrom(src => src.SnapToGrid))
            .ForMember(dest => dest.BackgroundVariant, opt => opt.MapFrom(src => src.BackgroundVariant))
            .ForMember(dest => dest.GridColor, opt => opt.MapFrom(src => src.GridColor))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.MinZoom, opt => opt.MapFrom(src => src.MinZoom))
            .ForMember(dest => dest.MaxZoom, opt => opt.MapFrom(src => src.MaxZoom))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));
        #endregion

        #region ComponentTemplate
        CreateMap<ComponentTemplate, ComponentTemplateBaseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.DeviceTypeId, opt => opt.MapFrom(src => src.DeviceTypeId))
            .ForMember(dest => dest.IsSystemTemplate, opt => opt.MapFrom(src => src.IsSystemTemplate))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.BackgroundImageUrl, opt => opt.MapFrom(src => src.BackgroundImageUrl));

        CreateMap<ComponentTemplate, ComponentTemplateDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.DeviceTypeId, opt => opt.MapFrom(src => src.DeviceTypeId))
            .ForMember(dest => dest.IsSystemTemplate, opt => opt.MapFrom(src => src.IsSystemTemplate))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.BackgroundImageUrl, opt => opt.MapFrom(src => src.BackgroundImageUrl))
            .ForMember(dest => dest.DeviceTypeName, opt => opt.MapFrom(src => src.DeviceType != default ? src.DeviceType.Name : default))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<ComponentTemplateCreateRequest, ComponentTemplate>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.DeviceTypeId, opt => opt.MapFrom(src => src.DeviceTypeId))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.BackgroundImageUrl, opt => opt.MapFrom(src => src.BackgroundImageUrl));
        #endregion

        #region ComponentTemplatePin
        CreateMap<ComponentTemplatePin, ComponentTemplatePinDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.ComponentTemplateId, opt => opt.MapFrom(src => src.ComponentTemplateId))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.RelativeX, opt => opt.MapFrom(src => src.RelativeX))
            .ForMember(dest => dest.RelativeY, opt => opt.MapFrom(src => src.RelativeY))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.ChannelNumber))
            .ForMember(dest => dest.Function, opt => opt.MapFrom(src => src.Function))
            .ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction))
            .ForMember(dest => dest.VoltageLevel, opt => opt.MapFrom(src => src.VoltageLevel))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));

        CreateMap<TemplatePinDraft, ComponentTemplatePin>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.RelativeX, opt => opt.MapFrom(src => src.RelativeX))
            .ForMember(dest => dest.RelativeY, opt => opt.MapFrom(src => src.RelativeY))
            .ForMember(dest => dest.Side, opt => opt.MapFrom(src => src.Side))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.ChannelNumber))
            .ForMember(dest => dest.Function, opt => opt.MapFrom(src => src.Function))
            .ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction))
            .ForMember(dest => dest.VoltageLevel, opt => opt.MapFrom(src => src.VoltageLevel));
        #endregion

        #region Device
        CreateMap<Device, DeviceDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.CabinetId, opt => opt.MapFrom(src => src.CabinetId))
            .ForMember(dest => dest.DeviceStatusId, opt => opt.MapFrom(src => src.DeviceStatusId))
            .ForMember(dest => dest.ExternalCode, opt => opt.MapFrom(src => src.ExternalCode))
            .ForMember(dest => dest.LastSeen, opt => opt.MapFrom(src => src.LastSeen));

        CreateMap<Device, DeviceDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CoordinateX, opt => opt.MapFrom(src => src.CoordinateX))
            .ForMember(dest => dest.CoordinateY, opt => opt.MapFrom(src => src.CoordinateY))
            .ForMember(dest => dest.Rotation, opt => opt.MapFrom(src => src.Rotation))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.IsVisible, opt => opt.MapFrom(src => src.IsVisible))
            .ForMember(dest => dest.CabinetId, opt => opt.MapFrom(src => src.CabinetId))
            .ForMember(dest => dest.ComponentTemplateId, opt => opt.MapFrom(src => src.ComponentTemplateId))
            .ForMember(dest => dest.ComponentTemplateName, opt => opt.MapFrom(src => src.ComponentTemplate != default ? src.ComponentTemplate.Name : default))
            .ForMember(dest => dest.DeviceStatusId, opt => opt.MapFrom(src => src.DeviceStatusId))
            .ForMember(dest => dest.DeviceStatusName, opt => opt.MapFrom(src => src.DeviceStatus != default ? src.DeviceStatus.Name : default))
            .ForMember(dest => dest.IpAddress, opt => opt.MapFrom(src => src.IpAddress))
            .ForMember(dest => dest.MacAddress, opt => opt.MapFrom(src => src.MacAddress))
            .ForMember(dest => dest.ExternalCode, opt => opt.MapFrom(src => src.ExternalCode))
            .ForMember(dest => dest.LastSeen, opt => opt.MapFrom(src => src.LastSeen))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));
        #endregion

        #region DiagramAnnotation
        CreateMap<DiagramAnnotation, DiagramAnnotationDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CoordinateX, opt => opt.MapFrom(src => src.CoordinateX))
            .ForMember(dest => dest.CoordinateY, opt => opt.MapFrom(src => src.CoordinateY))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.Rotation, opt => opt.MapFrom(src => src.Rotation))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.IsVisible, opt => opt.MapFrom(src => src.IsVisible))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.CabinetId, opt => opt.MapFrom(src => src.CabinetId))
            .ForMember(dest => dest.CabinetName, opt => opt.MapFrom(src => src.Cabinet != default ? src.Cabinet.Name : default))
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(dest => dest.Shape, opt => opt.MapFrom(src => src.Shape))
            .ForMember(dest => dest.FontColor, opt => opt.MapFrom(src => src.FontColor))
            .ForMember(dest => dest.FontSize, opt => opt.MapFrom(src => src.FontSize))
            .ForMember(dest => dest.IsBold, opt => opt.MapFrom(src => src.IsBold))
            .ForMember(dest => dest.BorderColor, opt => opt.MapFrom(src => src.BorderColor))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));
        #endregion

        #region DeviceStatus
        CreateMap<DeviceStatus, DeviceStatusDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Color))
            .ForMember(dest => dest.Icon, opt => opt.MapFrom(src => src.Icon))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));
        #endregion

        #region DeviceType
        CreateMap<DeviceType, DeviceTypeDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy))
            .ForMember(dest => dest.CreateDateUtc, opt => opt.MapFrom(src => src.CreateDateUtc))
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.MapFrom(src => src.UpdateDateUtc));
        #endregion

        #region Diagram aggregate okuma
        CreateMap<Cabinet, DiagramCabinetDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CompanyId, opt => opt.MapFrom(src => src.CompanyId))
            .ForMember(dest => dest.DeviceStatusId, opt => opt.MapFrom(src => src.DeviceStatusId))
            .ForMember(dest => dest.DeviceStatusName, opt => opt.MapFrom(src => src.DeviceStatus != default ? src.DeviceStatus.Name : default))
            .ForMember(dest => dest.LastSeen, opt => opt.MapFrom(src => src.LastSeen))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.ScadaIsEnabled, opt => opt.MapFrom(src => src.ScadaIsEnabled))
            .ForMember(dest => dest.ScadaLastIngestAt, opt => opt.MapFrom(src => src.ScadaLastIngestAt));

        CreateMap<Device, DiagramDeviceDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CoordinateX, opt => opt.MapFrom(src => src.CoordinateX))
            .ForMember(dest => dest.CoordinateY, opt => opt.MapFrom(src => src.CoordinateY))
            .ForMember(dest => dest.Rotation, opt => opt.MapFrom(src => src.Rotation))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.IsVisible, opt => opt.MapFrom(src => src.IsVisible))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.ComponentTemplateId, opt => opt.MapFrom(src => src.ComponentTemplateId))
            .ForMember(dest => dest.ExternalCode, opt => opt.MapFrom(src => src.ExternalCode))
            .ForMember(dest => dest.DeviceStatusId, opt => opt.MapFrom(src => src.DeviceStatusId))
            .ForMember(dest => dest.DeviceStatusName, opt => opt.MapFrom(src => src.DeviceStatus != default ? src.DeviceStatus.Name : default))
            .ForMember(dest => dest.LastSeen, opt => opt.MapFrom(src => src.LastSeen))
            // Sablon ozeti cihazla birlikte tasinir: sablon pasife alinsa bile
            // kabin dogru boyut ve renkle render olmali.
            .ForMember(dest => dest.Template, opt => opt.MapFrom(src => src.ComponentTemplate))
            .ForMember(dest => dest.Pins, opt => opt.MapFrom(src => src.Pins))
            .ForMember(dest => dest.IoChannels, opt => opt.MapFrom(src => src.IoChannels));

        CreateMap<ComponentTemplate, DiagramComponentTemplateDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.DeviceTypeId, opt => opt.MapFrom(src => src.DeviceTypeId))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.BackgroundImageUrl, opt => opt.MapFrom(src => src.BackgroundImageUrl));

        CreateMap<Pin, DiagramPinDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.RelativeX, opt => opt.MapFrom(src => src.RelativeX))
            .ForMember(dest => dest.RelativeY, opt => opt.MapFrom(src => src.RelativeY))
            .ForMember(dest => dest.Side, opt => opt.MapFrom(src => src.Side))
            .ForMember(dest => dest.Function, opt => opt.MapFrom(src => src.Function))
            .ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction))
            .ForMember(dest => dest.VoltageLevel, opt => opt.MapFrom(src => src.VoltageLevel))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.ChannelNumber))
            .ForMember(dest => dest.ComponentTemplatePinId, opt => opt.MapFrom(src => src.ComponentTemplatePinId))
            .ForMember(dest => dest.IoChannelId, opt => opt.MapFrom(src => src.IoChannelId));

        CreateMap<IoChannel, DiagramIoChannelDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.ChannelNumber))
            .ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction))
            .ForMember(dest => dest.IsEnabled, opt => opt.MapFrom(src => src.IsEnabled))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));

        CreateMap<DiagramAnnotation, DiagramAnnotationItemDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CoordinateX, opt => opt.MapFrom(src => src.CoordinateX))
            .ForMember(dest => dest.CoordinateY, opt => opt.MapFrom(src => src.CoordinateY))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.Rotation, opt => opt.MapFrom(src => src.Rotation))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.IsVisible, opt => opt.MapFrom(src => src.IsVisible))
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(dest => dest.Shape, opt => opt.MapFrom(src => src.Shape))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.FontColor, opt => opt.MapFrom(src => src.FontColor))
            .ForMember(dest => dest.FontSize, opt => opt.MapFrom(src => src.FontSize))
            .ForMember(dest => dest.IsBold, opt => opt.MapFrom(src => src.IsBold))
            .ForMember(dest => dest.BorderColor, opt => opt.MapFrom(src => src.BorderColor));

        CreateMap<CanvasSettings, DiagramCanvasSettingsDto>()
            .ForMember(dest => dest.GridSize, opt => opt.MapFrom(src => src.GridSize))
            .ForMember(dest => dest.SnapToGrid, opt => opt.MapFrom(src => src.SnapToGrid))
            .ForMember(dest => dest.BackgroundVariant, opt => opt.MapFrom(src => src.BackgroundVariant))
            .ForMember(dest => dest.GridColor, opt => opt.MapFrom(src => src.GridColor))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.MinZoom, opt => opt.MapFrom(src => src.MinZoom))
            .ForMember(dest => dest.MaxZoom, opt => opt.MapFrom(src => src.MaxZoom));
        #endregion

        #region Diagram aggregate yazma (Device ve Connection Draft eklenecek)
        CreateMap<DiagramAnnotationDraft, DiagramAnnotation>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.CoordinateX, opt => opt.MapFrom(src => src.CoordinateX))
            .ForMember(dest => dest.CoordinateY, opt => opt.MapFrom(src => src.CoordinateY))
            .ForMember(dest => dest.Width, opt => opt.MapFrom(src => src.Width))
            .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
            .ForMember(dest => dest.Rotation, opt => opt.MapFrom(src => src.Rotation))
            .ForMember(dest => dest.ZIndex, opt => opt.MapFrom(src => src.ZIndex))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(src => src.IsLocked))
            .ForMember(dest => dest.IsVisible, opt => opt.MapFrom(src => src.IsVisible))
            .ForMember(dest => dest.Text, opt => opt.MapFrom(src => src.Text))
            .ForMember(dest => dest.Shape, opt => opt.MapFrom(src => src.Shape))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.FontColor, opt => opt.MapFrom(src => src.FontColor))
            .ForMember(dest => dest.FontSize, opt => opt.MapFrom(src => src.FontSize))
            .ForMember(dest => dest.IsBold, opt => opt.MapFrom(src => src.IsBold))
            .ForMember(dest => dest.BorderColor, opt => opt.MapFrom(src => src.BorderColor));
        #endregion

        #region CanvasSettings upsert
        CreateMap<CanvasSettingsUpsertDto, CanvasSettings>()
            .ForMember(dest => dest.GridSize, opt => opt.MapFrom(src => src.GridSize))
            .ForMember(dest => dest.SnapToGrid, opt => opt.MapFrom(src => src.SnapToGrid))
            .ForMember(dest => dest.BackgroundVariant, opt => opt.MapFrom(src => src.BackgroundVariant))
            .ForMember(dest => dest.GridColor, opt => opt.MapFrom(src => src.GridColor))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.MinZoom, opt => opt.MapFrom(src => src.MinZoom))
            .ForMember(dest => dest.MaxZoom, opt => opt.MapFrom(src => src.MaxZoom));

        CreateMap<CanvasSettingsUpsertDto, DiagramCanvasSettingsDto>()
            .ForMember(dest => dest.GridSize, opt => opt.MapFrom(src => src.GridSize))
            .ForMember(dest => dest.SnapToGrid, opt => opt.MapFrom(src => src.SnapToGrid))
            .ForMember(dest => dest.BackgroundVariant, opt => opt.MapFrom(src => src.BackgroundVariant))
            .ForMember(dest => dest.GridColor, opt => opt.MapFrom(src => src.GridColor))
            .ForMember(dest => dest.BackgroundColor, opt => opt.MapFrom(src => src.BackgroundColor))
            .ForMember(dest => dest.MinZoom, opt => opt.MapFrom(src => src.MinZoom))
            .ForMember(dest => dest.MaxZoom, opt => opt.MapFrom(src => src.MaxZoom));
        #endregion

        #region ChannelEvent
        CreateMap<ChannelEvent, ChannelEventDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.IoChannelId, opt => opt.MapFrom(src => src.IoChannelId))
            .ForMember(dest => dest.CabinetId, opt => opt.MapFrom(src => src.CabinetId))
            .ForMember(dest => dest.ChannelName, opt => opt.MapFrom(src => src.IoChannel != default ? src.IoChannel.Name : default))
            .ForMember(dest => dest.ChannelNumber, opt => opt.MapFrom(src => src.IoChannel != default ? (int?)src.IoChannel.ChannelNumber : default))
            .ForMember(dest => dest.DeviceId, opt => opt.MapFrom(src => src.IoChannel != default ? (Guid?)src.IoChannel.DeviceId : default))
            .ForMember(dest => dest.DeviceName, opt => opt.MapFrom(src => src.IoChannel != default && src.IoChannel.Device != default ? src.IoChannel.Device.Name : default))
            .ForMember(dest => dest.DeviceExternalCode, opt => opt.MapFrom(src => src.IoChannel != default && src.IoChannel.Device != default ? src.IoChannel.Device.ExternalCode : default))
            .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.Value))
            .ForMember(dest => dest.PreviousValue, opt => opt.MapFrom(src => src.PreviousValue))
            .ForMember(dest => dest.OccurredAtUtc, opt => opt.MapFrom(src => src.OccurredAtUtc))
            .ForMember(dest => dest.ReceivedAtUtc, opt => opt.MapFrom(src => src.ReceivedAtUtc));
        #endregion

        #region CameraCapture
        CreateMap<CameraCapture, CameraCaptureDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.CameraId, opt => opt.MapFrom(src => src.CameraId))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.CapturedAtUtc, opt => opt.MapFrom(src => src.CapturedAtUtc))
            .ForMember(dest => dest.DurationSec, opt => opt.MapFrom(src => src.DurationSec))
            .ForMember(dest => dest.RelativePath, opt => opt.MapFrom(src => src.RelativePath))
            .ForMember(dest => dest.SizeBytes, opt => opt.MapFrom(src => src.SizeBytes))
            .ForMember(dest => dest.FailureReason, opt => opt.MapFrom(src => src.FailureReason))
            .ForMember(dest => dest.ExpiresAt, opt => opt.MapFrom(src => src.ExpiresAt))
            .ForMember(dest => dest.RequestedByUserId, opt => opt.MapFrom(src => src.RequestedByUserId));
        #endregion
    }
}
