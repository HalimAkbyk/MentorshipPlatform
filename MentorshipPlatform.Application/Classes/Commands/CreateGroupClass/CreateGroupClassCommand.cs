using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Application.Classes.Commands.CreateGroupClass;

public record CreateGroupClassCommand(
    string Title,
    string? Description,
    string Category,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    decimal PricePerSeat,
    string? CoverImageUrl) : IRequest<Result<Guid>>;

public class CreateGroupClassCommandValidator : AbstractValidator<CreateGroupClassCommand>
{
    public CreateGroupClassCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().WithMessage("Kategori zorunludur.").MaximumLength(100);
        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow.AddHours(1))
            .WithMessage("Ders başlangıç saati en az 1 saat sonrası olmalıdır.");
        RuleFor(x => x.EndAt)
            .GreaterThan(x => x.StartAt)
            .WithMessage("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        RuleFor(x => x.Capacity).InclusiveBetween(2, 100).WithMessage("Kontenjan 2-100 arasında olmalıdır.");
        RuleFor(x => x.PricePerSeat).GreaterThan(0).WithMessage("Ücret 0'dan büyük olmalıdır.");
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
            request.Description,
            request.Category,
            request.StartAt,
            request.EndAt,
            request.Capacity,
            request.PricePerSeat,
            request.CoverImageUrl);

        groupClass.Publish();

        _context.GroupClasses.Add(groupClass);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(groupClass.Id);
    }
}
