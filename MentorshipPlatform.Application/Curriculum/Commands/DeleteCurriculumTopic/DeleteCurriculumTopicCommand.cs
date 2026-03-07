using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculumTopic;

public record DeleteCurriculumTopicCommand(Guid TopicId) : IRequest<Result>;

public class DeleteCurriculumTopicCommandHandler : IRequestHandler<DeleteCurriculumTopicCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteCurriculumTopicCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCurriculumTopicCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var topic = await _context.CurriculumTopics
            .Include(x => x.Week)
                .ThenInclude(x => x.Curriculum)
            .FirstOrDefaultAsync(x => x.Id == request.TopicId, cancellationToken);

        if (topic == null)
            return Result.Failure("Konu bulunamadi");

        if (topic.Week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizdaki konulari silebilirsiniz");

        _context.CurriculumTopics.Remove(topic);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
