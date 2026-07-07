using System.Security.Claims;
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
    private readonly IEmailSender _emailSender;
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
        IEmailSender emailSender,
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
        _emailSender = emailSender;
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

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("AUTH_LOGIN_BLOCKED Email={Email} CorrelationId={CorrelationId}",
                request.Email, correlationId);
            return StatusCode(StatusCodes.Status429TooManyRequests, "Account temporarily locked. Please try again later.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
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

            _logger.LogWarning("AUTH_LOGIN_FAILED Email={Email} Attempts={Attempts} CorrelationId={CorrelationId}",
                request.Email, failCount, correlationId);
            return Unauthorized("Invalid credentials.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("AUTH_LOGIN_SUCCESS UserId={UserId} Email={Email} CorrelationId={CorrelationId}",
            user.Id, request.Email, correlationId);

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

                var emailSent = await TrySendEmailAsync(
                    user.Email,
                    "Reset your Planora password",
                    $"<p>We received a request to reset your password.</p>" +
                    $"<p><a href=\"{link}\">Reset your password</a></p>" +
                    $"<p>This link expires in 1 hour. If you didn't request this, you can ignore this email.</p>",
                    HttpContext.RequestAborted);

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

    private async Task<bool> SendEmailConfirmationAsync(AppUser user, string? correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{GetWebBaseUrl()}/confirm-email" +
                   $"?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var sent = await TrySendEmailAsync(
            user.Email,
            "Verify your Planora email",
            $"<p>Welcome to Planora.</p>" +
            $"<p><a href=\"{link}\">Verify your email</a></p>" +
            $"<p>This link expires in 1 hour. If you didn't create this account, you can ignore this email.</p>",
            ct);

        _logger.LogInformation("AUTH_SEND_EMAIL_CONFIRMATION UserId={UserId} EmailSent={EmailSent} CorrelationId={CorrelationId}",
            user.Id, sent, correlationId);

        return sent;
    }

    private async Task<bool> TrySendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            await _emailSender.SendAsync(toEmail, subject, htmlBody, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EMAIL_SEND_FAILED To={ToEmail} Subject={Subject}", toEmail, subject);
            return false;
        }
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
