using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.UpdateSection;

public record UpdateSectionCommand(Guid SectionId, string Title) : IRequest<Result>;

public class UpdateSectionCommandValidator : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionCommandValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public class UpdateSectionCommandHandler : IRequestHandler<UpdateSectionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateSectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var section = await _context.CourseSections
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);

        if (section == null) return Result.Failure("Section not found");
        if (section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        section.Update(request.Title);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
