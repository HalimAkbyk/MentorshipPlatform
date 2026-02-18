using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Notifications.Queries.GetMyNotifications;

public record UserNotificationDto(
    Guid Id, string Type, string Title, string Message,
    bool IsRead, string? ReferenceType, Guid? ReferenceId,
    DateTime CreatedAt);

public record GetMyNotificationsQuery(int Page = 1, int PageSize = 20) : IRequest<Result<List<UserNotificationDto>>>;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, Result<List<UserNotificationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<UserNotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<UserNotificationDto>>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var notifications = await _context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new UserNotificationDto(
                n.Id, n.Type, n.Title, n.Message,
                n.IsRead, n.ReferenceType, n.ReferenceId,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<UserNotificationDto>>.Success(notifications);
    }
}
