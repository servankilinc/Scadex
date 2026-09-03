using System.Text.Json;
using CabinetOs.Model.Dtos.Common;

namespace CabinetOs.Business.Utils;

/// <summary> <c>Connection.WaypointsJson</c> (nvarchar(max)) ile sozlesmedeki tipli <see cref="PointDto"/> dizisi arasindaki donusum. </summary>
public static class DiagramWaypoints
{
    /// <summary> Yazarken camelCase uretilir okurken buyuk/kucuk harf onemsenmez. </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static List<PointDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<PointDto>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Bos liste NULL olarak yazilir — "kirilma noktasi yok" ile "[]" ayni sey.</summary>
    public static string? Serialize(ICollection<PointDto>? points)
    {
        return points is null || points.Count == 0 ? null : JsonSerializer.Serialize(points, Options);
    }
}
