using Scadex.Core.Enums;

namespace Scadex.Core.Utils.ResultPattern;

public class Error
{
    public ErrorType Type { get; set; }
    public int Code { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string[]>? ValidationFailures { get; protected init; }
    public Dictionary<string, object?>? Metadata { get; set; }

    #region Constructors
    public Error(ErrorType type, string? description = default, int code = default, Dictionary<string, object?>? metadata = default)
    {
        Type = type;
        Code = code == default ? (int)type : code;
        Description = description ?? type.GetDefaultDescription();
        Metadata = metadata;
    }
    public Error(Dictionary<string, string[]> validationFailures, string? description = default, int code = default, Dictionary<string, object?>? metadata = default) : this(ErrorType.Validation, description, code, metadata)
    {
        ValidationFailures = validationFailures;
    }

    #endregion

    #region Static Factory Methods
    public static Error Failure(
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default
    ) => new Error(ErrorType.Failure, description, code, metadata);

    public static Error NotFound(
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default
    ) => new Error(ErrorType.NotFound, description, code, metadata);

    public static Error Forbidden(
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default
    ) => new Error(ErrorType.Forbidden, description, code, metadata);

    public static Error Validation(
        Dictionary<string, string[]> validationFailures,
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default
    ) => new Error(validationFailures, description, code, metadata);
    #endregion
}
