using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.ReportMessage;

public record ReportMessageCommand(
    Guid MessageId,
    string Reason) : IRequest<Result<Guid>>;

public class ReportMessageCommandValidator : AbstractValidator<ReportMessageCommand>
{
    public ReportMessageCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ReportMessageCommandHandler : IRequestHandler<ReportMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportMessageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(ReportMessageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<Guid>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        var message = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Booking)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message == null)
            return Result<Guid>.Failure("Mesaj bulunamadı.");

        // Only participants can report
        if (message.Booking.StudentUserId != userId && message.Booking.MentorUserId != userId)
            return Result<Guid>.Failure("Bu mesajı raporlama yetkiniz yok.");

        // Can't report own message
        if (message.SenderUserId == userId)
            return Result<Guid>.Failure("Kendi mesajınızı raporlayamazsınız.");

        // Check for existing pending report
        var existingReport = await _context.MessageReports
            .AsNoTracking()
            .AnyAsync(r => r.MessageId == request.MessageId
                           && r.ReporterUserId == userId
                           && r.Status == ReportStatus.Pending,
                cancellationToken);

        if (existingReport)
            return Result<Guid>.Failure("Bu mesaj için zaten bekleyen bir raporunuz var.");

        var report = MessageReport.Create(request.MessageId, userId, request.Reason);
        _context.MessageReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(report.Id);
    }
}
