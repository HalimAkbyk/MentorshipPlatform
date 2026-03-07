using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.SubmitAssignment;

public record SubmitAssignmentCommand(
    Guid AssignmentId,
    string? SubmissionText,
    string? FileUrl,
    string? OriginalFileName) : IRequest<Result<Guid>>;

public class SubmitAssignmentCommandValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.SubmissionText).MaximumLength(5000).When(x => x.SubmissionText != null);
        RuleFor(x => x.OriginalFileName).MaximumLength(300).When(x => x.OriginalFileName != null);
    }
}

public class SubmitAssignmentCommandHandler : IRequestHandler<SubmitAssignmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubmitAssignmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(SubmitAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId, cancellationToken);

        if (assignment == null)
            return Result<Guid>.Failure("Odev bulunamadi");

        if (assignment.Status != AssignmentStatus.Published)
            return Result<Guid>.Failure("Bu odev teslime acik degil");

        // Check if late
        var isLate = assignment.DueDate.HasValue && DateTime.UtcNow > assignment.DueDate.Value;

        if (isLate && !assignment.AllowLateSubmission)
            return Result<Guid>.Failure("Bu odevin teslim suresi gecmis ve gec teslime izin verilmiyor");

        // Check for existing submission (allow resubmission if Returned)
        var existingSubmission = await _context.AssignmentSubmissions
            .FirstOrDefaultAsync(x => x.AssignmentId == request.AssignmentId && x.StudentUserId == _currentUser.UserId.Value, cancellationToken);

        if (existingSubmission != null && existingSubmission.Status != SubmissionStatus.Returned)
            return Result<Guid>.Failure("Bu odev icin zaten teslim yapmissiniz");

        if (existingSubmission != null && existingSubmission.Status == SubmissionStatus.Returned)
        {
            // Remove old submission for resubmission
            _context.AssignmentSubmissions.Remove(existingSubmission);
        }

        var submission = AssignmentSubmission.Create(
            request.AssignmentId,
            _currentUser.UserId.Value,
            request.SubmissionText,
            request.FileUrl,
            request.OriginalFileName,
            isLate);

        _context.AssignmentSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(submission.Id);
    }
}
