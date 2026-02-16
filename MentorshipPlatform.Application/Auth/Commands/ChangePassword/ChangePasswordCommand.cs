using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MentorshipPlatform.Application.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Result<bool>>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut şifre gereklidir");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre gereklidir")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<bool>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("User not found");

        // Social-only accounts have no password
        if (string.IsNullOrEmpty(user.PasswordHash))
            return Result<bool>.Failure("Bu hesap sosyal giriş ile oluşturulmuş. Şifre değişikliği yapılamaz.");

        // Verify current password
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash!, request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result<bool>.Failure("Mevcut şifre yanlış");

        // Hash and set new password
        var newHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.ChangePassword(newHash);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
