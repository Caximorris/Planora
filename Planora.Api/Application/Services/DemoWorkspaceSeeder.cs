using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Enums;

namespace Planora.Api.Application.Services;

/// <summary>
/// Seeds a showcase workspace for newly registered users so they can see
/// boards, columns, cards, priorities, due dates and custom colors in action
/// without having to build anything from scratch.
/// </summary>
public class DemoWorkspaceSeeder : IDemoWorkspaceSeeder
{
    private readonly ApplicationDbContext _db;

    public DemoWorkspaceSeeder(ApplicationDbContext db) => _db = db;

    public async Task SeedAsync(string userId)
    {
        var now = DateTime.UtcNow;

        var workspace = new Workspace
        {
            Name = "Welcome Workspace",
            Description = "A guided tour of everything Planora can do.",
            OwnerId = userId
        };
        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            JoinedAt = now
        });

        var board = new Board
        {
            WorkspaceId = workspace.Id,
            Name = "Showcase Board",
            Description = "Explore columns, cards, priorities, due dates and custom colors.",
            CoverColor = "#3e0019"
        };

        var backlog = new Column { BoardId = board.Id, Title = "Backlog", Position = 0 };
        var inProgress = new Column { BoardId = board.Id, Title = "In Progress", Position = 1, Color = "#fef3c7" };
        var review = new Column { BoardId = board.Id, Title = "Review", Position = 2, Color = "#e0e7ff" };
        var done = new Column { BoardId = board.Id, Title = "Done", Position = 3, Color = "#dcfce7" };

        var cards = new List<Card>
        {
            new()
            {
                ColumnId = backlog.Id,
                Position = 0,
                Title = "👋 Click this card to open it",
                Description = "Cards can have a title, a description, a due date, a priority and their own color. Try editing any of them.",
                Priority = CardPriority.None
            },
            new()
            {
                ColumnId = backlog.Id,
                Position = 1,
                Title = "Drag cards between columns",
                Description = "Drag and drop any card into another column to move it.",
                Priority = CardPriority.Low,
                DueDate = now.AddDays(7)
            },
            new()
            {
                ColumnId = inProgress.Id,
                Position = 0,
                Title = "Give a column its own color",
                Description = "Click the swatch on a column header to pick a background color for it — this column and 'Review' already have one.",
                Priority = CardPriority.Medium,
                DueDate = now.AddDays(2),
                Color = "#fde68a"
            },
            new()
            {
                ColumnId = inProgress.Id,
                Position = 1,
                Title = "Overdue cards turn red",
                Description = "This card's due date is in the past, so it's flagged automatically.",
                Priority = CardPriority.High,
                DueDate = now.AddDays(-1)
            },
            new()
            {
                ColumnId = review.Id,
                Position = 0,
                Title = "🔥 Critical priority example",
                Description = "Critical cards get a distinct red accent so they stand out at a glance.",
                Priority = CardPriority.Critical,
                DueDate = now,
                Color = "#fecaca"
            },
            new()
            {
                ColumnId = review.Id,
                Position = 1,
                Title = "Filter cards by priority",
                Description = "Use the priority pills at the top of the board to show only Low, Medium, High or Critical cards.",
                Priority = CardPriority.Medium
            },
            new()
            {
                ColumnId = done.Id,
                Position = 0,
                Title = "A finished task",
                Description = "Cards don't disappear when you're done — move them here to keep a record.",
                Priority = CardPriority.Low,
                Color = "#bbf7d0"
            },
            new()
            {
                ColumnId = done.Id,
                Position = 1,
                Title = "Delete this board whenever you're ready",
                Description = "This whole workspace is just a demo — delete it from the sidebar once you're comfortable, or keep it as a reference.",
                Priority = CardPriority.None
            }
        };

        board.Columns.Add(backlog);
        board.Columns.Add(inProgress);
        board.Columns.Add(review);
        board.Columns.Add(done);
        workspace.Boards.Add(board);

        _db.Workspaces.Add(workspace);
        _db.Cards.AddRange(cards);

        await _db.SaveChangesAsync();
    }
}
