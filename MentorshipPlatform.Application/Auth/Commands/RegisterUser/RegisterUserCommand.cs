using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Auth.Commands.RegisterUser;

public record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    UserRole InitialRole) : IRequest<Result<AuthResponse>>;
public record AuthResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    UserRole[] Roles);

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RegisterUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // Check if email exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser != null)
            return Result<AuthResponse>.Failure("Email already registered");

        // Create user
        var passwordHash = _passwordHasher.HashPassword(null!, request.Password);
        var user = User.Create(request.Email, request.DisplayName, passwordHash);
        user.AddRole(request.InitialRole);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var (accessToken, refreshToken) = _jwtTokenGenerator.GenerateTokens(
            user.Id, user.Email!, new[] { request.InitialRole });

        var response = new AuthResponse(
            user.Id,
            accessToken,
            refreshToken,
            new[] { request.InitialRole });

        return Result<AuthResponse>.Success(response);
    }
}