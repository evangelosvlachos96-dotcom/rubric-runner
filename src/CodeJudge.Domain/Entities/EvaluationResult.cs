using CodeJudge.Domain.Enums;

namespace CodeJudge.Domain.Entities;

/// <summary>
/// The outcome of one rubric item for one submission. Exactly three exist once a
/// submission is <see cref="SubmissionStatus.Completed"/> — one per <see cref="RubricItem"/>
/// (Database Design §9). Created through the static factories, never mutated afterwards.
/// </summary>
public sealed class EvaluationResult
{
    // EF Core materialisation constructor.
    private EvaluationResult()
    {
    }

    private EvaluationResult(
        RubricItem rubricItem,
        bool passed,
        bool skipped,
        string? message,
        string? output,
        int? testsPassed,
        int? testsTotal,
        int? durationMs)
    {
        Id = Guid.CreateVersion7();
        RubricItem = rubricItem;
        Passed = passed;
        Skipped = skipped;
        Message = message;
        Output = output;
        TestsPassed = testsPassed;
        TestsTotal = testsTotal;
        DurationMs = durationMs;
        EvaluatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public RubricItem RubricItem { get; private set; }

    public bool Passed { get; private set; }

    public bool Skipped { get; private set; }

    /// <summary>Short, display-safe summary (matched keyword, first diagnostic, "3/4 test cases passed", …).</summary>
    public string? Message { get; private set; }

    /// <summary>Structured detail as JSON (compiler diagnostics or per-test-case results). Stored as <c>jsonb</c>.</summary>
    public string? Output { get; private set; }

    /// <summary><see cref="RubricItem.Test"/> only: number of cases that passed.</summary>
    public int? TestsPassed { get; private set; }

    /// <summary><see cref="RubricItem.Test"/> only: number of cases run.</summary>
    public int? TestsTotal { get; private set; }

    /// <summary>Wall-clock time of this rubric step in milliseconds.</summary>
    public int? DurationMs { get; private set; }

    public DateTime EvaluatedAt { get; private set; }

    // NOTE: the instruction names these factories Passed/Failed/SkippedResult, but `Passed`
    // collides with the bool property of the same name. The data property keeps the exact
    // contract name `Passed`; the factory verbs are Pass/Fail/Skip to resolve the collision.

    /// <summary>A rubric item that ran and passed.</summary>
    public static EvaluationResult Pass(
        RubricItem item,
        string? message = null,
        string? output = null,
        int? testsPassed = null,
        int? testsTotal = null,
        int? durationMs = null)
        => new(item, passed: true, skipped: false, message, output, testsPassed, testsTotal, durationMs);

    /// <summary>A rubric item that ran and failed.</summary>
    public static EvaluationResult Fail(
        RubricItem item,
        string? message = null,
        string? output = null,
        int? testsPassed = null,
        int? testsTotal = null,
        int? durationMs = null)
        => new(item, passed: false, skipped: false, message, output, testsPassed, testsTotal, durationMs);

    /// <summary>A rubric item that did not run because an earlier item failed. <see cref="Passed"/> is <c>false</c>.</summary>
    public static EvaluationResult Skip(RubricItem item)
        => new(item, passed: false, skipped: true, message: "Skipped because an earlier rubric item failed.", output: null, testsPassed: null, testsTotal: null, durationMs: null);

    /// <summary>Attaches this result to its parent submission. Called by <see cref="Submission.Complete"/>.</summary>
    internal void AttachTo(Guid submissionId) => SubmissionId = submissionId;
}
