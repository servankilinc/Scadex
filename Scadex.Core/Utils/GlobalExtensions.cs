using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Scadex.Core.Enums;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Scadex.Core.Utils;

public static class GlobalExtensions
{
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


    #region Enum Extensions
    public static TEnum GetEnumByDescription<TEnum>(string description) where TEnum : Enum
    {
        foreach (var field in typeof(TEnum).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (TEnum)field.GetValue(null)!;
            }
            else
            {
                if (field.Name == description)
                    return (TEnum)field.GetValue(null)!;
            }
        }

        throw new ArgumentException($"No enum with description '{description}' found in {typeof(TEnum)}");
    }

    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
        return attr?.Description ?? value.ToString();
    }
    #endregion


    #region String Extensions
    public static string ToSeoFriendly(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string result = text.Trim();

        result = result.ToLowerInvariant()
            .Replace('ç', 'c').Replace('Ç', 'C')
            .Replace('ğ', 'g').Replace('Ğ', 'G')
            .Replace('ı', 'i').Replace('İ', 'I')
            .Replace('ö', 'o').Replace('Ö', 'O')
            .Replace('ş', 's').Replace('Ş', 'S')
            .Replace('ü', 'u').Replace('Ü', 'U');

        result = result.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in result)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        result = sb.ToString();

        result = Regex.Replace(result, @"[^a-z0-9\s-]", "");
        result = Regex.Replace(result, @"\s+", "-").Trim('-');
        result = Regex.Replace(result, @"-+", "-");

        return result;
    }
    #endregion
}
