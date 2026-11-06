using AutoMapper;
using Scadex.CameraManagement.Model.Dtos.Capture.Queries;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Commands;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Queries;
using Scadex.CameraManagement.Model.Entities;
using Scadex.CameraManagement.Model.Settings;

namespace Scadex.CameraManagement.Mappings;

/// <summary> Modülün eşlemeleri; kaydı modülün kendisindedir (<c>AddCameraManagementModule</c>). Kameranın cihaz eşlemeleri çekirdektedir. </summary>
public class CameraManagementMappingProfile : Profile
{
    public CameraManagementMappingProfile()
    {
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

        #region CameraCaptureSetting
        CreateMap<CameraCaptureSetting, CameraCaptureSettings>()
            .ForMember(dest => dest.SnapshotTimeoutMs, opt => opt.MapFrom(src => src.SnapshotTimeoutMs))
            .ForMember(dest => dest.SnapshotCacheSeconds, opt => opt.MapFrom(src => src.SnapshotCacheSeconds))
            .ForMember(dest => dest.CaptureRoot, opt => opt.MapFrom(src => src.CaptureRoot))
            .ForMember(dest => dest.CaptureRetentionDays, opt => opt.MapFrom(src => src.CaptureRetentionDays))
            .ForMember(dest => dest.MaxClipDurationSec, opt => opt.MapFrom(src => src.MaxClipDurationSec))
            .ForMember(dest => dest.ClipFinalizeGraceMs, opt => opt.MapFrom(src => src.ClipFinalizeGraceMs));

        CreateMap<CameraCaptureSettings, CameraCaptureSettingDto>()
            .ForMember(dest => dest.SnapshotTimeoutMs, opt => opt.MapFrom(src => src.SnapshotTimeoutMs))
            .ForMember(dest => dest.SnapshotCacheSeconds, opt => opt.MapFrom(src => src.SnapshotCacheSeconds))
            .ForMember(dest => dest.CaptureRoot, opt => opt.MapFrom(src => src.CaptureRoot))
            .ForMember(dest => dest.CaptureRetentionDays, opt => opt.MapFrom(src => src.CaptureRetentionDays))
            .ForMember(dest => dest.MaxClipDurationSec, opt => opt.MapFrom(src => src.MaxClipDurationSec))
            .ForMember(dest => dest.ClipFinalizeGraceMs, opt => opt.MapFrom(src => src.ClipFinalizeGraceMs));

        CreateMap<CameraCaptureSettingUpdateDto, CameraCaptureSetting>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SnapshotTimeoutMs, opt => opt.MapFrom(src => src.SnapshotTimeoutMs))
            .ForMember(dest => dest.SnapshotCacheSeconds, opt => opt.MapFrom(src => src.SnapshotCacheSeconds))
            .ForMember(dest => dest.CaptureRoot, opt => opt.MapFrom(src => src.CaptureRoot.Trim('/')))
            .ForMember(dest => dest.CaptureRetentionDays, opt => opt.MapFrom(src => src.CaptureRetentionDays))
            .ForMember(dest => dest.MaxClipDurationSec, opt => opt.MapFrom(src => src.MaxClipDurationSec))
            .ForMember(dest => dest.ClipFinalizeGraceMs, opt => opt.MapFrom(src => src.ClipFinalizeGraceMs))
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreateDateUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdateDateUtc, opt => opt.Ignore());
        #endregion
    }
}
