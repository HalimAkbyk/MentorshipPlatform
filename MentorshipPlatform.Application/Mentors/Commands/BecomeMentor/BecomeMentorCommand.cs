using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Mentors.Commands.BecomeMentor;

public record BecomeMentorCommand(
    string University,
    string Department,
    string Bio,
    int? GraduationYear,
    string? Headline) : IRequest<Result<BecomeMentorResponse>>;

public record BecomeMentorResponse(string AccessToken, string RefreshToken, string[] Roles);

public class BecomeMentorCommandHandler : IRequestHandler<BecomeMentorCommand, Result<BecomeMentorResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public BecomeMentorCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _currentUser = currentUser;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<BecomeMentorResponse>> Handle(
        BecomeMentorCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<BecomeMentorResponse>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Check if mentor profile already exists
        var profileExists = await _context.MentorProfiles
            .AnyAsync(m => m.UserId == userId, cancellationToken);

        if (profileExists)
            return Result<BecomeMentorResponse>.Failure("Mentor profile already exists");

        // Get the user
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            return Result<BecomeMentorResponse>.Failure("User not found");

        // Check if user already has Mentor role
        if (user.Roles.Contains(UserRole.Mentor))
            return Result<BecomeMentorResponse>.Failure("User already has Mentor role");

        // Create mentor profile
        var profile = MentorProfile.Create(userId, request.University, request.Department);
        profile.UpdateProfile(request.Bio, request.Headline, request.GraduationYear);

        _context.MentorProfiles.Add(profile);

        // Add mentor role to user
        user.AddRole(UserRole.Mentor);

        await _context.SaveChangesAsync(cancellationToken);

        // Generate fresh JWT tokens with updated roles
        var (accessToken, refreshToken) = _jwtTokenGenerator.GenerateTokens(
            userId,
            user.Email ?? string.Empty,
            user.Roles.ToArray());

        var roles = user.Roles.Select(r => r.ToString()).ToArray();

        return Result<BecomeMentorResponse>.Success(
            new BecomeMentorResponse(accessToken, refreshToken, roles));
    }
}
