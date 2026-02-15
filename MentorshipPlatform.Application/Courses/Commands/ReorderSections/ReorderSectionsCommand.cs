using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.ReorderSections;

public record ReorderSectionsCommand(Guid CourseId, List<Guid> SectionIds) : IRequest<Result>;

public class ReorderSectionsCommandValidator : AbstractValidator<ReorderSectionsCommand>
{
    public ReorderSectionsCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionIds).NotEmpty();
    }
}

public class ReorderSectionsCommandHandler : IRequestHandler<ReorderSectionsCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReorderSectionsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReorderSectionsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null) return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        var sections = await _context.CourseSections
            .Where(s => s.CourseId == request.CourseId)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < request.SectionIds.Count; i++)
        {
            var section = sections.FirstOrDefault(s => s.Id == request.SectionIds[i]);
            section?.SetSortOrder(i);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
