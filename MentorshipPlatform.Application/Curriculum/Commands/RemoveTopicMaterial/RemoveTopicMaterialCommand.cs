using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.RemoveTopicMaterial;

public record RemoveTopicMaterialCommand(Guid TopicId, Guid LibraryItemId) : IRequest<Result>;

public class RemoveTopicMaterialCommandHandler : IRequestHandler<RemoveTopicMaterialCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveTopicMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RemoveTopicMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var material = await _context.CurriculumTopicMaterials
            .Include(x => x.Topic)
                .ThenInclude(x => x.Week)
                    .ThenInclude(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.CurriculumTopicId == request.TopicId && x.LibraryItemId == request.LibraryItemId, cancellationToken);

        if (material == null)
            return Result.Failure("Materyal baglantisi bulunamadi");

        if (material.Topic.Week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizdaki materyalleri kaldirabilirsiniz");

        _context.CurriculumTopicMaterials.Remove(material);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
