using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Library.Commands.DeleteLibraryItem;

public record DeleteLibraryItemCommand(Guid Id) : IRequest<Result>;

public class DeleteLibraryItemCommandHandler : IRequestHandler<DeleteLibraryItemCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteLibraryItemCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteLibraryItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var item = await _context.LibraryItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (item == null)
            return Result.Failure("Library item not found");

        if (item.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only delete your own library items");

        item.Delete();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
