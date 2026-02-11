using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Mentors.Commands.CreateMentorProfile;

public record CreateMentorProfileCommand(
    string University,
    string Department,
    string Bio,
    int? GraduationYear) : IRequest<Result<Guid>>;

public class CreateMentorProfileCommandValidator : AbstractValidator<CreateMentorProfileCommand>
{
    public CreateMentorProfileCommandValidator()
    {
        RuleFor(x => x.University).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Bio).MaximumLength(2000);
    }
}

public class CreateMentorProfileCommandHandler : IRequestHandler<CreateMentorProfileCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateMentorProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        CreateMentorProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Check if profile exists
        var exists = await _context.MentorProfiles
            .AnyAsync(m => m.UserId == userId, cancellationToken);

        if (exists)
            return Result<Guid>.Failure("Mentor profile already exists");

        // Create profile
        var profile = MentorProfile.Create(userId, request.University, request.Department);
        profile.UpdateProfile(request.Bio, null, request.GraduationYear);

        _context.MentorProfiles.Add(profile);

        // Add mentor role to user
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        user!.AddRole(UserRole.Mentor);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(userId);
    }
}