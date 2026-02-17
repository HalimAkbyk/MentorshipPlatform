using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.ReviewMessageReport;

public record ReviewMessageReportCommand(
    Guid ReportId,
    ReportStatus Status,
    string? AdminNotes) : IRequest<Result>;

public class ReviewMessageReportCommandValidator : AbstractValidator<ReviewMessageReportCommand>
{
    public ReviewMessageReportCommandValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Status).Must(s => s == ReportStatus.Reviewed || s == ReportStatus.Dismissed)
            .WithMessage("Durum yalnızca 'Reviewed' veya 'Dismissed' olabilir.");
    }
}

public class ReviewMessageReportCommandHandler : IRequestHandler<ReviewMessageReportCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReviewMessageReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReviewMessageReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result.Failure("Giriş yapmalısınız.");

        var report = await _context.MessageReports
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

        if (report == null)
            return Result.Failure("Rapor bulunamadı.");

        if (report.Status != ReportStatus.Pending)
            return Result.Failure("Bu rapor zaten işlenmiş.");

        report.Review(request.Status, request.AdminNotes, _currentUser.UserId!.Value);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
