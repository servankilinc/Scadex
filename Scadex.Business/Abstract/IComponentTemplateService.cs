using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IComponentTemplateService
{
    // Get
    Task<Result<ComponentTemplate>> GetAsync(Expression<Func<ComponentTemplate, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ComponentTemplate>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ComponentTemplateBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ComponentTemplateDetailDto>> GetComponentTemplateDetailDtoAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<ComponentTemplate>>> GetListAsync(Expression<Func<ComponentTemplate, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ComponentTemplate>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ComponentTemplateBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ComponentTemplateDetailDto>>> GetComponentTemplateDetailDtoListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);


    /// <summary> Diayagram üzerindeki listeleme için. </summary>
    Task<Result<ICollection<ComponentTemplatePaletteDto>>> GetPaletteAsync(CancellationToken cancellationToken = default);

    /// <summary> ComponentTemplate ve ComponentTemplatePin TEK transaction'da olusturur. </summary>
    Task<Result<CreatedDto>> CreateAsync(ComponentTemplateCreateRequest request, CancellationToken cancellationToken = default);
}
