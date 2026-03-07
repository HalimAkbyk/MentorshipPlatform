using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.ReviewSubmission;

public record ReviewSubmissionCommand(
    Guid SubmissionId,
    int? Score,
    string? Feedback,
    ReviewStatus ReviewStatus) : IRequest<Result<Guid>>;

public class ReviewSubmissionCommandValidator : AbstractValidator<ReviewSubmissionCommand>
{
    public ReviewSubmissionCommandValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.Feedback).MaximumLength(5000).When(x => x.Feedback != null);
        RuleFor(x => x.Score).GreaterThanOrEqualTo(0).When(x => x.Score.HasValue);
    }
}

public class ReviewSubmissionCommandHandler : IRequestHandler<ReviewSubmissionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReviewSubmissionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(ReviewSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Review)
            .FirstOrDefaultAsync(x => x.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
            return Result<Guid>.Failure("Teslim bulunamadi");

        if (submission.Assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<Guid>.Failure("Bu teslimi degerlendirme yetkiniz yok");

        // Remove existing review if any
        if (submission.Review != null)
        {
            _context.SubmissionReviews.Remove(submission.Review);
        }

        var review = SubmissionReview.Create(
            request.SubmissionId,
            _currentUser.UserId.Value,
            request.Score,
            request.Feedback,
            request.ReviewStatus);

        _context.SubmissionReviews.Add(review);

        // Update submission status based on review
        if (request.ReviewStatus == ReviewStatus.NeedsRevision)
            submission.MarkReturned();
        else
            submission.MarkReviewed();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(review.Id);
    }
}
