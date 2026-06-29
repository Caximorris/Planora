using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Search;
using Planora.Shared.Enums;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public SearchController(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q = "")
    {
        if (q.Length < 2)
            return Ok(Array.Empty<SearchResultDto>());

        var wsIds = await _db.WorkspaceMembers
            .Where(m => m.UserId == UserId)
            .Select(m => m.WorkspaceId)
            .ToListAsync();

        var term = $"%{q}%";

        var boards = await _db.Boards
            .Where(b => wsIds.Contains(b.WorkspaceId) && !b.IsArchived &&
                        EF.Functions.ILike(b.Name, term))
            .OrderBy(b => b.Name)
            .Take(5)
            .Select(b => new SearchResultDto
            {
                Type = SearchResultType.Board,
                Id = b.Id,
                Title = b.Name,
                BoardId = b.Id,
                WorkspaceId = b.WorkspaceId
            })
            .ToListAsync();

        var cards = await _db.Cards
            .Where(c => !c.IsArchived &&
                        wsIds.Contains(c.Column.Board.WorkspaceId) &&
                        (EF.Functions.ILike(c.Title, term) ||
                         (c.Description != null && EF.Functions.ILike(c.Description, term))))
            .OrderByDescending(c => c.UpdatedAt)
            .Take(10)
            .Select(c => new SearchResultDto
            {
                Type = SearchResultType.Card,
                Id = c.Id,
                Title = c.Title,
                Subtitle = c.Column.Board.Name + " › " + c.Column.Title,
                BoardId = c.Column.Board.Id,
                WorkspaceId = c.Column.Board.WorkspaceId
            })
            .ToListAsync();

        return Ok(boards.Concat(cards).ToList());
    }
}
