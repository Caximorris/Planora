using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Constants;
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
    private readonly IWebHostEnvironment _env;

    public BoardsController(ApplicationDbContext db, IValidator<CreateBoardRequest> createValidator, IWebHostEnvironment env)
    {
        _db = db;
        _createValidator = createValidator;
        _env = env;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] bool includeArchived = false)
    {
        var board = await _db.Boards
            .IgnoreQueryFilters()
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards
                    .Where(card => includeArchived || !card.IsArchived)
                    .OrderBy(card => card.Position))
                    .ThenInclude(card => card.Labels)
                        .ThenInclude(cl => cl.Label)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        return Ok(board.ToDetailDto());
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
            CoverColor = request.CoverColor,
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
        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (request.Name is not null) board.Name = request.Name;
        if (request.ClearDescription) board.Description = null;
        else if (request.Description is not null) board.Description = request.Description;
        if (request.CoverColor is not null) board.CoverColor = request.CoverColor;
        if (request.Position.HasValue) board.Position = request.Position.Value;

        await _db.SaveChangesAsync();
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
        var board = await _db.Boards.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        board.IsArchived = false;
        await _db.SaveChangesAsync();
        return Ok(board.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var board = await _db.Boards.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        DeleteCoverImageFile(board.CoverImageUrl);
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

        var uploadsDir = Path.Combine(_env.WebRootPath, CoverImageRelativeDir);
        Directory.CreateDirectory(uploadsDir);

        // File name is always server-generated (GUID + allowlisted extension) — the
        // client-supplied file name is never used for the path, so there's no traversal risk.
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);
        await using (var stream = new FileStream(filePath, FileMode.Create))
            await buffer.CopyToAsync(stream);

        DeleteCoverImageFile(board.CoverImageUrl);
        board.CoverImageUrl = $"/{CoverImageRelativeDir}/{fileName}";
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

        DeleteCoverImageFile(board.CoverImageUrl);
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

    private void DeleteCoverImageFile(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;
        if (!relativeUrl.StartsWith($"/{CoverImageRelativeDir}/", StringComparison.Ordinal)) return;

        var fileName = Path.GetFileName(relativeUrl);
        var filePath = Path.Combine(_env.WebRootPath, CoverImageRelativeDir, fileName);
        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
    }
}
