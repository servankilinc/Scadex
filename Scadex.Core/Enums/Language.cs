using System.ComponentModel;

namespace Scadex.Core.Enums;

public enum Language : byte
{
    [Description("tr-TR")]
    Turkish = 1,
    [Description("en-US")]
    English = 2,
    [Description("ru-RU")]
    Russian = 3,
    [Description("de-DE")]
    German = 4,
    [Description("fr-FR")]
    French = 5,
    [Description("es-ES")]
    Spanish = 6
}