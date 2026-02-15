using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Auth.Commands.RegisterUser;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Auth.Commands.ExternalLogin;

public record ExternalLoginCommand(
    string Provider,
    string Token,
    string? Code,
    string? RedirectUri,
    string? DisplayName,
    UserRole? InitialRole) : IRequest<Result<ExternalLoginResponse>>;

public record ExternalLoginResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    UserRole[] Roles,
    bool IsNewUser,
    string? PendingToken = null);

public class ExternalLoginCommandValidator : AbstractValidator<ExternalLoginCommand>
{
    private static readonly string[] SupportedProviders = { "google", "linkedin" };

    public ExternalLoginCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .Must(p => SupportedProviders.Contains(p.ToLowerInvariant()))
            .WithMessage("Desteklenmeyen sağlayıcı. Desteklenen: google, linkedin");

        // Token or Code must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.Token) || !string.IsNullOrEmpty(x.Code))
            .WithMessage("Token veya Code gereklidir");
    }
}

public class ExternalLoginCommandHandler : IRequestHandler<ExternalLoginCommand, Result<ExternalLoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IExternalAuthService _externalAuthService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly Microsoft.Extensions.Logging.ILogger<ExternalLoginCommandHandler> _logger;

    public ExternalLoginCommandHandler(
        IApplicationDbContext context,
        IExternalAuthService externalAuthService,
        IJwtTokenGenerator jwtTokenGenerator,
        Microsoft.Extensions.Logging.ILogger<ExternalLoginCommandHandler> logger)
    {
        _context = context;
        _externalAuthService = externalAuthService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<Result<ExternalLoginResponse>> Handle(
        ExternalLoginCommand request,
        CancellationToken cancellationToken)
    {
        var provider = request.Provider.ToLowerInvariant();

        // 1. Validate external token or exchange code
        ExternalUserInfo? externalUser;
        if (!string.IsNullOrEmpty(request.Code))
        {
            externalUser = await _externalAuthService.ExchangeCodeAsync(
                provider, request.Code, request.RedirectUri ?? "");
        }
        else
        {
            externalUser = await _externalAuthService.ValidateTokenAsync(provider, request.Token!);
        }

        if (externalUser == null)
            return Result<ExternalLoginResponse>.Failure("Sosyal giriş doğrulanamadı. Lütfen tekrar deneyin.");

        // 2. Try to find user by provider + externalId
        var user = await _context.Users
            .Include(u => u.MentorProfile)
            .FirstOrDefaultAsync(
                u => u.ExternalProvider == provider && u.ExternalId == externalUser.ExternalId,
                cancellationToken);

        bool isNewUser = false;

        if (user == null)
        {
            // 3. Try to find by email (link existing account)
            user = await _context.Users
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Email == externalUser.Email, cancellationToken);

            if (user != null)
            {
                // Link external provider to existing account
                user.LinkExternalProvider(provider, externalUser.ExternalId);
            }
            else
            {
                // 4. Create new user — InitialRole required
                if (request.InitialRole == null)
                {
                    // Return a success response with PendingToken instead of a failure.
                    // This avoids HTTP 400 and the global error interceptor toast.
                    // Frontend detects PendingToken != null → shows role selection UI.
                    var newUserPt = !string.IsNullOrEmpty(request.Token)
                        ? request.Token
                        : !string.IsNullOrEmpty(externalUser.ProviderAccessToken)
                            ? externalUser.ProviderAccessToken
                            : "ROLE_REQUIRED";
                    return Result<ExternalLoginResponse>.Success(new ExternalLoginResponse(
                        Guid.Empty,
                        "",
                        "",
                        Array.Empty<UserRole>(),
                        false,
                        PendingToken: newUserPt));
                }

                var displayName = request.DisplayName ?? externalUser.DisplayName;
                user = User.CreateExternal(
                    externalUser.Email,
                    displayName,
                    externalUser.AvatarUrl,
                    provider,
                    externalUser.ExternalId);

                user.AddRole(request.InitialRole.Value);
                _context.Users.Add(user);
                isNewUser = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        // 5. If user has no roles, require role selection
        _logger.LogInformation(
            "ExternalLogin user {UserId} email={Email} rolesCount={RolesCount} roles=[{Roles}] initialRole={InitialRole}",
            user.Id, user.Email, user.Roles.Count,
            string.Join(",", user.Roles),
            request.InitialRole);

        if (!user.Roles.Any())
        {
            if (request.InitialRole != null)
            {
                // Role provided — assign it now
                user.AddRole(request.InitialRole.Value);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // No role yet — ask frontend to show role selection
                // Use a guaranteed non-empty sentinel if no real token is available
                var pt = !string.IsNullOrEmpty(request.Token)
                    ? request.Token
                    : !string.IsNullOrEmpty(externalUser?.ProviderAccessToken)
                        ? externalUser.ProviderAccessToken
                        : "ROLE_REQUIRED";
                _logger.LogWarning(
                    "ExternalLogin returning pendingToken for role-less user {UserId}, token length={TokenLen}, requestToken length={ReqLen}",
                    user.Id, pt?.Length ?? 0, request.Token?.Length ?? 0);
                return Result<ExternalLoginResponse>.Success(new ExternalLoginResponse(
                    Guid.Empty,
                    "",
                    "",
                    Array.Empty<UserRole>(),
                    false,
                    PendingToken: pt));
            }
        }

        // 6. Check if account is active
        if (user.Status != UserStatus.Active)
            return Result<ExternalLoginResponse>.Failure("Hesap aktif değil");

        // 7. Generate JWT
        _logger.LogInformation(
            "ExternalLogin generating JWT for user {UserId} with roles [{Roles}]",
            user.Id, string.Join(",", user.Roles));
        var (accessToken, refreshToken) = _jwtTokenGenerator.GenerateTokens(
            user.Id, user.Email!, user.Roles.ToArray());

        return Result<ExternalLoginResponse>.Success(new ExternalLoginResponse(
            user.Id,
            accessToken,
            refreshToken,
            user.Roles.ToArray(),
            isNewUser));
    }
}
