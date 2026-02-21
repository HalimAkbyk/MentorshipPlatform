using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("İsim soyisim zorunludur.")
            .MaximumLength(100).WithMessage("İsim soyisim en fazla 100 karakter olabilir.");
    }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // Check if email exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser != null)
            return Result<AuthResponse>.Failure("Bu e-posta adresi zaten kayıtlı");

        // Create user
        var passwordHash = _passwordHasher.HashPassword(null!, request.Password);
        var user = User.Create(request.Email, request.DisplayName, passwordHash);
        user.AddRole(request.InitialRole);

        // Mentor seçen kullanıcıya otomatik olarak Student rolünü de ekle
        // Böylece mentor, diğer mentorların kurslarına kayıt olabilir ve birebir seans alabilir
        if (request.InitialRole == UserRole.Mentor)
        {
            user.AddRole(UserRole.Student);
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Generate tokens — tüm rolleri dahil et
        var allRoles = user.Roles.ToArray();
        var (accessToken, refreshToken) = _jwtTokenGenerator.GenerateTokens(
            user.Id, user.Email!, allRoles);

        // Send welcome email
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.Welcome,
                user.Email!,
                new Dictionary<string, string>
                {
                    ["displayName"] = user.DisplayName
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
        }

        var response = new AuthResponse(
            user.Id,
            accessToken,
            refreshToken,
            allRoles);

        return Result<AuthResponse>.Success(response);
    }
}