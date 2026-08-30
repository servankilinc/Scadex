namespace Scadex.Core.Utils.Validation;

public interface IValidationService
{
    ValidatorResult Validate<TModel>(TModel model);
    ValidatorResult Validate<TModel>(IEnumerable<TModel> models);
    Task<ValidatorResult> ValidateAsync<TModel>(TModel model, CancellationToken cancellationToken = default);
    Task<ValidatorResult> ValidateAsync<TModel>(IEnumerable<TModel> models, CancellationToken cancellationToken = default);
}