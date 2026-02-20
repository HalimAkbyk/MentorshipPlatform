using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Commands.EnrollInClass;

public record EnrollInClassCommand(Guid ClassId) : IRequest<Result<Guid>>;

public class EnrollInClassCommandHandler
    : IRequestHandler<EnrollInClassCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EnrollInClassCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        EnrollInClassCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("Oturum açmanız gerekiyor");

        var studentUserId = _currentUser.UserId.Value;

        var groupClass = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (groupClass == null || groupClass.Status != ClassStatus.Published)
            return Result<Guid>.Failure("Ders bulunamadı veya aktif değil");

        // Check if class time has passed
        if (groupClass.StartAt <= DateTime.UtcNow)
            return Result<Guid>.Failure("Bu dersin başlangıç saati geçmiştir. Kayıt yapılamaz.");

        if (!groupClass.HasAvailableSeats())
            return Result<Guid>.Failure("Ders kontenjanı dolmuştur");

        // Check existing enrollments for this student
        var existingEnrollment = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e =>
                    e.ClassId == request.ClassId &&
                    e.StudentUserId == studentUserId &&
                    e.Status != EnrollmentStatus.Cancelled &&
                    e.Status != EnrollmentStatus.Refunded,
                cancellationToken);

        // If there's an existing PendingPayment enrollment, reuse it
        // (previous checkout attempt may have failed)
        if (existingEnrollment != null && existingEnrollment.Status == EnrollmentStatus.PendingPayment)
        {
            return Result<Guid>.Success(existingEnrollment.Id);
        }

        // If already confirmed/attended, block
        if (existingEnrollment != null)
            return Result<Guid>.Failure("Bu derse zaten kayıtlısınız");

        var enrollment = ClassEnrollment.Create(request.ClassId, studentUserId);
        _context.ClassEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(enrollment.Id);
    }
}