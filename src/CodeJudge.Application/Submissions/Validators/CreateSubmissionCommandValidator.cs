using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Common;
using CodeJudge.Domain.Enums;
using FluentValidation;

namespace CodeJudge.Application.Submissions.Validators;

/// <summary>
/// Validates a <see cref="CreateSubmissionCommand"/> (System Design §6.7). Specific error codes are
/// attached via <c>WithErrorCode</c> so the API middleware can surface a targeted
/// <see cref="CodeJudgeErrorCode"/> instead of the generic <c>ValidationFailed</c>.
/// </summary>
public sealed class CreateSubmissionCommandValidator : AbstractValidator<CreateSubmissionCommand>
{
    public const int MaxCodeLength = 65536;

    public CreateSubmissionCommandValidator(IProblemCatalog catalog)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required.")
            .MaximumLength(100).WithMessage("User id must be at most 100 characters.")
            .Matches("^[A-Za-z0-9_\\-.@]+$")
            .WithMessage("User id may only contain letters, digits and the characters _ - . @");

        RuleFor(x => x.ProblemId)
            .NotEmpty().WithMessage("Problem id is required.")
            .Must(catalog.Exists)
            .WithErrorCode(nameof(CodeJudgeErrorCode.ProblemNotFound))
            .WithMessage(x => $"Problem '{x.ProblemId}' does not exist.");

        RuleFor(x => x.Language)
            .Must(l => Enum.IsDefined(l))
            .WithErrorCode(nameof(CodeJudgeErrorCode.UnsupportedLanguage))
            .WithMessage("Language must be one of: CSharp, Python, JavaScript.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(MaxCodeLength)
            .WithErrorCode(nameof(CodeJudgeErrorCode.CodeTooLarge))
            .WithMessage($"Code must be at most {MaxCodeLength} characters.");
    }
}
