using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Mentors.Commands.UpdateMentorProfile;

public record UpdateMentorProfileCommand(
    string Bio,
    string? Headline,
    int? GraduationYear) : IRequest<Result>;

public class UpdateMentorProfileCommandValidator : AbstractValidator<UpdateMentorProfileCommand>
{
    public UpdateMentorProfileCommandValidator()
    {
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.Headline).MaximumLength(300);
    }
}

public class UpdateMentorProfileCommandHandler : IRequestHandler<UpdateMentorProfileCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateMentorProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        UpdateMentorProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var profile = await _context.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (profile == null)
            return Result.Failure("Mentor profile not found");

        profile.UpdateProfile(request.Bio, request.Headline, request.GraduationYear);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}