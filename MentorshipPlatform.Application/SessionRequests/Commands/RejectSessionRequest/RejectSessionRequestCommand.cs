using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionRequests.Commands.RejectSessionRequest;

public record RejectSessionRequestCommand(Guid Id, string? Reason) : IRequest<Result>;

public class RejectSessionRequestCommandHandler : IRequestHandler<RejectSessionRequestCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RejectSessionRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RejectSessionRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);
        var isMentor = _currentUser.IsInRole(UserRole.Mentor);

        if (!isAdmin && !isMentor)
            return Result.Failure("Yetkiniz yok");

        var sessionRequest = await _context.SessionRequests
            .FirstOrDefaultAsync(sr => sr.Id == request.Id, cancellationToken);

        if (sessionRequest == null)
            return Result.Failure("Seans talebi bulunamadi");

        if (sessionRequest.Status != SessionRequestStatus.Pending)
            return Result.Failure("Bu talep zaten islenmis");

        // Mentor can only reject their own requests
        if (isMentor && !isAdmin && sessionRequest.MentorUserId != userId)
            return Result.Failure("Bu talep size ait degil");

        var reviewerRole = isAdmin ? "Admin" : "Mentor";
        sessionRequest.Reject(userId, reviewerRole, request.Reason);

        // Get mentor name for notification
        var mentorName = await _context.Users
            .Where(u => u.Id == sessionRequest.MentorUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Egitmen";

        // Create UserNotification for student
        var reasonText = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Sebep: {request.Reason}";
        var studentNotification = UserNotification.Create(
            sessionRequest.StudentUserId,
            "SessionRequestRejected",
            "Seans talebiniz reddedildi",
            $"{mentorName} seans talebinizi reddetti.{reasonText}",
            "SessionRequest",
            sessionRequest.Id);

        _context.UserNotifications.Add(studentNotification);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
