using CodeJudge.Api.Models.Requests;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

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
}
