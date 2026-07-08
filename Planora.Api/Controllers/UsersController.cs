using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Account;
using Planora.Shared.DTOs.Users;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountService _accountService;

    public UsersController(UserManager<AppUser> userManager, IRefreshTokenService refreshTokenService, IAccountService accountService)
    {
        _userManager = userManager;
        _refreshTokenService = refreshTokenService;
        _accountService = accountService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        return Ok(new UserProfileDto
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed
        });
    }

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

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        return Ok(new NotificationPreferencesDto
        {
            EmailOnAssigned = user.EmailOnAssigned,
            EmailOnComment = user.EmailOnComment,
            EmailOnWorkspaceInvite = user.EmailOnWorkspaceInvite
        });
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] NotificationPreferencesDto request)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        user.EmailOnAssigned = request.EmailOnAssigned;
        user.EmailOnComment = request.EmailOnComment;
        user.EmailOnWorkspaceInvite = request.EmailOnWorkspaceInvite;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(new NotificationPreferencesDto
        {
            EmailOnAssigned = user.EmailOnAssigned,
            EmailOnComment = user.EmailOnComment,
            EmailOnWorkspaceInvite = user.EmailOnWorkspaceInvite
        });
    }

    /// <summary>Downloads a full JSON export of the caller's profile and every workspace they belong to.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportData(CancellationToken ct)
    {
        var export = await _accountService.BuildExportAsync(UserId, ct);
        return export is null ? NotFound() : Ok(export);
    }

    /// <summary>
    /// Permanently deletes the caller's account after re-authenticating with their password.
    /// Solo-owned workspaces are removed with it; owning a workspace with other members blocks
    /// deletion (409) until it is transferred or deleted.
    /// </summary>
    [HttpPost("delete-account")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Your password is required to delete your account.");

        var result = await _accountService.DeleteAccountAsync(UserId, request.Password, ct);
        return result.Status switch
        {
            AccountDeletionStatus.Success => NoContent(),
            AccountDeletionStatus.WrongPassword => BadRequest("Incorrect password."),
            AccountDeletionStatus.Blocked => Conflict(new AccountDeletionBlockedResponse
            {
                BlockedWorkspaces = result.BlockedWorkspaces?.ToList() ?? []
            }),
            AccountDeletionStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Error ?? "Account deletion failed.")
        };
    }
}
