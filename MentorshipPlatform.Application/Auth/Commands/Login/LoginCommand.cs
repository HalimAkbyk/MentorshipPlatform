using MediatR;
using MentorshipPlatform.Application.Auth.Commands.RegisterUser;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
            return Result<AuthResponse>.Failure("E-posta veya şifre hatalı");

        // Social-only accounts have no password
        if (string.IsNullOrEmpty(user.PasswordHash))
            return Result<AuthResponse>.Failure("Bu hesap sosyal giriş ile oluşturulmuş. Lütfen sosyal giriş butonlarını kullanın.");

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash!, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result<AuthResponse>.Failure("E-posta veya şifre hatalı");

        if (user.Status != UserStatus.Active)
            return Result<AuthResponse>.Failure("Hesabınız aktif değil");

        var (accessToken, refreshToken) = _jwtTokenGenerator.GenerateTokens(
            user.Id, user.Email!, user.Roles.ToArray());

        var response = new AuthResponse(
            user.Id,
            accessToken,
            refreshToken,
            user.Roles.ToArray());

        return Result<AuthResponse>.Success(response);
    }
}