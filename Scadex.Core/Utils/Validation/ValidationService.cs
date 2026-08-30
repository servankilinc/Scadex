using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Scadex.Core.Utils.Validation;

public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    public ValidationService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    #region Sync Methods
    public ValidatorResult Validate<TModel>(TModel model)
    {
        var validator = _serviceProvider.GetService<IValidator<TModel>>();
        if (validator == null)
            return ValidatorResult.Success();

        var validationResult = validator.Validate(model);
        if (validationResult.IsValid)
            return ValidatorResult.Success();
        return ValidatorResult.Failure(validationResult.Errors.ParseDictionary());
    }

    public ValidatorResult Validate<TModel>(IEnumerable<TModel> models)
    {
        var validator = _serviceProvider.GetService<IValidator<TModel>>();
        if (validator == null)
            return ValidatorResult.Success();

        var failures = new Dictionary<string, string[]>();

        int index = 0;
        foreach (var model in models)
        {
            var result = validator.Validate(model);
            if (!result.IsValid)
            {
                var temp = result.Errors.ParseDictionary();
                foreach (var item in temp)
                {
                    failures.Add($"{item.Key}[{index}]", item.Value);
                }
            }
            index++;
        }

        if (failures.Any())
            return ValidatorResult.Failure(failures);

        return ValidatorResult.Success();
    }
    #endregion

    #region Async Methods
    public async Task<ValidatorResult> ValidateAsync<TModel>(TModel model, CancellationToken cancellationToken = default)
    {
        var validator = _serviceProvider.GetService<IValidator<TModel>>();
        if (validator == null)
            return ValidatorResult.Success();

        var validationResult = await validator.ValidateAsync(model, cancellationToken);
        if (validationResult.IsValid)
            return ValidatorResult.Success();

        return ValidatorResult.Failure(validationResult.Errors.ParseDictionary());
    }

    public async Task<ValidatorResult> ValidateAsync<TModel>(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
    {
        var validator = _serviceProvider.GetService<IValidator<TModel>>();
        if (validator == null)
            return ValidatorResult.Success();

        var failures = new Dictionary<string, string[]>();

        int index = 0;
        foreach (var model in models)
        {
            var result = await validator.ValidateAsync(model, cancellationToken);
            if (!result.IsValid)
            {
                var temp = result.Errors.ParseDictionary();
                foreach (var item in temp)
                {
                    failures.Add($"{item.Key}[{index}]", item.Value);
                }
            }
            index++;
        }

        if (failures.Any())
            return ValidatorResult.Failure(failures);

        return ValidatorResult.Success();
    }
    #endregion
}
