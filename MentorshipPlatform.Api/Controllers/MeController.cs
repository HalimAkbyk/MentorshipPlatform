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
    private readonly IStorageService _storage;

    public MeController(IApplicationDbContext db, ICurrentUserService currentUser, IStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
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
            avatarUrl: req.AvatarUrl ?? user.AvatarUrl
        );

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(user));
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        if (avatar == null || avatar.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya secilmedi." } });

        // Max 2MB
        if (avatar.Length > 2 * 1024 * 1024)
            return BadRequest(new { errors = new[] { "Dosya boyutu 2MB'den buyuk olamaz." } });

        // Only images
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
            return BadRequest(new { errors = new[] { "Sadece JPG, PNG, GIF veya WebP dosyalari yuklenebilir." } });

        var userId = _currentUser.UserId.Value;
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user == null) return Unauthorized();

        // Upload to storage
        using var stream = avatar.OpenReadStream();
        var result = await _storage.UploadFileAsync(
            stream,
            avatar.FileName,
            avatar.ContentType,
            userId.ToString(),
            "avatar",
            ct);

        if (!result.Success)
            return BadRequest(new { errors = new[] { result.ErrorMessage ?? "Dosya yuklenemedi." } });

        // Update user avatar URL
        user.UpdateProfile(
            displayName: user.DisplayName,
            phone: user.Phone,
            birthYear: user.BirthYear,
            avatarUrl: result.PublicUrl);

        await _db.SaveChangesAsync(ct);

        return Ok(new { avatarUrl = result.PublicUrl });
    }

    [HttpPut("avatar-url")]
    public async Task<IActionResult> SetAvatarUrl([FromBody] SetAvatarUrlRequest req, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var userId = _currentUser.UserId.Value;
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user == null) return Unauthorized();

        user.UpdateProfile(
            displayName: user.DisplayName,
            phone: user.Phone,
            birthYear: user.BirthYear,
            avatarUrl: req.AvatarUrl);

        await _db.SaveChangesAsync(ct);

        return Ok(new { avatarUrl = req.AvatarUrl });
    }

    public sealed record SetAvatarUrlRequest(string AvatarUrl);

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
