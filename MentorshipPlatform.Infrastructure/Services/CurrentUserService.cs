using System.Security.Claims;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace MentorshipPlatform.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User
        .FindFirst(ClaimTypes.Email)?.Value;

    public IEnumerable<UserRole> Roles
    {
        get
        {
            var roleClaims = _httpContextAccessor.HttpContext?.User
                .FindAll(ClaimTypes.Role)
                .Select(c => Enum.Parse<UserRole>(c.Value));
            
            return roleClaims ?? Enumerable.Empty<UserRole>();
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(UserRole role) => Roles.Contains(role);
}