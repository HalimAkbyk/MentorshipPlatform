using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetMessageReports;

public record MessageReportDto(
    Guid Id,
    Guid MessageId,
    string MessageContent,
    string SenderName,
    Guid ReporterUserId,
    string ReporterName,
    string Reason,
    string Status,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

public record GetMessageReportsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<MessageReportDto>>>;

public class GetMessageReportsQueryHandler
    : IRequestHandler<GetMessageReportsQuery, Result<PaginatedList<MessageReportDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMessageReportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<MessageReportDto>>> Handle(
        GetMessageReportsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.MessageReports
            .AsNoTracking()
            .Include(r => r.Message)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<ReportStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var orderedQuery = query.OrderByDescending(r => r.CreatedAt);
        var paginated = await orderedQuery.ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with user names
        var userIds = paginated.Items
            .SelectMany(r => new[] { r.Message.SenderUserId, r.ReporterUserId })
            .Distinct().ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var dtos = paginated.Items.Select(r =>
        {
            users.TryGetValue(r.Message.SenderUserId, out var senderName);
            users.TryGetValue(r.ReporterUserId, out var reporterName);

            return new MessageReportDto(
                r.Id,
                r.MessageId,
                r.Message.Content,
                senderName ?? "Bilinmeyen",
                r.ReporterUserId,
                reporterName ?? "Bilinmeyen",
                r.Reason,
                r.Status.ToString(),
                r.AdminNotes,
                r.CreatedAt,
                r.ReviewedAt);
        }).ToList();

        return Result<PaginatedList<MessageReportDto>>.Success(
            new PaginatedList<MessageReportDto>(dtos, paginated.TotalCount, paginated.PageNumber, paginated.PageSize));
    }
}
