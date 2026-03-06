using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class User : BaseEntity
{
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? PasswordHash { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public bool IsAvatarFlagged { get; private set; }
    public int? BirthYear { get; private set; }
    public UserStatus Status { get; private set; } = UserStatus.Active;

    // Instructor (pivot) fields
    public bool IsOwner { get; private set; }
    public DateTime? InstructorAssignedAt { get; private set; }
    public InstructorStatus? InstructorStatus { get; private set; }

    // External (social) login
    public string? ExternalProvider { get; private set; }
    public string? ExternalId { get; private set; }

    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public MentorProfile? MentorProfile { get; private set; }

    private User() { } // EF Core

    public static User Create(string email, string displayName, string passwordHash)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHash
        };
        return user;
    }

    public static User CreateExternal(string email, string displayName, string? avatarUrl, string provider, string externalId)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            ExternalProvider = provider,
            ExternalId = externalId
        };
        return user;
    }

    public void LinkExternalProvider(string provider, string externalId)
    {
        ExternalProvider = provider;
        ExternalId = externalId;
    }

    public void AddRole(UserRole role)
    {
        if (!_roles.Contains(role))
        {
            _roles.Add(role);
        }
    }

    public void RemoveRole(UserRole role)
    {
        _roles.Remove(role);
    }

    public void UpdateProfile(string displayName,string? phone, string? avatarUrl, int? birthYear)
    {
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        Phone=phone;
        BirthYear = birthYear;
        UpdatedAt = DateTime.UtcNow;
    }
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend() => Status = UserStatus.Suspended;
    public void Activate() => Status = UserStatus.Active;

    public void FlagAvatar()
    {
        IsAvatarFlagged = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnflagAvatar()
    {
        IsAvatarFlagged = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetAvatar()
    {
        AvatarUrl = null;
        IsAvatarFlagged = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsMinor()
    {
        if (!BirthYear.HasValue) return false;
        var currentYear = DateTime.UtcNow.Year;
        return (currentYear - BirthYear.Value) < 18;
    }

    public void AssignAsInstructor()
    {
        if (!_roles.Contains(UserRole.Mentor))
            _roles.Add(UserRole.Mentor);
        InstructorAssignedAt = DateTime.UtcNow;
        InstructorStatus = Enums.InstructorStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOwner(bool isOwner)
    {
        IsOwner = isOwner;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SuspendInstructor()
    {
        InstructorStatus = Enums.InstructorStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveInstructor()
    {
        InstructorStatus = Enums.InstructorStatus.Removed;
        RemoveRole(UserRole.Mentor);
        UpdatedAt = DateTime.UtcNow;
    }
}