using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.UpdateInstructorStatus;

public record UpdateInstructorStatusCommand(
    Guid UserId,
    string Status // Active, Suspended, Removed
) : IRequest<Result>;

public class UpdateInstructorStatusCommandValidator : AbstractValidator<UpdateInstructorStatusCommand>
{
    public UpdateInstructorStatusCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<InstructorStatus>(s, true, out _))
            .WithMessage("Gecerli durumlar: Active, Suspended, Removed");
    }
}

public class UpdateInstructorStatusCommandHandler : IRequestHandler<UpdateInstructorStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateInstructorStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateInstructorStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            return Result.Failure("Kullanici bulunamadi");

        if (!user.InstructorAssignedAt.HasValue)
            return Result.Failure("Bu kullanici egitmen olarak atanmamis");

        var newStatus = Enum.Parse<InstructorStatus>(request.Status, true);
        var oldStatus = user.InstructorStatus?.ToString() ?? "None";

        switch (newStatus)
        {
            case InstructorStatus.Active:
                user.AssignAsInstructor();
                break;
            case InstructorStatus.Suspended:
                user.SuspendInstructor();
                break;
            case InstructorStatus.Removed:
                user.RemoveInstructor();
                break;
        }

        var history = ProcessHistory.Create(
            entityType: "User",
            entityId: request.UserId,
            action: "InstructorStatusChanged",
            oldValue: oldStatus,
            newValue: newStatus.ToString(),
            description: $"Egitmen durumu degistirildi: {user.DisplayName} ({oldStatus} -> {newStatus})",
            performedBy: _currentUser.UserId,
            performedByRole: "Admin");

        _context.ProcessHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
