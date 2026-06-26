using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Users;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;

    public UsersController(UserManager<AppUser> userManager, IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPut("profile")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var name = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 50)
            return BadRequest("Display name must be between 2 and 50 characters.");

        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        user.DisplayName = name;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(new { displayName = user.DisplayName });
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest("Current password is required.");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest("New password must be at least 6 characters.");
        if (request.CurrentPassword == request.NewPassword)
            return BadRequest("New password must be different from the current password.");

        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Revoke all refresh tokens so other sessions are invalidated
        await _refreshTokenService.RevokeAllAsync(UserId);

        return NoContent();
    }
}
