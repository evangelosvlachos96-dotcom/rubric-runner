using System.Text.Json;
using CodeJudge.Api.Models.Common;
using CodeJudge.Application.Common;
using CodeJudge.Domain.Exceptions;
using FluentValidation;

namespace CodeJudge.Api.Middleware;

/// <summary>
/// Single place that maps every exception type to an HTTP status and a
/// <see cref="CodeJudgeErrorCode"/>, and renders it as an <see cref="ApiResult{TData}"/>
/// (System Design §6.10, §10). Internals are never leaked; 4xx is logged as a warning,
/// 5xx as an error with the request's trace id.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                "Handled {ErrorCode} ({StatusCode}): {Message}. TraceId: {TraceId}",
                errorCode,
                statusCode,
                message,
                context.TraceIdentifier);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var result = new ApiResult<object>(errorCode, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, ApiJson.Options));
    }

    private static (int StatusCode, CodeJudgeErrorCode ErrorCode, string Message) Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                PickValidationCode(validation),
                string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage))),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.EntityName == "Submission" ? CodeJudgeErrorCode.SubmissionNotFound : CodeJudgeErrorCode.NotFound,
                notFound.Message),

            InvalidStatusTransitionException => (
                StatusCodes.Status409Conflict,
                CodeJudgeErrorCode.Conflict,
                exception.Message),

            UnsupportedLanguageException => (
                StatusCodes.Status400BadRequest,
                CodeJudgeErrorCode.UnsupportedLanguage,
                exception.Message),

            DomainException => (
                StatusCodes.Status400BadRequest,
                CodeJudgeErrorCode.ValidationFailed,
                exception.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                CodeJudgeErrorCode.Unknown,
                "An unexpected error occurred while processing the request."),
        };

    // A validator can attach a specific error code via WithErrorCode; prefer it over the generic one.
    private static CodeJudgeErrorCode PickValidationCode(ValidationException validation)
    {
        foreach (var candidate in new[]
                 {
                     CodeJudgeErrorCode.ProblemNotFound,
                     CodeJudgeErrorCode.UnsupportedLanguage,
                     CodeJudgeErrorCode.CodeTooLarge,
                 })
        {
            if (validation.Errors.Any(e => e.ErrorCode == candidate.ToString()))
            {
                return candidate;
            }
        }

        return CodeJudgeErrorCode.ValidationFailed;
    }
}
