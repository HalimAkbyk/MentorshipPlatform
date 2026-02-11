using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Reviews.Commands.CreateReview;

public record CreateReviewCommand(
    Guid BookingId,
    int Rating,
    string? Comment) : IRequest<Result<Guid>>;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public class CreateReviewCommandHandler 
    : IRequestHandler<CreateReviewCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        CreateReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        // Validate booking
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result<Guid>.Failure("Booking not found");

        if (booking.StudentUserId != studentUserId)
            return Result<Guid>.Failure("Unauthorized");

        if (booking.Status != BookingStatus.Completed)
            return Result<Guid>.Failure("Can only review completed sessions");

        // Check if already reviewed
        var existingReview = await _context.Reviews
            .AnyAsync(r => 
                r.ResourceType == "Booking" && 
                r.ResourceId == request.BookingId,
                cancellationToken);

        if (existingReview)
            return Result<Guid>.Failure("Session already reviewed");

        // Create review
        var review = Review.Create(
            studentUserId,
            booking.MentorUserId,
            "Booking",
            request.BookingId,
            request.Rating,
            request.Comment);

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(review.Id);
    }
}