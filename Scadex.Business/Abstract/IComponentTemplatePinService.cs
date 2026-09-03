using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.ComponentTemplatePin.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IComponentTemplatePinService
{
    // Get
    Task<Result<ComponentTemplatePin>> GetAsync(Expression<Func<ComponentTemplatePin, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ComponentTemplatePin>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ComponentTemplatePinDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<ComponentTemplatePin>>> GetListAsync(Expression<Func<ComponentTemplatePin, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ComponentTemplatePin>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<ComponentTemplatePinDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
}
