namespace Scadex.Core.Utils.Localization;

/// <summary>
/// Dont use '.' in the key, use '_' instead.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class LocalizablePropAttribute : Attribute
{
    public string Key { get; }
    public LocalizablePropAttribute(string key)
    {
        if (key.Contains('.')) throw new ArgumentException("Localization key cannot contain '.' character. Use '_' instead.");
        Key = key;
    }
}
