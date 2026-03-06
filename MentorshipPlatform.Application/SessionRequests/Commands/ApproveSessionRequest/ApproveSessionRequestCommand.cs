using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionRequests.Commands.ApproveSessionRequest;

public record ApproveSessionRequestCommand(Guid Id) : IRequest<Result<Guid>>;

public class ApproveSessionRequestCommandHandler : IRequestHandler<ApproveSessionRequestCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ApproveSessionRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(ApproveSessionRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);
        var isMentor = _currentUser.IsInRole(UserRole.Mentor);

        if (!isAdmin && !isMentor)
            return Result<Guid>.Failure("Yetkiniz yok");

        var sessionRequest = await _context.SessionRequests
            .FirstOrDefaultAsync(sr => sr.Id == request.Id, cancellationToken);

        if (sessionRequest == null)
            return Result<Guid>.Failure("Seans talebi bulunamadi");

        if (sessionRequest.Status != SessionRequestStatus.Pending)
            return Result<Guid>.Failure("Bu talep zaten islenmis");

        // Mentor can only approve their own requests
        if (isMentor && !isAdmin && sessionRequest.MentorUserId != userId)
            return Result<Guid>.Failure("Bu talep size ait degil");

        // Create booking (PendingPayment → Confirmed since approved requests skip payment)
        var booking = Booking.Create(
            sessionRequest.StudentUserId,
            sessionRequest.MentorUserId,
            sessionRequest.OfferingId,
            sessionRequest.RequestedStartAt,
            sessionRequest.DurationMin);

        booking.Confirm();

        _context.Bookings.Add(booking);

        // Approve the session request
        var reviewerRole = isAdmin ? "Admin" : "Mentor";
        sessionRequest.Approve(userId, reviewerRole, booking.Id);

        // Get mentor name for notification
        var mentorName = await _context.Users
            .Where(u => u.Id == sessionRequest.MentorUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Egitmen";

        // Create UserNotification for student
        var studentNotification = UserNotification.Create(
            sessionRequest.StudentUserId,
            "SessionRequestApproved",
            "Seans talebiniz onaylandi",
            $"{mentorName} seans talebinizi onayladi. Seans tarihi: {sessionRequest.RequestedStartAt:dd.MM.yyyy HH:mm}",
            "Booking",
            booking.Id);

        _context.UserNotifications.Add(studentNotification);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(booking.Id);
    }
}
