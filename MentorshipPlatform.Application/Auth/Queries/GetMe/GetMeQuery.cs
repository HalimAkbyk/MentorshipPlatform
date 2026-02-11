using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Auth.Queries.GetMe;

public record MeDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole[] Roles,
    UserStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? AvatarUrl,
    int? BirthYear,
    string? Phone
);

public record GetMeQuery() : IRequest<Result<MeDto>>;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMeQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<MeDto>.Failure("Not authenticated");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);

        if (user == null)
            return Result<MeDto>.Failure("User not found");

        var dto = new MeDto(
            Id: user.Id,
            Email: user.Email ?? "",
            DisplayName: user.DisplayName,
            Roles: user.Roles.ToArray(),
            Status: user.Status,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt,
            AvatarUrl: user.AvatarUrl,
            BirthYear: user.BirthYear,
            Phone: user.Phone
        );

        return Result<MeDto>.Success(dto);
    }
}