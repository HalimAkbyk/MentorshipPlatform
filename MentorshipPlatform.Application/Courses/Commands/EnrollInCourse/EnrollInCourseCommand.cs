using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.EnrollInCourse;

public record EnrollInCourseCommand(Guid CourseId) : IRequest<Result<Guid>>;

public class EnrollInCourseCommandValidator : AbstractValidator<EnrollInCourseCommand>
{
    public EnrollInCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class EnrollInCourseCommandHandler : IRequestHandler<EnrollInCourseCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EnrollInCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(EnrollInCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<Guid>.Failure("User not authenticated");
        var studentId = _currentUser.UserId.Value;

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null) return Result<Guid>.Failure("Course not found");
        if (course.Status != CourseStatus.Published) return Result<Guid>.Failure("Bu kurs şu anda satışta değil");

        // Kendi kursuna kayıt olamaz
        if (course.MentorUserId == studentId)
            return Result<Guid>.Failure("Kendi kursunuza kayıt olamazsınız");

        var existing = await _context.CourseEnrollments
            .FirstOrDefaultAsync(e => e.CourseId == request.CourseId && e.StudentUserId == studentId, cancellationToken);

        if (existing != null)
        {
            if (existing.Status == CourseEnrollmentStatus.Active)
                return Result<Guid>.Failure("Bu kursa zaten kayıtlısınız");
            if (existing.Status == CourseEnrollmentStatus.PendingPayment)
                return Result<Guid>.Success(existing.Id); // Return existing pending enrollment
        }

        var enrollment = CourseEnrollment.Create(request.CourseId, studentId);
        _context.CourseEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(enrollment.Id);
    }
}
