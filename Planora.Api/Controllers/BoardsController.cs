using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Activity;
using Planora.Shared.DTOs.Board;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BoardsController : ControllerBase
{
    // Cover images are only ever accepted as uploaded files (never as a raw URL from the
    // client) so we fully control the extension and storage path — no SSRF/arbitrary-URL risk.
    private static readonly Dictionary<string, string> AllowedCoverImageTypes = new()
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };
    private const string CoverImageRelativeDir = "uploads/boards";

    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreateBoardRequest> _createValidator;
    private readonly IFileStorage _storage;

    public BoardsController(ApplicationDbContext db, IValidator<CreateBoardRequest> createValidator, IFileStorage storage)
    {
        _db = db;
        _createValidator = createValidator;
        _storage = storage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] bool includeArchived = false)
    {
        var board = await _db.Boards
            .IgnoreQueryFilters()
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards
                    // Trashed cards are never returned; archived ones only when asked for.
                    .Where(card => (includeArchived || !card.IsArchived) && card.DeletedAt == null)
                    .OrderBy(card => card.Position))
                    .ThenInclude(card => card.Labels)
                        .ThenInclude(cl => cl.Label)
            .FirstOrDefaultAsync(b => b.Id == id);

        // IgnoreQueryFilters also un-hides trashed boards — a trashed board must read as gone.
        if (board is null || board.DeletedAt is not null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        return Ok(board.ToDetailDto());
    }

    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid id, [FromQuery] int take = 30)
    {
        take = Math.Clamp(take, 1, 100);

        var board = await _db.Boards
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id);
        if (board is null || board.DeletedAt is not null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var events = await _db.ActivityEvents
            .Include(e => e.Actor)
            .Where(e => e.BoardId == id && e.WorkspaceId == board.WorkspaceId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync();

        return Ok(events.Select(e => new ActivityEventDto
        {
            Id = e.Id,
            ActorUserId = e.ActorUserId,
            ActorDisplayName = e.Actor.DisplayName,
            Verb = e.Verb,
            TargetType = e.TargetType,
            TargetId = e.TargetId,
            WorkspaceId = e.WorkspaceId,
            BoardId = e.BoardId,
            Summary = BuildActivitySummary(e),
            CreatedAt = e.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBoardRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == request.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var maxPosition = await _db.Boards
            .Where(b => b.WorkspaceId == request.WorkspaceId)
            .MaxAsync(b => (int?)b.Position) ?? -1;

        var board = new Board
        {
            Name = request.Name,
            Description = request.Description,
            CoverColor = PlanoraColors.SafeBoardBackgroundOrNull(request.CoverColor),
            WorkspaceId = request.WorkspaceId,
            Position = maxPosition + 1
        };

        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = board.Id }, board.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBoardRequest request)
    {
        if (request.RowVersion == 0) return BadRequest("RowVersion is required.");

        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.Entry(board).Property(b => b.RowVersion).OriginalValue = request.RowVersion;

        if (request.Name is not null) board.Name = request.Name;
        if (request.ClearDescription) board.Description = null;
        else if (request.Description is not null) board.Description = request.Description;
        if (request.CoverColor is not null)
        {
            if (!PlanoraColors.TryNormalizeSafeBoardBackground(request.CoverColor, out var coverColor))
                return BadRequest("CoverColor must be a readable board background color.");
            board.CoverColor = coverColor;
        }
        if (request.Position.HasValue) board.Position = request.Position.Value;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Board was modified by another request. Reload and try again." });
        }

        return Ok(board.ToDto());
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        board.IsArchived = true;
        await _db.SaveChangesAsync();
        return Ok(board.ToDto());
    }

    [HttpPatch("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id)
    {
        // DeletedAt == null: a trashed board is restored via /restore, not unarchived here.
        var board = await _db.Boards.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        board.IsArchived = false;
        await _db.SaveChangesAsync();
        return Ok(board.ToDto());
    }

    // Soft delete: move the board to the workspace trash (recoverable). The cover image is kept
    // on storage so a restore is lossless — it's only cleaned up on permanent delete.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var board = await _db.Boards.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        board.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash([FromQuery] Guid workspaceId)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var boards = await _db.Boards
            .IgnoreQueryFilters()
            .Where(b => b.WorkspaceId == workspaceId && b.DeletedAt != null)
            .OrderByDescending(b => b.DeletedAt)
            .ToListAsync();

        return Ok(boards.ToDtoList());
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var board = await _db.Boards.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt != null);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        board.DeletedAt = null;
        await _db.SaveChangesAsync();
        return Ok(board.ToDto());
    }

    // Hard delete — only a trashed board can be permanently removed. Cascades to columns/cards/etc.
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id)
    {
        var board = await _db.Boards.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt != null);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        await _storage.DeleteAsync(board.CoverImageUrl);
        _db.Boards.Remove(board);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/cover-image")]
    [RequestSizeLimit(BoardLimits.MaxCoverImageBytes + 1024)]
    public async Task<IActionResult> UploadCoverImage(Guid id, IFormFile? file)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        if (file.Length > BoardLimits.MaxCoverImageBytes) return BadRequest($"Image must be smaller than {BoardLimits.MaxCoverImageBytes / 1024 / 1024} MB.");
        if (!AllowedCoverImageTypes.TryGetValue(file.ContentType, out var extension))
            return BadRequest("Unsupported image type. Use PNG, JPEG, WEBP or GIF.");

        // Buffer the upload (capped at BoardLimits.MaxCoverImageBytes) so we can sniff the real file
        // signature before trusting it — file.ContentType is just a client-supplied header
        // and can't be trusted on its own to decide what gets written to disk and served.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;
        var header = new byte[Math.Min(16, (int)buffer.Length)];
        await buffer.ReadExactlyAsync(header);
        buffer.Position = 0;

        if (!HasValidImageSignature(header, file.ContentType))
            return BadRequest("File content doesn't match a supported image format.");

        // Write the new image first, then remove the previous one, so a failed write
        // never leaves the board without its existing cover.
        var newUrl = await _storage.SaveAsync(buffer, CoverImageRelativeDir, extension);
        await _storage.DeleteAsync(board.CoverImageUrl);
        board.CoverImageUrl = newUrl;
        await _db.SaveChangesAsync();

        return Ok(board.ToDto());
    }

    [HttpDelete("{id:guid}/cover-image")]
    public async Task<IActionResult> DeleteCoverImage(Guid id)
    {
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        await _storage.DeleteAsync(board.CoverImageUrl);
        board.CoverImageUrl = null;
        await _db.SaveChangesAsync();

        return Ok(board.ToDto());
    }

    // Verifies the actual file bytes match the claimed content type (magic-byte sniffing),
    // since file.ContentType alone is attacker-controlled and not sufficient on its own.
    private static bool HasValidImageSignature(byte[] header, string contentType) => contentType switch
    {
        "image/png" => header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
        "image/jpeg" => header.Length >= 3
            && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        "image/gif" => header.Length >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
            && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61,
        "image/webp" => header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
        _ => false,
    };

    private static string BuildActivitySummary(ActivityEvent activity)
    {
        using var payload = JsonDocument.Parse(activity.PayloadJson);
        var root = payload.RootElement;
        var title = GetPayloadString(root, "title", "a card");

        return activity.Verb switch
        {
            "card.created" => $"Created \"{title}\" in \"{GetPayloadString(root, "columnTitle", "a column")}\"",
            "card.moved" when GetPayloadString(root, "fromColumnId") == GetPayloadString(root, "toColumnId")
                => $"Reordered \"{title}\" in \"{GetPayloadString(root, "toColumnTitle", "a column")}\"",
            "card.moved" => $"Moved \"{title}\" from \"{GetPayloadString(root, "fromColumnTitle", "a column")}\" to \"{GetPayloadString(root, "toColumnTitle", "another column")}\"",
            _ => activity.Verb
        };
    }

    private static string GetPayloadString(JsonElement payload, string name, string fallback = "")
    {
        if (!payload.TryGetProperty(name, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }
}
