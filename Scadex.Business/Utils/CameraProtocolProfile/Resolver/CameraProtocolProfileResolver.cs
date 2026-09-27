using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile.Resolver;

public interface ICameraProtocolProfileResolver
{
    ICameraProtocolProfile Resolve(Camera camera);
}

public class CameraProtocolProfileResolver : ICameraProtocolProfileResolver
{
    private readonly IReadOnlyDictionary<CameraBrand, ICameraProtocolProfile> _profiles;

    public CameraProtocolProfileResolver(IEnumerable<ICameraProtocolProfile> profiles)
    {
        var profileList = profiles.ToList();

        var duplicates = profileList.GroupBy(p => p.Brand).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"Aynı markaya birden fazla ICameraProtocolProfile kayıtlı: {string.Join(", ", duplicates)}");

        _profiles = profileList.ToDictionary(p => p.Brand);

        // Sessiz geri dusus YOK: profili olmayan bir marka yanlis URL sablonuyla "erisilemez" gorunurdu.
        // Enum'a deger eklenip profil unutulursa hata ilk kullanimda (singleton kurulurken) patlar.
        var missing = Enum.GetValues<CameraBrand>().Where(b => !_profiles.ContainsKey(b)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Şu kamera markalarının ICameraProtocolProfile kaydı yok: {string.Join(", ", missing)}");
    }

    public ICameraProtocolProfile Resolve(Camera camera) =>
        _profiles.TryGetValue(camera.Brand, out var profile)
            ? profile
            : throw new InvalidOperationException($"Kamera {camera.Id} için tanımsız marka: {(int)camera.Brand}");
}
