using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionRequests.Commands.CreateSessionRequest;

[RequiresFeature(FeatureFlags.SessionRequestEnabled)]
public record CreateSessionRequestCommand(
    Guid MentorUserId,
    Guid OfferingId,
    DateTime RequestedStartAt,
    int DurationMin,
    string? StudentNote) : IRequest<Result<Guid>>;

public class CreateSessionRequestCommandValidator : AbstractValidator<CreateSessionRequestCommand>
{
    public CreateSessionRequestCommandValidator()
    {
        RuleFor(x => x.MentorUserId)
            .NotEmpty().WithMessage("Egitmen secimi zorunludur");

        RuleFor(x => x.OfferingId)
            .NotEmpty().WithMessage("Paket secimi zorunludur");

        RuleFor(x => x.RequestedStartAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("Talep edilen tarih gelecekte olmalidir");

        RuleFor(x => x.DurationMin)
            .InclusiveBetween(15, 180).WithMessage("Sure 15 ile 180 dakika arasinda olmalidir");
    }
}

public class CreateSessionRequestCommandHandler : IRequestHandler<CreateSessionRequestCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAdminNotificationService _adminNotification;

    public CreateSessionRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAdminNotificationService adminNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _adminNotification = adminNotification;
    }

    public async Task<Result<Guid>> Handle(CreateSessionRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        // Check offering exists and belongs to mentor
        var offering = await _context.Offerings
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId
                && o.MentorUserId == request.MentorUserId
                && o.IsActive, cancellationToken);

        if (offering == null)
            return Result<Guid>.Failure("Paket bulunamadi veya aktif degil");

        // Don't allow requesting a session with yourself
        if (studentUserId == request.MentorUserId)
            return Result<Guid>.Failure("Kendinize seans talebi gonderemezsiniz");

        // Check for duplicate pending request
        var hasPendingRequest = await _context.SessionRequests
            .AnyAsync(sr => sr.StudentUserId == studentUserId
                && sr.MentorUserId == request.MentorUserId
                && sr.OfferingId == request.OfferingId
                && sr.Status == SessionRequestStatus.Pending, cancellationToken);

        if (hasPendingRequest)
            return Result<Guid>.Failure("Bu paket icin zaten bekleyen bir talebiniz var");

        var sessionRequest = SessionRequest.Create(
            studentUserId,
            request.MentorUserId,
            request.OfferingId,
            request.RequestedStartAt,
            request.DurationMin,
            request.StudentNote);

        _context.SessionRequests.Add(sessionRequest);

        // Get student name for notification
        var studentName = await _context.Users
            .Where(u => u.Id == studentUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Ogrenci";

        // Create UserNotification for mentor
        var mentorNotification = UserNotification.Create(
            request.MentorUserId,
            "SessionRequest",
            "Yeni seans talebi",
            $"{studentName} size yeni bir seans talebi gonderdi",
            "SessionRequest",
            sessionRequest.Id);

        _context.UserNotifications.Add(mentorNotification);

        await _context.SaveChangesAsync(cancellationToken);

        // Admin notification
        await _adminNotification.CreateOrUpdateGroupedAsync(
            "SessionRequest",
            "new-session-requests",
            count => ("Yeni seans talepleri", $"{count} yeni seans talebi bekliyor"),
            "SessionRequest",
            sessionRequest.Id,
            cancellationToken);

        return Result<Guid>.Success(sessionRequest.Id);
    }
}
