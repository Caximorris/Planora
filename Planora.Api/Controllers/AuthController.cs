using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Auth;

namespace Planora.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDemoWorkspaceSeeder _demoSeeder;
    private readonly ITransactionalEmailService _email;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
    private readonly IValidator<ConfirmEmailRequest> _confirmEmailValidator;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IDemoWorkspaceSeeder demoSeeder,
        ITransactionalEmailService email,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator,
        IValidator<ConfirmEmailRequest> confirmEmailValidator,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _demoSeeder = demoSeeder;
        _email = email;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _confirmEmailValidator = confirmEmailValidator;
        _config = config;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            return Conflict("An account with this email already exists.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _demoSeeder.SeedAsync(user.Id);

        _logger.LogInformation("AUTH_REGISTER UserId={UserId} Email={Email} CorrelationId={CorrelationId}",
            user.Id, user.Email, HttpContext.Items["CorrelationId"]);

        await SendEmailConfirmationAsync(
            user,
            HttpContext.Items["CorrelationId"] as string,
            HttpContext.RequestAborted);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized("Invalid credentials.");

        var check = await CheckPasswordWithLockoutAsync(user, request.Password, correlationId);
        if (check == CredentialCheck.LockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests, "Account temporarily locked. Please try again later.");
        if (check == CredentialCheck.InvalidCredentials)
            return Unauthorized("Invalid credentials.");

        // Password is correct. If the account has 2FA, stop here — no tokens are issued until the
        // second factor is verified via POST /api/auth/login/2fa. Don't reset the failed-attempt
        // counter yet, so brute-forcing the code still accrues toward lockout.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            _logger.LogInformation("AUTH_LOGIN_2FA_REQUIRED UserId={UserId} CorrelationId={CorrelationId}",
                user.Id, correlationId);
            return Ok(new AuthResponse { RequiresTwoFactor = true });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("AUTH_LOGIN_SUCCESS UserId={UserId} Email={Email} CorrelationId={CorrelationId}",
            user.Id, request.Email, correlationId);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("login/2fa")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request)
    {
        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized("Invalid credentials.");

        // Re-verify the password (with the same lockout) so this endpoint can't be used as a
        // password-less way to test recovery/TOTP codes.
        var check = await CheckPasswordWithLockoutAsync(user, request.Password, correlationId);
        if (check == CredentialCheck.LockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests, "Account temporarily locked. Please try again later.");
        if (check == CredentialCheck.InvalidCredentials)
            return Unauthorized("Invalid credentials.");

        // If 2FA isn't actually enabled, treat this as a normal completed login.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            var codeValid = await VerifySecondFactorAsync(user, request.Code, request.IsRecoveryCode);
            if (!codeValid)
            {
                await ApplyFailedAttemptAsync(user);
                _logger.LogWarning("AUTH_LOGIN_2FA_FAILED UserId={UserId} CorrelationId={CorrelationId}",
                    user.Id, correlationId);
                return Unauthorized("Invalid credentials.");
            }
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("AUTH_LOGIN_2FA_SUCCESS UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, correlationId);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var (isValid, user) = await _refreshTokenService.ValidateAsync(request.RefreshToken);

        if (!isValid || user is null)
        {
            _logger.LogWarning("AUTH_REFRESH_FAILED CorrelationId={CorrelationId}", correlationId);
            return Unauthorized("Invalid or expired refresh token.");
        }

        await _refreshTokenService.RevokeAsync(request.RefreshToken);
        var response = await BuildAuthResponseAsync(user);

        _logger.LogInformation("AUTH_REFRESH_SUCCESS UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, correlationId);
        return Ok(response);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var correlationId = HttpContext.Items["CorrelationId"] as string;

        // Always return the same response whether or not the email exists — no account enumeration.
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is not null && user.Email is not null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = $"{GetWebBaseUrl()}/reset-password" +
                           $"?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

                var emailSent = await _email.SendPasswordResetAsync(user.Email, link, HttpContext.RequestAborted);

                _logger.LogInformation("AUTH_FORGOT_PASSWORD UserId={UserId} EmailSent={EmailSent} CorrelationId={CorrelationId}",
                    user.Id, emailSent, correlationId);
            }
            else
            {
                _logger.LogInformation("AUTH_FORGOT_PASSWORD_UNKNOWN CorrelationId={CorrelationId}", correlationId);
            }
        }

        return Ok(new { message = "If an account exists for that email, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var validation = await _resetPasswordValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Generic failure for a missing user / bad token so neither can be probed.
        const string genericError = "This reset link is invalid or has expired. Please request a new one.";
        if (user is null)
            return BadRequest(genericError);

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            // Surface password-policy problems (actionable); keep token/user failures generic.
            var passwordErrors = result.Errors
                .Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                .Select(e => e.Description)
                .ToList();

            _logger.LogWarning("AUTH_RESET_PASSWORD_FAILED CorrelationId={CorrelationId}", correlationId);
            return BadRequest(passwordErrors.Count > 0 ? passwordErrors : [genericError]);
        }

        // ResetPasswordAsync rotates the SecurityStamp (invalidating existing JWTs). Also revoke
        // refresh tokens and clear any lockout so the user can immediately sign in with the new password.
        await _refreshTokenService.RevokeAllAsync(user.Id);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);

        // Notify the account owner of the change so an unauthorized reset can't go unnoticed.
        if (user.Email is not null)
            await _email.SendPasswordChangedAsync(user.Email, DateTimeOffset.UtcNow, HttpContext.RequestAborted);

        _logger.LogInformation("AUTH_RESET_PASSWORD_SUCCESS UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, correlationId);

        return Ok(new { message = "Your password has been reset. You can now sign in." });
    }

    [Authorize]
    [HttpPost("send-email-confirmation")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SendEmailConfirmation()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        if (user.EmailConfirmed)
            return Ok(new { message = "Email is already verified." });

        var sent = await SendEmailConfirmationAsync(
            user,
            HttpContext.Items["CorrelationId"] as string,
            HttpContext.RequestAborted);

        if (!sent)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Email delivery is not configured. Please try again later.");

        return Ok(new { message = "Verification email sent." });
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var validation = await _confirmEmailValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var user = await _userManager.FindByIdAsync(request.UserId);
        const string genericError = "This verification link is invalid or has expired. Please request a new one.";

        if (user is null)
            return BadRequest(genericError);

        if (user.EmailConfirmed)
            return Ok(new { message = "Email is already verified." });

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            _logger.LogWarning("AUTH_CONFIRM_EMAIL_FAILED UserId={UserId} CorrelationId={CorrelationId}",
                user.Id, correlationId);
            return BadRequest(genericError);
        }

        _logger.LogInformation("AUTH_CONFIRM_EMAIL_SUCCESS UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, correlationId);

        return Ok(new { message = "Email verified." });
    }

    [Authorize]
    [HttpPost("sessions")]
    public async Task<IActionResult> GetSessions([FromBody] SessionListRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var sessions = await _refreshTokenService.GetActiveSessionsAsync(userId);
        return Ok(sessions.Select(s => new RefreshSessionDto
        {
            Id = s.Id,
            CreatedAt = s.CreatedAt,
            ExpiresAt = s.ExpiresAt,
            IsCurrent = !string.IsNullOrWhiteSpace(request.CurrentRefreshToken)
                && s.Token == request.CurrentRefreshToken
        }));
    }

    [Authorize]
    [HttpPost("sessions/revoke")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var revoked = await _refreshTokenService.RevokeSessionAsync(userId, request.SessionId);
        return revoked ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpPost("sessions/revoke-others")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RevokeOtherSessions([FromBody] RevokeOtherSessionsRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CurrentRefreshToken))
            return BadRequest("Current session is required.");

        var revoked = await _refreshTokenService.RevokeOtherSessionsAsync(userId, request.CurrentRefreshToken);
        return revoked ? NoContent() : BadRequest("Current session is invalid or expired.");
    }

    [HttpPost("demo")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Demo()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        string[] names = ["Alex", "Sam", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Avery", "Drew", "Quinn"];
        var displayName = names[Random.Shared.Next(names.Length)];
        var email = $"guest.{uid}@planora.demo";
        var password = $"Demo1{uid}Xyz";

        if (await _userManager.FindByEmailAsync(email) is not null)
            return StatusCode(500, "Demo account collision — please retry.");

        var user = new AppUser { UserName = email, Email = email, DisplayName = displayName };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return StatusCode(500, result.Errors.First().Description);

        await _demoSeeder.SeedAsync(user.Id);

        _logger.LogInformation("AUTH_DEMO UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, HttpContext.Items["CorrelationId"]);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var correlationId = HttpContext.Items["CorrelationId"] as string;
        string? userId = null;

        // Prefer the authenticated JWT path (access token still valid)
        if (User.Identity?.IsAuthenticated == true)
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        // Fall back to refresh token when access token is expired or missing.
        // GetUserIdAsync does not trigger reuse-detection side effects unlike ValidateAsync.
        else if (!string.IsNullOrEmpty(request?.RefreshToken))
        {
            userId = await _refreshTokenService.GetUserIdAsync(request.RefreshToken);
        }

        if (userId is not null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
                await _userManager.UpdateSecurityStampAsync(user);
            // RevokeAllAsync runs even when FindByIdAsync returns null (deleted user)
            await _refreshTokenService.RevokeAllAsync(userId);
        }

        _logger.LogInformation("AUTH_LOGOUT UserId={UserId} CorrelationId={CorrelationId}",
            userId ?? "unknown", correlationId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("2fa/status")]
    public async Task<IActionResult> TwoFactorStatus()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        return Ok(new TwoFactorStatusResponse
        {
            Enabled = await _userManager.GetTwoFactorEnabledAsync(user),
            RecoveryCodesRemaining = await _userManager.CountRecoveryCodesAsync(user)
        });
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactorSetup()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Two-factor authentication is already enabled.");

        // Ensure an authenticator key exists (generating one rotates the SecurityStamp; harmless here
        // since 2FA isn't enabled yet and the client auto-refreshes on the next request).
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = FormatKey(key!),
            AuthenticatorUri = BuildAuthenticatorUri(user.Email!, key!)
        });
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactorEnable([FromBody] EnableTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Two-factor authentication is already enabled.");

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, Sanitize(request.Code));
        if (!valid)
            return BadRequest("That code is invalid or expired. Please try again.");

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        if (user.Email is not null)
            await _email.SendTwoFactorChangedAsync(user.Email, enabled: true, DateTimeOffset.UtcNow, HttpContext.RequestAborted);

        _logger.LogInformation("AUTH_2FA_ENABLED UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, HttpContext.Items["CorrelationId"]);

        return Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = recoveryCodes?.ToList() ?? [] });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactorDisable([FromBody] DisableTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Two-factor authentication is not enabled.");

        // Re-verify a second factor so a stolen access token can't silently weaken the account.
        if (!await VerifySecondFactorAsync(user, request.Code, request.IsRecoveryCode))
            return BadRequest("That code is invalid or expired. Please try again.");

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        if (user.Email is not null)
            await _email.SendTwoFactorChangedAsync(user.Email, enabled: false, DateTimeOffset.UtcNow, HttpContext.RequestAborted);

        _logger.LogInformation("AUTH_2FA_DISABLED UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, HttpContext.Items["CorrelationId"]);

        return NoContent();
    }

    [Authorize]
    [HttpPost("2fa/recovery-codes")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] DisableTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Two-factor authentication is not enabled.");

        if (!await VerifySecondFactorAsync(user, request.Code, request.IsRecoveryCode))
            return BadRequest("That code is invalid or expired. Please try again.");

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        _logger.LogInformation("AUTH_2FA_RECOVERY_REGENERATED UserId={UserId} CorrelationId={CorrelationId}",
            user.Id, HttpContext.Items["CorrelationId"]);

        return Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = recoveryCodes?.ToList() ?? [] });
    }

    private enum CredentialCheck { Ok, InvalidCredentials, LockedOut }

    // Shared password + progressive-lockout gate used by both the password step and the 2FA step, so
    // lockout behavior can never diverge between them.
    private async Task<CredentialCheck> CheckPasswordWithLockoutAsync(AppUser user, string password, string? correlationId)
    {
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("AUTH_LOGIN_BLOCKED Email={Email} CorrelationId={CorrelationId}",
                user.Email, correlationId);
            return CredentialCheck.LockedOut;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            await ApplyFailedAttemptAsync(user);
            _logger.LogWarning("AUTH_LOGIN_FAILED Email={Email} CorrelationId={CorrelationId}",
                user.Email, correlationId);
            return CredentialCheck.InvalidCredentials;
        }

        return CredentialCheck.Ok;
    }

    // Records a failed attempt and applies the manual progressive lockout (3–4 → 5m, 5–7 → 15m,
    // 8–9 → 1h, 10+ → 24h). Shared by wrong-password and wrong-2FA-code paths.
    private async Task ApplyFailedAttemptAsync(AppUser user)
    {
        var failCountBefore = user.AccessFailedCount;
        await _userManager.AccessFailedAsync(user);
        var failCount = failCountBefore + 1;

        if (failCount >= 3)
        {
            var duration = failCount switch
            {
                >= 10 => TimeSpan.FromHours(24),
                >= 8  => TimeSpan.FromHours(1),
                >= 5  => TimeSpan.FromMinutes(15),
                _     => TimeSpan.FromMinutes(5)
            };
            // Re-fetch to avoid EF tracking conflicts after AccessFailedAsync
            var freshUser = await _userManager.FindByIdAsync(user.Id);
            await _userManager.SetLockoutEndDateAsync(freshUser!, DateTimeOffset.UtcNow.Add(duration));
        }
    }

    private async Task<bool> VerifySecondFactorAsync(AppUser user, string code, bool isRecoveryCode)
    {
        if (isRecoveryCode)
            // Recovery codes are stored with their exact formatting (e.g. "xxxxx-xxxxx"); only trim
            // surrounding whitespace so the value still matches what was issued.
            return (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, (code ?? string.Empty).Trim())).Succeeded;

        return await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, Sanitize(code));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await _userManager.FindByIdAsync(userId);
    }

    private static string Sanitize(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

    // Group the base32 secret into 4-char chunks for readable manual entry.
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        for (var i = 0; i < unformattedKey.Length; i += 4)
        {
            if (i > 0) result.Append(' ');
            result.Append(unformattedKey.AsSpan(i, Math.Min(4, unformattedKey.Length - i)));
        }
        return result.ToString().ToUpperInvariant();
    }

    private static string BuildAuthenticatorUri(string email, string unformattedKey) =>
        $"otpauth://totp/{Uri.EscapeDataString("Planora")}:{Uri.EscapeDataString(email)}" +
        $"?secret={unformattedKey}&issuer={Uri.EscapeDataString("Planora")}&digits=6";

    private async Task<bool> SendEmailConfirmationAsync(AppUser user, string? correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{GetWebBaseUrl()}/confirm-email" +
                   $"?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var sent = await _email.SendEmailVerificationAsync(user.Email, link, ct);

        _logger.LogInformation("AUTH_SEND_EMAIL_CONFIRMATION UserId={UserId} EmailSent={EmailSent} CorrelationId={CorrelationId}",
            user.Id, sent, correlationId);

        return sent;
    }

    // Base URL of the Blazor client, used to build password-reset links. Prefer an explicit
    // App:WebBaseUrl; otherwise fall back to the first configured CORS origin.
    private string GetWebBaseUrl()
    {
        var explicitUrl = _config["App:WebBaseUrl"];
        if (!string.IsNullOrWhiteSpace(explicitUrl))
            return explicitUrl.TrimEnd('/');

        var corsOrigin = _config.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?.FirstOrDefault(origin => !string.IsNullOrWhiteSpace(origin));

        if (string.IsNullOrWhiteSpace(corsOrigin))
        {
            corsOrigin = _config["Cors:AllowedOrigins"]
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(origin => !string.IsNullOrWhiteSpace(origin));
        }

        if (!string.IsNullOrWhiteSpace(corsOrigin))
            return corsOrigin.TrimEnd('/');

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}".TrimEnd('/');
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user) => new()
    {
        Token = _tokenService.GenerateToken(user),
        RefreshToken = await _refreshTokenService.CreateAsync(user.Id),
        UserId = user.Id,
        Email = user.Email!,
        DisplayName = user.DisplayName,
        ExpiresAt = _tokenService.GetExpiration()
    };
}
