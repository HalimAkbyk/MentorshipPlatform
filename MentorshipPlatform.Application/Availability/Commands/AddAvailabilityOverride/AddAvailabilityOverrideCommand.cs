using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.AddAvailabilityOverride;

public record AddAvailabilityOverrideCommand(
    string Date,
    bool IsBlocked,
    string? StartTime,
    string? EndTime,
    string? Reason) : IRequest<Result<Guid>>;

public class AddAvailabilityOverrideCommandValidator : AbstractValidator<AddAvailabilityOverrideCommand>
{
    public AddAvailabilityOverrideCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Date is required");

        When(x => !x.IsBlocked, () =>
        {
            RuleFor(x => x.StartTime).NotEmpty().WithMessage("Start time required for non-blocked overrides");
            RuleFor(x => x.EndTime).NotEmpty().WithMessage("End time required for non-blocked overrides");
        });
    }
}

public class AddAvailabilityOverrideCommandHandler
    : IRequestHandler<AddAvailabilityOverrideCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddAvailabilityOverrideCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddAvailabilityOverrideCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var template = await _context.AvailabilityTemplates
            .Include(t => t.Overrides)
            .FirstOrDefaultAsync(t => t.MentorUserId == mentorUserId && t.IsDefault, ct);

        // Template yoksa otomatik oluştur (mentor takvimden override ekleyebilsin)
        if (template == null)
        {
            template = AvailabilityTemplate.Create(mentorUserId);
            _context.AvailabilityTemplates.Add(template);
            await _context.SaveChangesAsync(ct);
        }

        var date = DateOnly.Parse(request.Date);

        // Aynı tarihte mevcut override varsa sil
        var existing = template.Overrides.FirstOrDefault(o => o.Date == date);
        if (existing != null)
        {
            template.RemoveOverride(existing.Id);
        }

        var @override = AvailabilityOverride.Create(
            date,
            request.IsBlocked,
            !request.IsBlocked && request.StartTime != null ? TimeSpan.Parse(request.StartTime) : null,
            !request.IsBlocked && request.EndTime != null ? TimeSpan.Parse(request.EndTime) : null,
            request.Reason);

        template.AddOverride(@override);
        await _context.SaveChangesAsync(ct);

        return Result<Guid>.Success(@override.Id);
    }
}
