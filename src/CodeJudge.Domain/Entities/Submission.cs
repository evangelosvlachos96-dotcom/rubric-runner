using CodeJudge.Domain.Enums;
using CodeJudge.Domain.Exceptions;

namespace CodeJudge.Domain.Entities;

/// <summary>
/// Aggregate root: one submitted code solution. Owns its lifecycle state machine
/// (System Design §7, Database Design §4) — setters are private and every transition
/// is a guarded domain method. The row also serves as the evaluation job queue via
/// <see cref="Status"/>, <see cref="LockedBy"/>, <see cref="LockedUntil"/> and
/// <see cref="AttemptCount"/> (Database Design §8).
/// </summary>
public sealed class Submission
{
    private readonly List<EvaluationResult> _results = [];

    // EF Core materialisation constructor.
    private Submission()
    {
    }

    private Submission(string userId, string problemId, Language language, string code)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        ProblemId = problemId;
        Language = language;
        Code = code;
        Status = SubmissionStatus.Pending;
        AttemptCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = null!;

    public string ProblemId { get; private set; } = null!;

    public Language Language { get; private set; }

    public string Code { get; private set; } = null!;

    public SubmissionStatus Status { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTime? LockedUntil { get; private set; }

    public int AttemptCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    /// <summary>The rubric results. Empty until the submission is <see cref="SubmissionStatus.Completed"/>.</summary>
    public IReadOnlyCollection<EvaluationResult> Results => _results.AsReadOnly();

    /// <summary>Creates a new <see cref="SubmissionStatus.Pending"/> submission. The only state reachable from the API.</summary>
    public static Submission Create(string userId, string problemId, Language language, string code)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(problemId))
        {
            throw new DomainException("Problem id is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Code is required.");
        }

        if (!Enum.IsDefined(language))
        {
            throw new UnsupportedLanguageException($"Language '{language}' is not supported.");
        }

        return new Submission(userId, problemId, language, code);
    }

    /// <summary>
    /// Claims the submission for evaluation. Allowed from <see cref="SubmissionStatus.Pending"/>,
    /// or from <see cref="SubmissionStatus.Evaluating"/> when the previous lock has expired
    /// (the earlier worker is presumed dead).
    /// </summary>
    public void Claim(string workerId, DateTime lockUntil)
    {
        var lockExpired = Status == SubmissionStatus.Evaluating
            && LockedUntil is { } until
            && until < DateTime.UtcNow;

        if (Status != SubmissionStatus.Pending && !lockExpired)
        {
            throw new InvalidStatusTransitionException(Status, "claim");
        }

        Status = SubmissionStatus.Evaluating;
        LockedBy = workerId;
        LockedUntil = lockUntil;
        AttemptCount++;
        StartedAt ??= DateTime.UtcNow;
    }

    /// <summary>
    /// Records the rubric outcome and marks the submission <see cref="SubmissionStatus.Completed"/>.
    /// Requires exactly one result for each of the three <see cref="RubricItem"/> values.
    /// </summary>
    public void Complete(IEnumerable<EvaluationResult> results)
    {
        if (Status != SubmissionStatus.Evaluating)
        {
            throw new InvalidStatusTransitionException(Status, "complete");
        }

        var materialised = results.ToList();
        var items = materialised.Select(r => r.RubricItem).ToHashSet();
        var expected = Enum.GetValues<RubricItem>();

        if (materialised.Count != expected.Length || !expected.All(items.Contains))
        {
            throw new DomainException(
                $"Complete requires exactly one result per rubric item ({string.Join(", ", expected)}).");
        }

        foreach (var result in materialised)
        {
            result.AttachTo(Id);
            _results.Add(result);
        }

        Status = SubmissionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ClearLock();
    }

    /// <summary>Marks the submission <see cref="SubmissionStatus.Error"/>: the system could not evaluate it.</summary>
    public void Fail(string message)
    {
        if (Status != SubmissionStatus.Evaluating)
        {
            throw new InvalidStatusTransitionException(Status, "fail");
        }

        Status = SubmissionStatus.Error;
        ErrorMessage = message;
        CompletedAt = DateTime.UtcNow;
        ClearLock();
    }

    private void ClearLock()
    {
        LockedBy = null;
        LockedUntil = null;
    }
}
