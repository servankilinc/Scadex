using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.CanvasSettings.Queries;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface ICanvasSettingsService
{
    // Get
    Task<Result<CanvasSettings>> GetAsync(Expression<Func<CanvasSettings, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<CanvasSettings>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CanvasSettingsDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<CanvasSettings>>> GetListAsync(Expression<Func<CanvasSettings, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CanvasSettings>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CanvasSettingsDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    /// <summary>  Bir kabinin canvas tercihlerini yazar; kayit yoksa olusturur (upsert). </summary>
    Task<Result<DiagramCanvasSettingsDto>> UpsertAsync(Guid cabinetId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken = default);
}
