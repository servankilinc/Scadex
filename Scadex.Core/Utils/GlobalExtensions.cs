using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Scadex.Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scadex.Core.Utils;

public static class GlobalExtensions
{
    #region Result Extensions
    public static string GetDefaultMessage(this ResultPattern.IResult result)
    {
        if (result.IsSuccess) return "The operation completed successfully";
        return result.Error.Type switch
        {
            ErrorType.Failure => "The operation could not be completed successfully",
            ErrorType.NotFound => "The operation could not be completed successfully",
            ErrorType.Validation => "The operation could not be completed successfully",
            ErrorType.Forbidden => "The operation could not be completed successfully",
            _ => "The operation could not be completed successfully"
        };
    }

    public static string GetDefaultDescription(this ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Failure => "An error occurred in the process",
            ErrorType.NotFound => "Data not available",
            ErrorType.Validation => "Failed to pass the data validation rules",
            ErrorType.Forbidden => "A problem occurred due to an unauthorized operation.",
            _ => "An error occurred"
        };
    }

    public static int GetHttpStatusCode(this ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    public static Dictionary<string, string[]> ParseDictionary(this List<FluentValidation.Results.ValidationFailure> validationFailures)
    {
        return validationFailures
            .GroupBy(v => v.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage).ToArray());
    }

    public static ProblemDetails GetProblemDetail(this ResultPattern.IResult result)
    {
        if (result.IsSuccess || result.Error == null) throw new InvalidOperationException("Cannot generate ProblemDetails for a successful result or null error.");

        var problemDetail = new ProblemDetails
        {
            Type = $"problems/{result.Error.Type}",
            Status = result.Error.Type.GetHttpStatusCode(),
            Title = result.Message,
            Detail = string.Empty, //result.Error.Description,
            Extensions =
            {
                ["code"] = result.Error.Code
            }
        };
        if (result.Error.Type == ErrorType.Validation && result.Error.ValidationFailures != null)
        {
            problemDetail.Extensions["errors"] = result.Error.ValidationFailures;
        }
        return problemDetail;
    }

    public static Dictionary<string, object?> Meta(string key, object? value)
    {
        return new Dictionary<string, object?>()
        {
            [key] = value
        };
    }

    public static Dictionary<string, object?> Meta(params (string Key, object? Value)[] values)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var (k, v) in values)
            dict[k] = v;

        return dict;
    }
    #endregion

    #region String Extensions
    public static string? Truncate(this string? value, int max)
    {
        return value == null || value.Length <= max ? value : value[..max];
    }
    #endregion
}
