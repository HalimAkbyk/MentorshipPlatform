using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.StartDirectConversation;

public record StartDirectConversationCommand(
    Guid RecipientUserId) : IRequest<Result<DirectConversationResult>>;

public record DirectConversationResult(
    Guid ConversationId,
    Guid OtherUserId,
    string OtherUserName,
    string? OtherUserAvatar,
    bool IsNew);

public class StartDirectConversationCommandValidator : AbstractValidator<StartDirectConversationCommand>
{
    public StartDirectConversationCommandValidator()
    {
        RuleFor(x => x.RecipientUserId).NotEmpty();
    }
}

public class StartDirectConversationCommandHandler
    : IRequestHandler<StartDirectConversationCommand, Result<DirectConversationResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StartDirectConversationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<DirectConversationResult>> Handle(
        StartDirectConversationCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<DirectConversationResult>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        if (userId == request.RecipientUserId)
            return Result<DirectConversationResult>.Failure("Kendinizle konuşma başlatamazsınız.");

        // Check recipient user exists
        var recipientUser = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.RecipientUserId)
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipientUser == null)
            return Result<DirectConversationResult>.Failure("Kullanıcı bulunamadı.");

        // Check if a direct conversation already exists between these users
        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.Type == ConversationType.Direct &&
                ((c.User1Id == userId && c.User2Id == request.RecipientUserId) ||
                 (c.User1Id == request.RecipientUserId && c.User2Id == userId)),
                cancellationToken);

        if (existingConversation != null)
        {
            return Result<DirectConversationResult>.Success(new DirectConversationResult(
                existingConversation.Id,
                recipientUser.Id,
                recipientUser.DisplayName,
                recipientUser.AvatarUrl,
                IsNew: false));
        }

        // Create new direct conversation
        var conversation = Conversation.CreateDirect(userId, request.RecipientUserId);
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<DirectConversationResult>.Success(new DirectConversationResult(
            conversation.Id,
            recipientUser.Id,
            recipientUser.DisplayName,
            recipientUser.AvatarUrl,
            IsNew: true));
    }
}
