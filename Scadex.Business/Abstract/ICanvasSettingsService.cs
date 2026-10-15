using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.Diagram.Queries.Items;

namespace Scadex.Business.Abstract;

public interface ICanvasSettingsService
{
    // List

    /// <summary> Bir kabinin canvas tercihlerini(ayarlarını) yazar; kayit yoksa olusturur (upsert). </summary>
    Task<Result<DiagramCanvasSettingsDto>> UpsertAsync(Guid cabinetId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken = default);
}
