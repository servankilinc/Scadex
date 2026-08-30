using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;

namespace Scadex.Core.Utils.DynamicQuery;

public static class QueryableFilterExtension
{
    public static readonly HashSet<string> Logics = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or"
    };

    /// <summary>
    /// Filtre alani ifadeye dogrudan gomuldugu icin bicimi kisitlanmistir: yalnizca
    /// tanimlayici karakterleri ve en fazla iki seviye nokta (orn. "Company.Name").
    /// Parantez, bosluk veya operator iceren bir Field, Dynamic LINQ ifadesine
    /// istemcinin metod cagrisi enjekte etmesine izin verirdi.
    /// </summary>
    private static readonly Regex FieldPattern = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*){0,2}$", RegexOptions.Compiled);

    /// <summary>
    /// Hicbir sorguda filtrelenmesine izin verilmeyen alanlar. Bunlar uzerinden
    /// karakter karakter deneme yapilarak parola ozeti disari sizdirilabilirdi.
    /// </summary>
    private static readonly HashSet<string> BlockedFieldSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "Token"
    };

    public static readonly Dictionary<string, Func<string, int, string>> OperatorsWithValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = (f, i) => $"np({f}) == @{i}",
        ["neq"] = (f, i) => $"np({f}) != @{i}",
        ["lt"] = (f, i) => $"np({f}) < @{i}",
        ["lte"] = (f, i) => $"np({f}) <= @{i}",
        ["gt"] = (f, i) => $"np({f}) > @{i}",
        ["gte"] = (f, i) => $"np({f}) >= @{i}",
        ["startswith"] = (f, i) => $"np({f}).StartsWith(@{i})",
        ["endswith"] = (f, i) => $"np({f}).EndsWith(@{i})",
        ["contains"] = (f, i) => $"np({f}).Contains(@{i})",
        ["doesnotcontain"] = (f, i) => $"!np({f}).Contains(@{i})"
    };

    public static readonly Dictionary<string, Func<string, string>> OperatorsWithoutValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["isnull"] = f => $"np({f}) == null",
        ["isnotnull"] = f => $"np({f}) != null"
    };

    public static IQueryable<T> ToFilter<T>(this IQueryable<T> query, Filter filter)
    {
        if (filter is null) return query;

        var parameters = new List<object>();
        var where = BuildExpression(filter, parameters);

        return string.IsNullOrWhiteSpace(where) ? query : query.Where(where, parameters.ToArray());
    }

    private static void Validate(Filter filter)
    {
        if (filter.Operator == "base") return;

        if (string.IsNullOrWhiteSpace(filter.Field))
            throw new ArgumentException("Empty field for dynamic filter");

        if (!FieldPattern.IsMatch(filter.Field))
            throw new ArgumentException($"Invalid field for dynamic filter, field: {filter.Field}");

        if (filter.Field.Split('.').Any(BlockedFieldSegments.Contains))
            throw new ArgumentException($"Field is not filterable, field: {filter.Field}");

        if (!OperatorsWithValue.ContainsKey(filter.Operator!) && !OperatorsWithoutValue.ContainsKey(filter.Operator!))
            throw new ArgumentException($"Invalid opreator type for dynamic filter, operator: {filter.Operator}");

        if (!string.IsNullOrWhiteSpace(filter.Logic) && !Logics.Contains(filter.Logic))
            throw new ArgumentException($"Invalid logic type for dynamic filter, logic: {filter.Logic}");
    }

    private static string BuildExpression(Filter filter, IList<object> parameters)
    {
        Validate(filter);

        var parts = new List<string>();

        if (filter.Operator != "base")
        {
            if (OperatorsWithValue.TryGetValue(filter.Operator!, out var op))
            {
                if (!string.IsNullOrWhiteSpace(filter.Value))
                {
                    var index = parameters.Count;
                    parameters.Add(filter.Value!);
                    parts.Add(op(filter.Field!, index));
                }
            }
            else if (OperatorsWithoutValue.TryGetValue(filter.Operator!, out var opNoVal))
            {
                parts.Add(opNoVal(filter.Field!));
            }
        }

        if (filter.Filters?.Any() == true)
        {
            var childParts = filter.Filters
                .Select(f => BuildExpression(f, parameters))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (childParts.Any())
            {
                var logic = string.IsNullOrWhiteSpace(filter.Logic) ? "and" : filter.Logic;
                parts.Add($"({string.Join($" {logic} ", childParts)})");
            }
        }

        if (!parts.Any())
            return string.Empty;

        var joinLogic = string.IsNullOrWhiteSpace(filter.Logic) ? "and" : filter.Logic;

        return $"({string.Join($" {joinLogic} ", parts)})";
    }
}