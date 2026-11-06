using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.Diagram.Queries;

namespace Scadex.Business.Abstract;

public interface ICanvasSettingsService
{
    // List

    /// <summary> Bir otomasyonun canvas tercihlerini(ayarlarını) yazar; kayit yoksa olusturur (upsert). </summary>
    Task<Result<DiagramCanvasSettingsDto>> UpsertAsync(Guid automationId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken = default);
}
