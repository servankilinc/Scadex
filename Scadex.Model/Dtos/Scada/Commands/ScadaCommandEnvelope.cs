using Scadex.Core.Model;
using Scadex.Model.Enums;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> Scadex'in SCADA'ya GONDERDIGI komut govdesi — ingest'in ters yönü </summary>
public class ScadaCommandEnvelope : IDto
{
    public Guid CabinetId { get; set; }

    /// <summary>
    /// <see cref="CommandId"/>: SCADA tarafinda TEKRAR TESPITI icin tasinir yani biz bir retry mekanizması kurarsak ve scada içinde komut kontrolü varsa biz kaç kez 
    /// gönderirsek gönderelim sadece 1 kez çalıştırır. örenğin tekrarlanan bir paketin roleyi iki kez surmemesi SCADA'nin elindedir ve bunu ancak degismeyen bir kimlikle yapabilir.
    /// </summary>
    public Guid CommandId { get; set; }


    /// <summary> 
    /// Hedef kanal. Bu aşamada tek komut turu (<c>SetOutput</c>) old için her zaman bir kanali hedefler ve hep doludur.
    /// Hedef nokta — <c>"OUT5"</c>, <c>"OUT17"</c> (LED).
    /// </summary>
    public string Pin { get; set; } = null!;

    public EntityEnums.DeviceCommandType CommandType { get; set; }

    public string? Value { get; set; }

    /// <summary>Komutun SUNUCUDA olustugu an.</summary>
    public DateTime IssuedAtUtc { get; set; }
}

#region RESULT AND DATA MODELS

/// <summary> SCADA komut yazma sonucu. </summary>
public readonly record struct ScadaCommandResponse(CommandStatus Status, string? Message);



/// <summary>
/// Gecmis kayıtların modeli. niyet ve taşınan data birlikte yazilir: yalnizca data saklansaydi, 
/// NC kabloli bir rolenin gecmisinde <c>"0"</c> goren biri bunun "kapat" mi yoksa "ac" mi oldugunu bir daha çıkaramazdı.
/// Pin'in fonksiyonu da gidiyor ki kablolama sonradan degisse bile o anki yorum sabit kalsın.
/// </summary>
public sealed record ScadaCommandPayload(bool TurnOn, string Value, PinFunction? Polarity); 
#endregion
