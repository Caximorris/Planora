using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Card;
using Planora.Shared.Enums;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/cards/{cardId:guid}/comments")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public CommentsController(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid cardId)
    {
        var card = await _db.Cards
            .Include(c => c.Column).ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var comments = await _db.CardComments
            .Include(c => c.Author)
            .Where(c => c.CardId == cardId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return Ok(comments.Select(c => new CardCommentDto
        {
            Id = c.Id,
            CardId = c.CardId,
            AuthorId = c.AuthorId,
            AuthorDisplayName = c.Author.DisplayName,
            Text = c.Text,
            CreatedAt = c.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid cardId, [FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Comment text is required.");

        var card = await _db.Cards
            .Include(c => c.Column).ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var author = await _db.Users.FindAsync(UserId);
        if (author is null) return Unauthorized();

        var comment = new CardComment
        {
            CardId = cardId,
            AuthorId = UserId,
            Text = request.Text.Trim()
        };
        _db.CardComments.Add(comment);

        // Notify the card's assignee if they're not the one commenting
        if (card.AssigneeId is not null && card.AssigneeId != UserId)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = card.AssigneeId,
                Type = NotificationType.NewComment,
                Message = $"{author.DisplayName} commented on \"{card.Title}\"",
                RelatedCardId = cardId,
                RelatedBoardId = card.Column.BoardId,
                RelatedWorkspaceId = card.Column.Board.WorkspaceId
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new CardCommentDto
        {
            Id = comment.Id,
            CardId = comment.CardId,
            AuthorId = comment.AuthorId,
            AuthorDisplayName = author.DisplayName,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt
        });
    }

    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid cardId, Guid commentId)
    {
        var comment = await _db.CardComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.CardId == cardId);

        if (comment is null) return NotFound();
        if (comment.AuthorId != UserId) return Forbid();

        _db.CardComments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
