using System.Text.Json.Serialization;
using CodeJudge.Application.Common;

namespace CodeJudge.Api.Models.Common;

/// <summary>
/// The single uniform response envelope for success and error alike (System Design §6.10).
/// A frontend checks one field — <see cref="Status"/> — to know which path it is on.
/// </summary>
public sealed class ApiResult<TData>
{
    /// <summary>Success envelope.</summary>
    public ApiResult(TData data, string? description = "🚀 Houston, we don't have a problem")
    {
        Status = true;
        Data = data;
        Description = description;
    }

    /// <summary>Failure envelope.</summary>
    public ApiResult(CodeJudgeErrorCode code, string? description = "🔥 Houston, we have a problem")
    {
        Status = false;
        Description = description;
        Error = new ErrorDetails(code, description);
    }

    /// <summary><c>true</c> for success, <c>false</c> for error.</summary>
    public bool Status { get; }

    /// <summary>Human-readable summary of the outcome.</summary>
    public string? Description { get; }

    /// <summary>The payload on success; <c>null</c> on error.</summary>
    public TData? Data { get; }

    /// <summary>The error detail on failure; <c>null</c> on success.</summary>
    public ErrorDetails? Error { get; }
}

/// <summary>Machine-readable error detail carried inside a failed <see cref="ApiResult{TData}"/>.</summary>
public sealed class ErrorDetails
{
    public ErrorDetails(CodeJudgeErrorCode errorCode, string? description)
    {
        ErrorCode = errorCode;
        Description = description;
    }

    /// <summary>Stable numeric error code (System Design §6.10 table).</summary>
    [JsonConverter(typeof(NumericEnumConverter<CodeJudgeErrorCode>))]
    public CodeJudgeErrorCode ErrorCode { get; }

    /// <summary>Human-readable error description.</summary>
    public string? Description { get; }
}
