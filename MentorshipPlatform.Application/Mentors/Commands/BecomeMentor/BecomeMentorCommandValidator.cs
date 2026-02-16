using FluentValidation;

namespace MentorshipPlatform.Application.Mentors.Commands.BecomeMentor;

public class BecomeMentorCommandValidator : AbstractValidator<BecomeMentorCommand>
{
    public BecomeMentorCommandValidator()
    {
        RuleFor(x => x.University)
            .NotEmpty().WithMessage("Üniversite adı gereklidir")
            .MaximumLength(200);

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Bölüm adı gereklidir")
            .MaximumLength(200);

        RuleFor(x => x.Bio)
            .MaximumLength(2000);

        RuleFor(x => x.Headline)
            .MaximumLength(300);
    }
}
