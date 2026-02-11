using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MeController(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var id = _currentUser.UserId.Value;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (user == null) return Unauthorized();

        return Ok(ToDto(user));
    }

    public sealed record UpdateMeRequest(
        string DisplayName,
        string? Phone,
        int? BirthYear,
        string? AvatarUrl
    );

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateMeRequest req, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var id = _currentUser.UserId.Value;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user == null) return Unauthorized();

        user.UpdateProfile(
            displayName: req.DisplayName,
            phone: req.Phone,
            birthYear: req.BirthYear,
            avatarUrl:req.AvatarUrl
        );

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(user));
    }

    private static object ToDto(User u) => new
    {
        id = u.Id,
        email = u.Email,
        displayName = u.DisplayName,
        phone = u.Phone,
        birthYear = u.BirthYear,
        avatarUrl = u.AvatarUrl,
        status = u.Status.ToString(),
        createdAt = u.CreatedAt,
        updatedAt = u.UpdatedAt,
        roles = u.Roles.Select(r => r.ToString()).ToArray(),
    };
}
