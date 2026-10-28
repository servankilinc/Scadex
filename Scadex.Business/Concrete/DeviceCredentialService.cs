using Scadex.Business.Abstract;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public class DeviceCredentialService : IDeviceCredentialService
{
    private readonly IUnitOfWork _unitOfWork;

    public DeviceCredentialService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<AuthenticatedDevice>> AuthenticateAsync(DeviceType deviceType, Guid deviceId, string secret, CancellationToken cancellationToken = default)
    {
        if (deviceType != DeviceType.Pc)
            throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Bu cihaz tipinin Basic Auth kimliği yok");

        // Biçimi tutmayan sır veritabanına sorulmaz: bozuk başlıklı istekler sorgu üretmesin.
        if (!DeviceCredential.IsValidSecret(secret))
            return Result<AuthenticatedDevice>.Failure(description: "Sır biçimi geçersiz");

        // Tek PK sorgusu: ek satır + cihaz (aktif mi, şablon tipi doğru mu) + kabin. Pasif cihazın kimliği hash'i dursa da geçmez.
        var row = await _unitOfWork.PcDevices.GetAsync(
            select: p => new CredentialRow(p.CredentialHash, p.Device!.CabinetId),
            where: p => p.DeviceId == deviceId && p.Device!.IsActive && p.Device.ComponentTemplate!.DeviceTypeId == (int)DeviceType.Pc,
            cancellationToken: cancellationToken
        );

        if (row == null)
            return Result<AuthenticatedDevice>.Failure(description: "Cihaz yok, pasif ya da tipi farklı");
        if (row.CredentialHash == null)
            return Result<AuthenticatedDevice>.Failure(description: "Cihazın kimlik bilgisi yok (üretilmemiş ya da iptal edilmiş)");
        if (!DeviceCredential.Verify(secret, row.CredentialHash))
            return Result<AuthenticatedDevice>.Failure(description: "Sır eşleşmedi");

        return Result<AuthenticatedDevice>.Success(new AuthenticatedDevice(deviceId, row.CabinetId));
    }

    /// <inheritdoc />
    public async Task<Result<AuthenticatedDevice>> AuthenticateControlCardAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        var device = await _unitOfWork.ControlCards.GetAsync(
            select: c => new AuthenticatedDevice(c.DeviceId, c.Device!.CabinetId),
            where: c => c.MacAddress == macAddress && c.Device!.IsActive && c.Device.ComponentTemplate!.DeviceTypeId == (int)DeviceType.ControlModule,
            cancellationToken: cancellationToken
        );

        return device == null
            ? Result<AuthenticatedDevice>.Failure(description: "Bu MAC adresiyle eşleşen aktif kontrol kartı yok")
            : Result<AuthenticatedDevice>.Success(device);
    }

    private sealed record CredentialRow(string? CredentialHash, Guid CabinetId);
}
