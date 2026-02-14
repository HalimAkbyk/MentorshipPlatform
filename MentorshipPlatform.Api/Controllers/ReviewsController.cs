using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Reviews.Commands.CreateReview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;
[ApiController]
[Route("api/reviews")]
public class ReviewsController: ControllerBase {

    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public ReviewsController(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { reviewId = result.Data });
    }

    [HttpGet("mentors/{mentorId}")]
    public async Task<IActionResult> GetMentorReviews(Guid mentorId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.AuthorUser)
            .Where(r => r.MentorUserId == mentorId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id,
                AuthorName = r.AuthorUser.DisplayName,
                AuthorAvatar = r.AuthorUser.AvatarUrl,
                r.Rating,
                r.Comment,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("mentors/{mentorId}/summary")]
    public async Task<IActionResult> GetRatingSummary(Guid mentorId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.MentorUserId == mentorId)
            .ToListAsync();

        var distribution = reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new {
            AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
            TotalReviews = reviews.Count,
            RatingDistribution = new {
                Five = distribution.GetValueOrDefault(5, 0),
                Four = distribution.GetValueOrDefault(4, 0),
                Three = distribution.GetValueOrDefault(3, 0),
                Two = distribution.GetValueOrDefault(2, 0),
                One = distribution.GetValueOrDefault(1, 0)
            }
        });
    }
}