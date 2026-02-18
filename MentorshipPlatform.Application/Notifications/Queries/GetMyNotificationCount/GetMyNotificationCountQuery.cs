using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Notifications.Queries.GetMyNotificationCount;

public record GetMyNotificationCountQuery() : IRequest<Result<int>>;

public class GetMyNotificationCountQueryHandler : IRequestHandler<GetMyNotificationCountQuery, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(GetMyNotificationCountQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<int>.Failure("User not authenticated");

        var count = await _context.UserNotifications
            .CountAsync(n => n.UserId == _currentUser.UserId.Value && !n.IsRead, cancellationToken);

        return Result<int>.Success(count);
    }
}
