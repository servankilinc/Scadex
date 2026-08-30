using System.Text.Json;

namespace Scadex.Core.Utils;

public static class ProjectJsonOptions
{
    // Web varsayilani ile baslat. Bu, camelCase ozellik adlari ve sayilari string'ten okuyabilme gibi ayarlari icerir.
    public static readonly JsonSerializerOptions SerializerOptions = CreateApiJson();
    private static JsonSerializerOptions CreateApiJson() =>
        new JsonSerializerOptions(JsonSerializerDefaults.Web).SetByProjectSettings();
}
