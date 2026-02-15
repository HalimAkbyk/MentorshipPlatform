using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.CreateSection;

public record CreateSectionCommand(Guid CourseId, string Title) : IRequest<Result<Guid>>;

public class CreateSectionCommandValidator : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Bölüm adı zorunludur").MaximumLength(200);
    }
}

public class CreateSectionCommandHandler : IRequestHandler<CreateSectionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateSectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<Guid>.Failure("User not authenticated");

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null) return Result<Guid>.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result<Guid>.Failure("Not authorized");

        var maxSort = await _context.CourseSections
            .Where(s => s.CourseId == request.CourseId)
            .MaxAsync(s => (int?)s.SortOrder, cancellationToken) ?? -1;

        var section = CourseSection.Create(request.CourseId, request.Title, maxSort + 1);
        _context.CourseSections.Add(section);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(section.Id);
    }
}
