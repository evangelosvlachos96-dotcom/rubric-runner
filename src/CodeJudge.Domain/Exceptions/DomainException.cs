namespace CodeJudge.Domain.Exceptions;

/// <summary>
/// Base type for all errors that represent a violated domain invariant.
/// Mapped by the API to <c>400 / ValidationFailed</c> (System Design §6.10).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
