using System.ComponentModel.DataAnnotations;

namespace CodeJudge.Infrastructure.Workers;

/// <summary>Worker/evaluation tuning, bound from the <c>Evaluation</c> section (Database Design §8).</summary>
public sealed class EvaluationSettings
{
    public const string SectionName = "Evaluation";

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; set; } = 2;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 5;

    [Range(1, 3600)]
    public int LockDurationSeconds { get; set; } = 90;

    [Range(1, 600)]
    public int CompileTimeoutSeconds { get; set; } = 10;

    [Range(1, 600)]
    public int RunTimeoutSeconds { get; set; } = 5;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 3;
}
