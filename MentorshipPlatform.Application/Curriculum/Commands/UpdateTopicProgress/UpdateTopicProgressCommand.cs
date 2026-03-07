using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.UpdateTopicProgress;

public record UpdateTopicProgressCommand(
    Guid EnrollmentId,
    Guid TopicId,
    TopicStatus Status,
    string? MentorNote,
    Guid? BookingId) : IRequest<Result<bool>>;

public class UpdateTopicProgressCommandValidator : AbstractValidator<UpdateTopicProgressCommand>
{
    public UpdateTopicProgressCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateTopicProgressCommandHandler : IRequestHandler<UpdateTopicProgressCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateTopicProgressCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateTopicProgressCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var enrollment = await _context.StudentCurriculumEnrollments
            .Include(x => x.TopicProgresses)
            .Include(x => x.Curriculum)
                .ThenInclude(x => x.Weeks)
                    .ThenInclude(x => x.Topics)
            .FirstOrDefaultAsync(x => x.Id == request.EnrollmentId, cancellationToken);

        if (enrollment == null)
            return Result<bool>.Failure("Kayit bulunamadi");

        if (enrollment.MentorUserId != _currentUser.UserId.Value)
            return Result<bool>.Failure("Bu islemi sadece atanan egitmen yapabilir");

        // Find or create TopicProgress
        var topicProgress = enrollment.TopicProgresses
            .FirstOrDefault(tp => tp.CurriculumTopicId == request.TopicId);

        if (topicProgress == null)
        {
            // Verify topic belongs to this curriculum
            var topicExists = enrollment.Curriculum.Weeks
                .SelectMany(w => w.Topics)
                .Any(t => t.Id == request.TopicId);

            if (!topicExists)
                return Result<bool>.Failure("Konu bu mufredatta bulunamadi");

            topicProgress = TopicProgress.Create(enrollment.Id, request.TopicId);
            _context.TopicProgresses.Add(topicProgress);
        }

        topicProgress.UpdateStatus(request.Status, request.MentorNote, request.BookingId);

        // Recalculate completion percentage
        var totalTopics = enrollment.Curriculum.Weeks
            .SelectMany(w => w.Topics)
            .Count();

        var completedTopics = enrollment.TopicProgresses
            .Count(tp => tp.CurriculumTopicId != request.TopicId && tp.Status == TopicStatus.Completed);

        // Include the current update
        if (request.Status == TopicStatus.Completed)
            completedTopics++;

        var percentage = totalTopics > 0
            ? Math.Round((decimal)completedTopics / totalTopics * 100, 2)
            : 0;

        enrollment.UpdateCompletionPercentage(percentage);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
