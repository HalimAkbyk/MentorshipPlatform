using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.AssignCurriculumToStudent;

public record AssignCurriculumToStudentCommand(
    Guid CurriculumId,
    Guid StudentUserId) : IRequest<Result<Guid>>;

public class AssignCurriculumToStudentCommandValidator : AbstractValidator<AssignCurriculumToStudentCommand>
{
    public AssignCurriculumToStudentCommandValidator()
    {
        RuleFor(x => x.CurriculumId).NotEmpty();
        RuleFor(x => x.StudentUserId).NotEmpty();
    }
}

public class AssignCurriculumToStudentCommandHandler : IRequestHandler<AssignCurriculumToStudentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssignCurriculumToStudentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AssignCurriculumToStudentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .Include(x => x.Weeks)
                .ThenInclude(x => x.Topics)
            .FirstOrDefaultAsync(x => x.Id == request.CurriculumId, cancellationToken);

        if (curriculum == null)
            return Result<Guid>.Failure("Mufredat bulunamadi");

        if (curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Sadece kendi mufredatinizi ogrenciye atayabilirsiniz");

        // Check student exists
        var studentExists = await _context.Users
            .AnyAsync(x => x.Id == request.StudentUserId, cancellationToken);

        if (!studentExists)
            return Result<Guid>.Failure("Ogrenci bulunamadi");

        // Check if already enrolled
        var alreadyEnrolled = await _context.StudentCurriculumEnrollments
            .AnyAsync(x => x.CurriculumId == request.CurriculumId
                && x.StudentUserId == request.StudentUserId
                && x.Status == "Active", cancellationToken);

        if (alreadyEnrolled)
            return Result<Guid>.Failure("Ogrenci zaten bu mufredatta kayitli");

        var enrollment = StudentCurriculumEnrollment.Create(
            request.CurriculumId,
            request.StudentUserId,
            _currentUser.UserId.Value);

        _context.StudentCurriculumEnrollments.Add(enrollment);

        // Create TopicProgress for all topics
        foreach (var week in curriculum.Weeks)
        {
            foreach (var topic in week.Topics)
            {
                var progress = TopicProgress.Create(enrollment.Id, topic.Id);
                _context.TopicProgresses.Add(progress);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(enrollment.Id);
    }
}
