using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;

namespace Scadex.Business.Abstract;

public interface IComponentTemplateService
{
    // List

    /// <summary> Diayagram üzerindeki listeleme için. </summary>
    Task<Result<ICollection<ComponentTemplatePaletteDto>>> GetPaletteAsync(CancellationToken cancellationToken = default);

    /// <summary> DeviceType bilgileri ve devicetype başına aktif componentTemplate sayısı. </summary>
    Task<Result<ICollection<ComponentTemplateTypeCountDto>>> GetTypeCountsAsync(CancellationToken cancellationToken = default);

    /// <summary> Seçili DeviceTypeId'nin aktif ComponentTemplate listesi, pin şeması olmadan. </summary>
    Task<Result<ICollection<ComponentTemplateListItemDto>>> GetListByDeviceTypeAsync(int deviceTypeId, CancellationToken cancellationToken = default);

    /// <summary> ComponentTemplate ve ComponentTemplatePin TEK transaction'da olusturur. </summary>
    Task<Result<CreatedDto>> CreateAsync(ComponentTemplateCreateRequest request, CancellationToken cancellationToken = default);
}
