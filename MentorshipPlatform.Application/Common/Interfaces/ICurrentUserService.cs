using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IEnumerable<UserRole> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
}