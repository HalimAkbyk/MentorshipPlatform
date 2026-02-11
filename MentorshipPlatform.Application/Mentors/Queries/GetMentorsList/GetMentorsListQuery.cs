using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Mentors.Queries.GetMentorsList;

public record GetMentorsListQuery(
    string? SearchTerm,
    string? University,
    decimal? MinPrice,
    decimal? MaxPrice,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<MentorListDto>>>;

public record MentorListDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string University,
    string Department,
    decimal RatingAvg,
    int RatingCount,
    decimal? HourlyRate,
    bool IsVerified);

public class GetMentorsListQueryHandler 
    : IRequestHandler<GetMentorsListQuery, Result<PaginatedList<MentorListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMentorsListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<MentorListDto>>> Handle(
        GetMentorsListQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.MentorProfiles
            .Include(m => m.User)
            .Include(m => m.Offerings)
            .Include(m => m.Verifications)
            .Where(m => m.IsListed && m.User.Status == UserStatus.Active);

        // Filters
        if (!string.IsNullOrEmpty(request.University))
            query = query.Where(m => m.University.Contains(request.University));

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            query = query.Where(m =>
                m.User.DisplayName.Contains(request.SearchTerm) ||
                m.University.Contains(request.SearchTerm) ||
                m.Department.Contains(request.SearchTerm));
        }

        // Price filter on offerings
        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
        {
            query = query.Where(m => m.Offerings.Any(o =>
                o.IsActive &&
                (!request.MinPrice.HasValue || o.PriceAmount >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || o.PriceAmount <= request.MaxPrice.Value)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var mentors = await query
            .OrderByDescending(m => m.RatingAvg)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new MentorListDto(
                m.UserId,
                m.User.DisplayName,
                m.User.AvatarUrl,
                m.University,
                m.Department,
                m.RatingAvg,
                m.RatingCount,
                m.Offerings.Where(o => o.IsActive).Min(o => (decimal?)o.PriceAmount),
                m.Verifications.Any(v => v.Status == VerificationStatus.Approved)))
            .ToListAsync(cancellationToken);

        var result = new PaginatedList<MentorListDto>(
            mentors, totalCount, request.Page, request.PageSize);

        return Result<PaginatedList<MentorListDto>>.Success(result);
    }
}