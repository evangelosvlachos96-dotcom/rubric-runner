namespace CodeJudge.Domain.Enums;

/// <summary>
/// A single rubric check. The rubric always runs in this declared order —
/// Security → Compiles → Test — and short-circuits on the first failure
/// (System Design §6.5, Database Design §9).
/// </summary>
public enum RubricItem
{
    Security,
    Compiles,
    Test,
}
