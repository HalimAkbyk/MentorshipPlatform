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
            return Result<Guid>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        var groupClass = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (groupClass == null || groupClass.Status != ClassStatus.Published)
            return Result<Guid>.Failure("Class not found or not available");

        if (!groupClass.HasAvailableSeats())
            return Result<Guid>.Failure("Class is full");

        // Check if already enrolled
        var existingEnrollment = await _context.ClassEnrollments
            .AnyAsync(e => 
                    e.ClassId == request.ClassId && 
                    e.StudentUserId == studentUserId &&
                    e.Status != EnrollmentStatus.Cancelled,
                cancellationToken);

        if (existingEnrollment)
            return Result<Guid>.Failure("Already enrolled in this class");

        var enrollment = ClassEnrollment.Create(request.ClassId, studentUserId);
        _context.ClassEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(enrollment.Id);
    }
}