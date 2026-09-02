using Scadex.Core.Model;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> Scadex'in SCADA'ya GONDERDIGI komut govdesi — ingest'in ters yonu </summary>
public class ScadaCommandEnvelope : IDto
{
    public Guid CabinetId { get; set; }

    /// <summary>
    /// <see cref="CommandId"/>: SCADA tarafinda TEKRAR TESPITI icin tasinir yani biz bir retry mekanizması kurarsak ve scada içinde komut kontrolü varsa biz kaç kez 
    /// gönderirsek gönderelim sadece 1 kez çalıştırır. örenğin tekrarlanan bir paketin roleyi iki kez surmemesi SCADA'nin elindedir ve bunu ancak degismeyen bir kimlikle yapabilir.
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary><c>Device.ExternalCode</c> — SCADA'nin cihazi tanimasi için.</summary>
    public string ExternalCode { get; set; } = null!;

    /// <summary> Hedef kanal. Bu aşamada tek komut turu (<c>SetOutput</c>) old. için her zaman bir kanali hedefle ve hep doludur. </summary>
    public int? ChannelNumber { get; set; }

    public EntityEnums.DeviceCommandType CommandType { get; set; }

    public string? Value { get; set; }

    /// <summary>Komutun SUNUCUDA olustugu an.</summary>
    public DateTime IssuedAtUtc { get; set; }
}
