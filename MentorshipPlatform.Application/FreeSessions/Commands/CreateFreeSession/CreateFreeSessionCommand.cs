using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.FreeSessions.Commands.CreateFreeSession;

public record CreateFreeSessionCommand(
    Guid StudentUserId,
    string? Note) : IRequest<Result<CreateFreeSessionResult>>;

public record CreateFreeSessionResult(Guid FreeSessionId, string RoomName);

[RequiresFeature(FeatureFlags.FreeSessionEnabled)]
public class CreateFreeSessionCommandHandler
    : IRequestHandler<CreateFreeSessionCommand, Result<CreateFreeSessionResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateFreeSessionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CreateFreeSessionResult>> Handle(
        CreateFreeSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Mentor))
            return Result<CreateFreeSessionResult>.Failure("Bu islemi sadece egitmenler yapabilir");

        var mentorUserId = _currentUser.UserId!.Value;

        if (mentorUserId == request.StudentUserId)
            return Result<CreateFreeSessionResult>.Failure("Kendinizle seans baslatamazsiniz");

        // Check student exists
        var student = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.StudentUserId, cancellationToken);

        if (student == null)
            return Result<CreateFreeSessionResult>.Failure("Ogrenci bulunamadi");

        // FIFO: find earliest-expiry PrivateLesson credit with remaining balance
        var credit = await _context.StudentCredits
            .Where(c => c.StudentId == request.StudentUserId
                     && c.CreditType == CreditType.PrivateLesson
                     && c.UsedCredits < c.TotalCredits
                     && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .OrderBy(c => c.ExpiresAt ?? DateTime.MaxValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (credit == null || !credit.HasAvailableCredits(1))
            return Result<CreateFreeSessionResult>.Failure("Ogrencinin yeterli PrivateLesson kredisi yok");

        // Deduct 1 credit
        credit.UseCredits(1);

        // Create credit transaction
        var roomName = $"free-session-{Guid.NewGuid():N}";
        var freeSession = FreeSession.Create(mentorUserId, request.StudentUserId, roomName, request.Note);

        var transaction = CreditTransaction.Create(
            credit.Id,
            CreditTransactionType.Usage,
            -1,
            freeSession.Id,
            "FreeSession",
            mentorUserId,
            "Anlik seans — kredi kullanimi");

        _context.CreditTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        freeSession.SetCreditTransaction(transaction.Id);
        freeSession.Start();

        _context.FreeSessions.Add(freeSession);

        // Notify student
        var mentorName = await _context.Users
            .Where(u => u.Id == mentorUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Egitmen";

        var notification = UserNotification.Create(
            request.StudentUserId,
            "FreeSessionInvite",
            "Anlik seans daveti",
            $"{mentorName} sizi anlik bir seansa davet etti.",
            "FreeSession",
            freeSession.Id);

        _context.UserNotifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateFreeSessionResult>.Success(
            new CreateFreeSessionResult(freeSession.Id, freeSession.RoomName));
    }
}

public class CreateFreeSessionCommandValidator : AbstractValidator<CreateFreeSessionCommand>
{
    public CreateFreeSessionCommandValidator()
    {
        RuleFor(x => x.StudentUserId).NotEmpty().WithMessage("Ogrenci secilmelidir");
    }
}
