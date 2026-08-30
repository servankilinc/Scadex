using Scadex.Core.Enums;

namespace Scadex.Core.Utils.Localization;

public class LocalizationSettings
{
    public Language DefaultLanguage { get; set; }
    public List<Language> AvailableLanguages { get; set; } = null!;
}

public class LocalizationSettingsConfigirationRaw
{
    public string? DefaultLanguage { get; set; }
    public List<string>? AvailableLanguages { get; set; }

    public LocalizationSettings ToLocalizationSettings() => new LocalizationSettings()
    {
        DefaultLanguage = GlobalExtensions.GetEnumByDescription<Language>(DefaultLanguage ?? Language.Turkish.GetDescription()),
        AvailableLanguages = AvailableLanguages?.Select(code => GlobalExtensions.GetEnumByDescription<Language>(code)).ToList() ?? [Language.Turkish]
    };
}
