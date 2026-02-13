using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.DeleteOfferingAvailabilityTemplate;

public record DeleteOfferingAvailabilityTemplateCommand(Guid OfferingId) : IRequest<Result<bool>>;

public class DeleteOfferingAvailabilityTemplateCommandHandler
    : IRequestHandler<DeleteOfferingAvailabilityTemplateCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteOfferingAvailabilityTemplateCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(
        DeleteOfferingAvailabilityTemplateCommand request,
        CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == mentorUserId, ct);

        if (offering == null)
            return Result<bool>.Failure("Offering not found");

        if (!offering.AvailabilityTemplateId.HasValue)
            return Result<bool>.Success(true); // Zaten custom template yok

        var templateId = offering.AvailabilityTemplateId.Value;

        // Template default mı kontrol et (default template silinmemeli)
        var template = await _context.AvailabilityTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template != null && template.IsDefault)
            return Result<bool>.Failure("Varsayılan program silinemez");

        // Bu template'e ait unbooked slot'ları sil
        var slotsToDelete = await _context.AvailabilitySlots
            .Where(s => s.TemplateId == templateId && !s.IsBooked)
            .ToListAsync(ct);

        _context.AvailabilitySlots.RemoveRange(slotsToDelete);

        // Offering'den template bağlantısını kaldır (default'a dönecek)
        offering.SetAvailabilityTemplate(null);

        // Template'i ve ilgili rules/overrides sil (cascade)
        if (template != null)
        {
            _context.AvailabilityTemplates.Remove(template);
        }

        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
