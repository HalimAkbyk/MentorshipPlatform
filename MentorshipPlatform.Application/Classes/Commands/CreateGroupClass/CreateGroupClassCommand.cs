using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Application.Classes.Commands.CreateGroupClass;

public record CreateGroupClassCommand(
    string Title,
    string Description,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    decimal PricePerSeat) : IRequest<Result<Guid>>;

public class CreateGroupClassCommandValidator : AbstractValidator<CreateGroupClassCommand>
{
    public CreateGroupClassCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartAt).GreaterThan(DateTime.UtcNow.AddHours(24));
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
        RuleFor(x => x.Capacity).InclusiveBetween(2, 100);
        RuleFor(x => x.PricePerSeat).GreaterThan(0);
    }
}

public class CreateGroupClassCommandHandler 
    : IRequestHandler<CreateGroupClassCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateGroupClassCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        CreateGroupClassCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        var groupClass = GroupClass.Create(
            mentorUserId,
            request.Title,
            request.StartAt,
            request.EndAt,
            request.Capacity,
            request.PricePerSeat);

        groupClass.Publish();

        _context.GroupClasses.Add(groupClass);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(groupClass.Id);
    }
}