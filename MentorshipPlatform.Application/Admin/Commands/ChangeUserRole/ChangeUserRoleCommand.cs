using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.ChangeUserRole;

// Command
public record ChangeUserRoleCommand(
    Guid UserId,
    string Role,
    string Action
) : IRequest<Result>;

// Handler
public class ChangeUserRoleCommandHandler
    : IRequestHandler<ChangeUserRoleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ChangeUserRoleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        ChangeUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            return Result.Failure("User not found.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return Result.Failure($"Invalid role: {request.Role}. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}");

        var action = request.Action?.ToLower();
        if (action != "add" && action != "remove")
            return Result.Failure("Action must be 'add' or 'remove'.");

        string description;
        string? oldValue;
        string? newValue;

        if (action == "add")
        {
            if (user.Roles.Contains(role))
                return Result.Failure($"User already has the '{role}' role.");

            oldValue = string.Join(", ", user.Roles.Select(r => r.ToString()));
            user.AddRole(role);
            newValue = string.Join(", ", user.Roles.Select(r => r.ToString()));
            description = $"Added {role} role to user {user.DisplayName} ({user.Email})";
        }
        else
        {
            if (!user.Roles.Contains(role))
                return Result.Failure($"User does not have the '{role}' role.");

            oldValue = string.Join(", ", user.Roles.Select(r => r.ToString()));
            user.RemoveRole(role);
            newValue = string.Join(", ", user.Roles.Select(r => r.ToString()));
            description = $"Removed {role} role from user {user.DisplayName} ({user.Email})";
        }

        // Create audit log
        var history = ProcessHistory.Create(
            entityType: "User",
            entityId: request.UserId,
            action: "RoleChanged",
            oldValue: oldValue,
            newValue: newValue,
            description: description,
            performedBy: _currentUser.UserId,
            performedByRole: "Admin");

        _context.ProcessHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
