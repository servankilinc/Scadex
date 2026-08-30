using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Scadex.Core.Utils.ResultPattern;

public class Result : IResult
{
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; protected init; } = true;
    public string Message { get; set; } = string.Empty;
    public Error? Error { get; protected init; }

    #region Constructor
    public Result(string? message = default)
    {
        IsSuccess = true;
        Message = message ?? this.GetDefaultMessage(); ;
    }

    public Result(Error error, string? message = default)
    {
        IsSuccess = false;
        Error = error;
        Message = message ?? this.GetDefaultMessage();
    }
    #endregion


    #region Static Factory Methods
    public static Result Success(string? message = default)
    {
        return new Result(message);
    }

    public static Result Failure(
        string? message = default,
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memeberName = "",
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var _metadata = HandleMetadata(metadata, filePath, memeberName, lineNumber);
        return new Result(Error.Failure(description, code, _metadata), message);
    }

    public static Result NotFound(
        string? message = default,
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memeberName = "",
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var _metadata = HandleMetadata(metadata, filePath, memeberName, lineNumber);
        return new Result(Error.NotFound(description, code, _metadata), message);
    }

    public static Result Validation(
        Dictionary<string, string[]> validationFailures,
        string? message = default,
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memeberName = "",
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var _metadata = HandleMetadata(metadata, filePath, memeberName, lineNumber, validationFailures);
        return new Result(Error.Validation(validationFailures, description, code, _metadata), message);
    }

    public static Result Forbidden(
        string? message = default,
        string? description = default,
        int code = default,
        Dictionary<string, object?>? metadata = default,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memeberName = "",
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var _metadata = HandleMetadata(metadata, filePath, memeberName, lineNumber);
        return new Result(Error.Forbidden(description, code, _metadata), message);
    }

    private static Dictionary<string, object?> HandleMetadata(Dictionary<string, object?>? details, string filePath, string memeberName, int lineNumber, Dictionary<string, string[]>? validationFailures = null)
    {
        Dictionary<string, object?> metadata = new()
        {
            { "filePath", filePath },
            { "memeberName", memeberName },
            { "lineNumber", lineNumber }
        };
        if (details != default)
        {
            foreach (var param in details)
            {
                metadata[param.Key] = param.Value;
            }
        }
        if (validationFailures != null)
        {
            metadata["validationFailures"] = validationFailures;
        }
        return metadata;
    }
    #endregion
}