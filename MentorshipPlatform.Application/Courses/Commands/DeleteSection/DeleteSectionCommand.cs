using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.DeleteSection;

public record DeleteSectionCommand(Guid SectionId) : IRequest<Result>;

public class DeleteSectionCommandValidator : AbstractValidator<DeleteSectionCommand>
{
    public DeleteSectionCommandValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
    }
}

public class DeleteSectionCommandHandler : IRequestHandler<DeleteSectionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;

    public DeleteSectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<Result> Handle(DeleteSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var section = await _context.CourseSections
            .Include(s => s.Course)
            .Include(s => s.Lectures)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);

        if (section == null) return Result.Failure("Section not found");
        if (section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        // Delete videos from S3
        foreach (var lecture in section.Lectures)
        {
            if (!string.IsNullOrEmpty(lecture.VideoKey))
            {
                try { await _storage.DeleteFileAsync(lecture.VideoKey, cancellationToken); } catch { }
            }
        }

        _context.CourseSections.Remove(section);
        await _context.SaveChangesAsync(cancellationToken);

        // Update course stats
        var totalLectures = await _context.CourseLectures
            .CountAsync(l => _context.CourseSections
                .Where(s => s.CourseId == section.CourseId)
                .Select(s => s.Id)
                .Contains(l.SectionId), cancellationToken);
        var totalDuration = await _context.CourseLectures
            .Where(l => _context.CourseSections
                .Where(s => s.CourseId == section.CourseId)
                .Select(s => s.Id)
                .Contains(l.SectionId))
            .SumAsync(l => l.DurationSec, cancellationToken);
        section.Course.UpdateStats(totalDuration, totalLectures);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
