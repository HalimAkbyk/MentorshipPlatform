using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Application.Messages.Queries.GetBookingMessages;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetConversationMessages;

public record GetConversationMessagesQuery(
    Guid ConversationId,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PaginatedList<MessageDto>>>;

public class GetConversationMessagesQueryHandler
    : IRequestHandler<GetConversationMessagesQuery, Result<PaginatedList<MessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetConversationMessagesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MessageDto>>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<PaginatedList<MessageDto>>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation == null)
            return Result<PaginatedList<MessageDto>>.Failure("Konuşma bulunamadı.");

        if (!conversation.IsParticipant(userId))
            return Result<PaginatedList<MessageDto>>.Failure("Bu konuşmanın mesajlarını görme yetkiniz yok.");

        var query = _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderBy(m => m.CreatedAt);

        var paginated = await query.ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with sender names
        var senderIds = paginated.Items.Select(m => m.SenderUserId).Distinct().ToList();
        var senders = await _context.Users
            .AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl }, cancellationToken);

        var dtos = paginated.Items.Select(m =>
        {
            senders.TryGetValue(m.SenderUserId, out var sender);
            return new MessageDto(
                m.Id,
                m.SenderUserId,
                sender?.DisplayName ?? "Bilinmeyen",
                sender?.AvatarUrl,
                m.Content,
                m.IsRead,
                m.SenderUserId == userId,
                m.CreatedAt,
                m.DeliveredAt,
                m.ReadAt);
        }).ToList();

        return Result<PaginatedList<MessageDto>>.Success(
            new PaginatedList<MessageDto>(dtos, paginated.TotalCount, paginated.PageNumber, paginated.PageSize));
    }
}
