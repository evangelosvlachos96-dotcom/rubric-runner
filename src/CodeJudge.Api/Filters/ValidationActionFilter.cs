using CodeJudge.Api.Models.Requests;
using CodeJudge.Application.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CodeJudge.Api.Filters;

/// <summary>
/// Runs FluentValidation on each action argument before the action executes (System Design §6.7).
/// API request models are mapped to their application command so the command validator (which lives
/// in Application) can run. A failure throws <see cref="ValidationException"/>, which the exception
/// middleware turns into a <c>400</c> <see cref="Models.Common.ApiResult{TData}"/>.
/// </summary>
public sealed class ValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Binding/deserialisation errors (malformed JSON, unknown enum value) land in ModelState because
        // the built-in 400 is suppressed; surface them through the same ValidationException path.
        if (!context.ModelState.IsValid)
        {
            throw new ValidationException(ToFailures(context.ModelState));
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            // Map known request models onto the command the validator targets.
            var toValidate = argument is CreateSubmissionRequest request
                ? (object)request.ToCommand()
                : argument;

            var validatorType = typeof(IValidator<>).MakeGenericType(toValidate.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(toValidate);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }

    private static List<ValidationFailure> ToFailures(ModelStateDictionary modelState)
    {
        var failures = new List<ValidationFailure>();
        foreach (var (key, entry) in modelState)
        {
            foreach (var error in entry.Errors)
            {
                // Keys look like "$.language" for body properties; keep only the property name.
                var property = key.TrimStart('$', '.');
                var isLanguage = string.Equals(property, "language", StringComparison.OrdinalIgnoreCase);

                var message = isLanguage
                    ? "Language must be one of: CSharp, Python, JavaScript."
                    : string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The request body is invalid." : error.ErrorMessage;

                failures.Add(new ValidationFailure(property, message)
                {
                    ErrorCode = isLanguage ? nameof(CodeJudgeErrorCode.UnsupportedLanguage) : null,
                });
            }
        }

        return failures;
    }
}
