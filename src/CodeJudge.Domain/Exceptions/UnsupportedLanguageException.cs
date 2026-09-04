namespace CodeJudge.Domain.Exceptions;

/// <summary>
/// Thrown when a submission references a language for which no evaluator exists,
/// or an undefined <see cref="Enums.Language"/> value is supplied.
/// Mapped by the API to <c>400 / UnsupportedLanguage</c> (System Design §6.10).
/// </summary>
public sealed class UnsupportedLanguageException : DomainException
{
    public UnsupportedLanguageException(string message)
        : base(message)
    {
    }
}
