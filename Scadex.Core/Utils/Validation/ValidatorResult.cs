using System.Diagnostics.CodeAnalysis;

namespace Scadex.Core.Utils.Validation;

public class ValidatorResult
{
    [MemberNotNullWhen(false, nameof(Failures))]
    public bool IsValid { get; set; }
    public Dictionary<string, string[]>? Failures { get; set; }

    #region Constructor
    public ValidatorResult(bool isValid)
    {
        IsValid = isValid;
    }
    public ValidatorResult(Dictionary<string, string[]> failures) : this(false)
    {
        Failures = failures;
    }
    #endregion

    #region Static Factory
    public static ValidatorResult Success() => new ValidatorResult(true);
    public static ValidatorResult Failure(Dictionary<string, string[]> failures) => new ValidatorResult(failures);
    #endregion
}
