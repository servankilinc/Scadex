using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scadex.Core.Utils;

public static class ProjectJsonOptions
{
    // Web varsayilani ile baslat. Bu, camelCase ozellik adlari ve sayilari string'ten okuyabilme gibi ayarlari icerir.
    public static readonly JsonSerializerOptions SerializerOptions = CreateApiJson();
    private static JsonSerializerOptions CreateApiJson() =>
        new JsonSerializerOptions(JsonSerializerDefaults.Web).SetByProjectSettings();


    public static JsonSerializerOptions SetByProjectSettings(this JsonSerializerOptions options)
    {
        // DTO/response ozellikleri camelCase.
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Sozluk ANAHTARLARI donusturulmez - ProblemDetails.errors'in PascalCase kalmasinin sebebi budur. Bilerek null.
        options.DictionaryKeyPolicy = null;

        // Gelen govde PascalCase de olsa baglanir.
        options.PropertyNameCaseInsensitive = true;

        // Web varsayilani sayilari string'ten de okuyabilir ("1" -> 1). 
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        // null alanlar govdede kalir
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        return options;
    }
}
