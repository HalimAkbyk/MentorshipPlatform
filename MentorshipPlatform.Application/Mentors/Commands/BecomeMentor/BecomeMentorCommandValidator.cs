using FluentValidation;

namespace MentorshipPlatform.Application.Mentors.Commands.BecomeMentor;

public class BecomeMentorCommandValidator : AbstractValidator<BecomeMentorCommand>
{
    public BecomeMentorCommandValidator()
    {
        RuleFor(x => x.University)
            .MaximumLength(200).When(x => x.University != null);

        RuleFor(x => x.Department)
            .MaximumLength(200).When(x => x.Department != null);

        RuleFor(x => x.Bio)
            .MaximumLength(2000);

        RuleFor(x => x.Headline)
            .MaximumLength(300);
    }
}
