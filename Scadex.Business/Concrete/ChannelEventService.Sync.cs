using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model;
using Scadex.Model.Realtime.Events;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;
using Scadex.Model.Scada.Queries;

namespace Scadex.Business.Concrete;

/// <summary> Karttan okuma (<c>GET {ScadaBaseUrl}/status</c>) ile kanal değerlerinin senkronu. </summary>
public partial class ChannelEventService
{
    /// <inheritdoc />
    public async Task<Result<ScadaSyncResultDto>> SyncFromScadaAsync(Guid automationId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        // 1) Otomasyon ve kartın SCADA yapılandırması
        var automation = await _unitOfWork.Automations.GetAsync(
            where: c => c.Id == automationId && c.IsActive,
            tracking: false,
            cancellationToken: cancellationToken
        );
        if (automation == null)
            return Result<ScadaSyncResultDto>.NotFound(description: "Otomasyon bulunamadi veya pasif durumda");
        var card = await _unitOfWork.ControlCards.GetActiveForAutomationAsync(automation.Id, cancellationToken: cancellationToken);
        if (card == null)
            return Result<ScadaSyncResultDto>.Failure($"Bu otomasyonda kontrol kartı yok; kart okunamaz", $"automation:{automation.Id}");
        if (!card.ScadaIsEnabled)
            return Result<ScadaSyncResultDto>.Failure($"Bu otomasyonda SCADA kapalı; kart okunamaz", $"automation:{automation.Id}");
        if (string.IsNullOrWhiteSpace(card.ScadaBaseUrl))
            return Result<ScadaSyncResultDto>.Failure($"Kontrol kartının SCADA adresi tanımlı değil", $"automation:{automation.Id}");


        // 2) Kart okunur (tüm kanallar). Yeniden deneme yok. Okuma başlamadan önceki an, aradaki yazımları ayırt etmek için (4. adım).
        var readStartedAt = DateTime.UtcNow;
        var outcome = await _scadaCommandGateway.ReadStatusAsync(card.ScadaBaseUrl, null, timeout ?? IScadaCommandGateway.RequestTimeout(card.ScadaCommandTimeoutMs));


        // 3) Canlılık: komuttaki kuralın aynısı — yanıt verdiyse temas, zaman aşımı / bağlantı hatası ise kayıp.
        if (outcome.Status == CommandStatus.Succeeded)
            await _automationStatusService.RecordScadaContactAsync(automation.Id, CancellationToken.None);
        else if (outcome.Status == CommandStatus.NoResponse)
            await _automationStatusService.RecordScadaUnreachableAsync(automation.Id, outcome.Message, CancellationToken.None);

        // Kartın kendisinden gelen başarısızlık hata değildir (komut sonucuyla aynı sözleşme): Status'te döner.
        if (outcome.Status != CommandStatus.Succeeded)
            return Result<ScadaSyncResultDto>.Success(new ScadaSyncResultDto { Status = outcome.Status, Message = outcome.Message, SyncedAtUtc = DateTime.UtcNow });


        // 4) Kartın bildirdiği her kanal otomasyonun etkin kanalıyla (tip + numara) eşlenir. Kartın BİLDİRMEDİĞİ kanala dokunulmaz:
        //    "okunamadı" "bilinmiyor" (null) demek değildir.
        var channels = await _unitOfWork.IoChannels.GetAllAsync(
            where: c => c.AutomationId == automation.Id && c.IsEnabled,
            tracking: true,
            cancellationToken: cancellationToken
        ) ?? [];
        var byAddress = channels
            .GroupBy(c => (c.Direction, c.ChannelNumber))
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        var writes = new Dictionary<Guid, ChannelWrite>();
        int unmatched = 0;

        foreach (var reading in outcome.Values!)
        {
            // Sözleşme okunamayan kanalı diziye koymaz; yine de null gelirse kayıtlı değer silinmez.
            if (reading.Value == null)
                continue;

            if (!ScadaPinAddress.CheckAndParsePin(reading.Type, out var direction) ||
                !byAddress.TryGetValue((direction, reading.ChannelNumber), out IoChannel? channel))
            {
                unmatched++;
                continue;
            }

            // Okuma sürerken komut tamamlandıysa ya da ingest geldiyse kanalın değeri okumadan YENİDİR: üzerine yazılmaz,
            // yoksa eski okuma başarılı komutu geri alır ve sahte bir "sapma" satırı yazardı.
            if (channel.ValueUpdatedAt >= readStartedAt)
                continue;

            // Aynı kanal iki kez geldiyse sonuncusu geçerli
            writes[channel.Id] = new ChannelWrite(channel, reading.Value, now);
        }

        if (unmatched > 0)
            _logger.LogInformation("Otomasyon {AutomationId}: karttan okunan {Count} kanalın Scadex'te etkin karşılığı yok", automation.Id, unmatched);


        // 5) Ingest ile aynı yazım yolu. Kaynak Sync: değişim anı bilinmiyor, gözlemci "şimdi oldu" diye işlememeli.
        int changed = await ApplyChannelValuesAsync(automation.Id, writes.Values, ChannelChangeSource.Sync, now, cancellationToken);

        return Result<ScadaSyncResultDto>.Success(new ScadaSyncResultDto
        {
            Status = CommandStatus.Succeeded,
            ReadCount = outcome.Values!.Count,
            ChangedCount = changed,
            UnmatchedCount = unmatched,
            SyncedAtUtc = now
        });
    }
}
