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
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IDemoWorkspaceSeeder demoSeeder,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _demoSeeder = demoSeeder;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not null)
        {
            await _userManager.UpdateSecurityStampAsync(user);
            await _refreshTokenService.RevokeAllAsync(userId);
        }

        _logger.LogInformation("AUTH_LOGOUT UserId={UserId} CorrelationId={CorrelationId}",
            userId, correlationId);
        return NoContent();
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
