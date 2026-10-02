using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

/// <summary>  Şablon yönetim ekranındaki tip kartının sayacı: bir cihaz tipindeki AKTİF şablon sayısı. </summary>
public class ComponentTemplateTypeCountDto : IDto
{
    public int DeviceTypeId { get; set; }
    public int Count { get; set; }
}
