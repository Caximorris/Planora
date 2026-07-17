using System.Text.Json;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Constants;
using Planora.Shared.Enums;

namespace Planora.Api.Application.Services;

/// <summary>
/// Seeds the lightweight welcome content for normal registrations and a richer,
/// multi-workspace showcase for instant demo accounts.
/// </summary>
public sealed class DemoWorkspaceSeeder : IDemoWorkspaceSeeder
{
    private readonly ApplicationDbContext _db;

    public DemoWorkspaceSeeder(ApplicationDbContext db) => _db = db;

    public async Task SeedAsync(string userId, bool fullShowcase = false)
    {
        if (!fullShowcase)
        {
            await SeedWelcomeWorkspaceAsync(userId);
            return;
        }

        await SeedFullShowcaseAsync(userId);
    }

    private async Task SeedWelcomeWorkspaceAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var workspace = new Workspace
        {
            Name = "Welcome Workspace",
            Description = "A guided tour of everything Planora can do.",
            OwnerId = userId
        };
        AddOwner(workspace, userId, now);

        var board = new Board
        {
            WorkspaceId = workspace.Id,
            Name = "Showcase Board",
            Description = "Explore columns, cards, priorities, due dates and custom colors.",
            CoverColor = PlanoraColors.DefaultBoardColor
        };
        AddColumns(board, ["Backlog", "In Progress", "Review", "Done"]);

        var cards = new List<Card>();
        AddCard(cards, board.Columns.ElementAt(0), 0, "Click this card to open it", "Cards can have a title, description, due date, priority and color.", CardPriority.None, now.AddDays(3));
        AddCard(cards, board.Columns.ElementAt(0), 1, "Drag cards between columns", "Move cards with drag and drop to update their workflow.", CardPriority.Low, now.AddDays(7));
        AddCard(cards, board.Columns.ElementAt(1), 0, "Give a column its own color", "Columns and cards can use their own surface colors.", CardPriority.Medium, now.AddDays(2), "#EDE9FE");
        AddCard(cards, board.Columns.ElementAt(1), 1, "Overdue cards turn red", "This card is intentionally overdue so the board shows the warning state.", CardPriority.High, now.AddDays(-1));
        AddCard(cards, board.Columns.ElementAt(2), 0, "Critical priority example", "Critical cards get a distinct accent so they stand out.", CardPriority.Critical, now, "#F5F3FF");
        AddCard(cards, board.Columns.ElementAt(2), 1, "Filter cards by priority", "Use the priority pills to focus the board.", CardPriority.Medium);
        AddCard(cards, board.Columns.ElementAt(3), 0, "A finished task", "Move finished work here to keep a record of delivery.", CardPriority.Low, now.AddDays(-2), "#CFFAFE");
        AddCard(cards, board.Columns.ElementAt(3), 1, "Keep this as a reference", "This workspace is a starting point for exploring Planora.", CardPriority.None);

        board.Columns.ElementAt(0).Cards.Add(cards[0]);
        board.Columns.ElementAt(0).Cards.Add(cards[1]);
        board.Columns.ElementAt(1).Cards.Add(cards[2]);
        board.Columns.ElementAt(1).Cards.Add(cards[3]);
        board.Columns.ElementAt(2).Cards.Add(cards[4]);
        board.Columns.ElementAt(2).Cards.Add(cards[5]);
        board.Columns.ElementAt(3).Cards.Add(cards[6]);
        board.Columns.ElementAt(3).Cards.Add(cards[7]);
        workspace.Boards.Add(board);

        _db.Workspaces.Add(workspace);
        _db.Cards.AddRange(cards);
        await _db.SaveChangesAsync();
    }

    private async Task SeedFullShowcaseAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var people = CreateShowcasePeople(userId);
        _db.Users.AddRange(people);

        var workspaces = new[]
        {
            CreateWorkspace(userId, "Northstar Product Lab", "A product team planning a polished launch.", now, people),
            CreateWorkspace(userId, "Brightside Creative Studio", "A small marketing studio coordinating campaigns and content.", now, people),
            CreateWorkspace(userId, "Life Admin HQ", "A personal operating system for plans, projects and recurring chores.", now, people)
        };

        var allCards = new List<Card>();
        var allChecklists = new List<Checklist>();
        var allChecklistItems = new List<ChecklistItem>();
        var allCardLabels = new List<CardLabel>();
        var allComments = new List<CardComment>();
        var allActivities = new List<ActivityEvent>();
        var allNotifications = new List<Notification>();
        var allInvitations = new List<WorkspaceInvitation>();

        foreach (var workspace in workspaces)
        {
            var labels = AddLabels(workspace);
            var boardThemes = workspace.Name switch
            {
                "Northstar Product Lab" => new[]
                {
                    ("Product Roadmap", "Shape the next product cycle", "#241F47", false),
                    ("Sprint Planning", "Turn the roadmap into an executable sprint", "#2E1065", false),
                    ("Launch Retrospective", "A completed launch for historical reference", "#334155", true)
                },
                "Brightside Creative Studio" => new[]
                {
                    ("Campaign Pipeline", "Move campaign work from brief to launch", "#0E7490", false),
                    ("Editorial Calendar", "Plan and review the next month of content", "#4C1D95", false),
                    ("Brand Refresh Archive", "Archived brand work kept for reference", "#475569", true)
                },
                _ => new[]
                {
                    ("Weekly Planning", "A calm view of the week ahead", "#17182B", false),
                    ("Home Renovation", "Coordinate a small home improvement project", "#2E1065", false),
                    ("Ideas Inbox", "A parking lot for future projects", "#334155", false)
                }
            };

            for (var boardIndex = 0; boardIndex < boardThemes.Length; boardIndex++)
            {
                var theme = boardThemes[boardIndex];
                var board = AddShowcaseBoard(workspace, theme.Item1, theme.Item2, theme.Item3, theme.Item4, boardIndex);
                var boardCards = AddShowcaseCards(board, theme.Item1, boardIndex, now, people, labels);
                allCards.AddRange(boardCards);
                allCardLabels.AddRange(boardCards.SelectMany(card => card.Labels));

                if (boardIndex == 0)
                {
                    AddChecklistContent(boardCards[2], allChecklists, allChecklistItems);
                    AddCommentContent(boardCards[2], workspace, people, allComments, now);
                    allActivities.Add(new ActivityEvent
                    {
                        ActorUserId = people[1].Id,
                        WorkspaceId = workspace.Id,
                        BoardId = board.Id,
                        TargetId = boardCards[2].Id,
                        TargetType = "Card",
                        Verb = "card.moved",
                        PayloadJson = JsonSerializer.Serialize(new { title = boardCards[2].Title, toColumnTitle = "In Progress" }),
                        CreatedAt = now.AddHours(-4)
                    });
                    allNotifications.Add(new Notification
                    {
                        UserId = userId,
                        Type = NotificationType.AssignedToCard,
                        Message = $"{people[1].DisplayName} assigned you to \"{boardCards[2].Title}\".",
                        RelatedCardId = boardCards[2].Id,
                        RelatedBoardId = board.Id,
                        RelatedWorkspaceId = workspace.Id,
                        IsRead = false
                    });
                }
            }

            allInvitations.Add(new WorkspaceInvitation
            {
                WorkspaceId = workspace.Id,
                InviterUserId = userId,
                InviteeEmail = $"prospect+{workspace.Id:N}@planora.demo",
                Role = WorkspaceRole.Member,
                Token = $"showcase-{workspace.Id:N}",
                ExpiresAt = now.AddDays(5),
                Status = InvitationStatus.Pending
            });
        }

        allNotifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationType.NewComment,
            Message = "Taylor left a comment on your launch checklist.",
            RelatedWorkspaceId = workspaces[0].Id,
            IsRead = true
        });

        _db.Workspaces.AddRange(workspaces);
        _db.Cards.AddRange(allCards);
        _db.Checklists.AddRange(allChecklists);
        _db.ChecklistItems.AddRange(allChecklistItems);
        _db.CardLabels.AddRange(allCardLabels);
        _db.CardComments.AddRange(allComments);
        _db.ActivityEvents.AddRange(allActivities);
        _db.Notifications.AddRange(allNotifications);
        _db.WorkspaceInvitations.AddRange(allInvitations);
        await _db.SaveChangesAsync();
    }

    private static AppUser[] CreateShowcasePeople(string ownerId) =>
    [
        new() { Id = $"demo-{ownerId}-alex", UserName = $"alex.{ownerId}@planora.demo", NormalizedUserName = $"ALEX.{ownerId}@PLANORA.DEMO", Email = $"alex.{ownerId}@planora.demo", NormalizedEmail = $"ALEX.{ownerId}@PLANORA.DEMO", DisplayName = "Alex Rivera", EmailConfirmed = true },
        new() { Id = $"demo-{ownerId}-taylor", UserName = $"taylor.{ownerId}@planora.demo", NormalizedUserName = $"TAYLOR.{ownerId}@PLANORA.DEMO", Email = $"taylor.{ownerId}@planora.demo", NormalizedEmail = $"TAYLOR.{ownerId}@PLANORA.DEMO", DisplayName = "Taylor Chen", EmailConfirmed = true },
        new() { Id = $"demo-{ownerId}-morgan", UserName = $"morgan.{ownerId}@planora.demo", NormalizedUserName = $"MORGAN.{ownerId}@PLANORA.DEMO", Email = $"morgan.{ownerId}@planora.demo", NormalizedEmail = $"MORGAN.{ownerId}@PLANORA.DEMO", DisplayName = "Morgan Ellis", EmailConfirmed = true }
    ];

    private static Workspace CreateWorkspace(string ownerId, string name, string description, DateTime now, IReadOnlyList<AppUser> people)
    {
        var workspace = new Workspace { Name = name, Description = description, OwnerId = ownerId, CreatedAt = now.AddDays(-14) };
        AddOwner(workspace, ownerId, now.AddDays(-14));
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = people[0].Id, Role = WorkspaceRole.Admin, JoinedAt = now.AddDays(-12) });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = people[1].Id, Role = WorkspaceRole.Member, JoinedAt = now.AddDays(-10) });
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = people[2].Id, Role = WorkspaceRole.Member, JoinedAt = now.AddDays(-8) });
        return workspace;
    }

    private static List<WorkspaceLabel> AddLabels(Workspace workspace)
    {
        var labels = new List<WorkspaceLabel>
        {
            new() { WorkspaceId = workspace.Id, Name = "Urgent", Color = "#E11D48" },
            new() { WorkspaceId = workspace.Id, Name = "Design", Color = "#6D28D9" },
            new() { WorkspaceId = workspace.Id, Name = "Planning", Color = "#0E7490" },
            new() { WorkspaceId = workspace.Id, Name = "Blocked", Color = "#D97706" },
            new() { WorkspaceId = workspace.Id, Name = "Shipped", Color = "#16A34A" }
        };
        foreach (var label in labels) workspace.Labels.Add(label);
        return labels;
    }

    private static Board AddShowcaseBoard(Workspace workspace, string name, string description, string color, bool archived, int position)
    {
        var board = new Board
        {
            WorkspaceId = workspace.Id,
            Name = name,
            Description = description,
            CoverColor = color,
            IsArchived = archived,
            Position = position
        };
        AddColumns(board, ["Ideas", "Planned", "In Progress", "Review", "Done"]);
        workspace.Boards.Add(board);
        return board;
    }

    private static List<Card> AddShowcaseCards(Board board, string theme, int boardIndex, DateTime now, IReadOnlyList<AppUser> people, IReadOnlyList<WorkspaceLabel> labels)
    {
        var cards = new List<Card>();
        var topics = new[] { "align on scope", "draft the first version", "collect stakeholder feedback", "resolve the open decision", "prepare the handoff" };
        for (var columnIndex = 0; columnIndex < board.Columns.Count; columnIndex++)
        {
            var column = board.Columns.ElementAt(columnIndex);
            for (var cardIndex = 0; cardIndex < 3; cardIndex++)
            {
                var index = columnIndex * 3 + cardIndex;
                var card = new Card
                {
                    ColumnId = column.Id,
                    Position = cardIndex,
                    Title = $"{theme}: {topics[columnIndex]} ({cardIndex + 1})",
                    Description = $"A realistic sample task for the {theme} workflow. Add notes, move it, assign it, or change its priority.",
                    Priority = (CardPriority)(index % 5),
                    DueDate = index % 4 == 0 ? now.AddDays(index - 2) : now.AddDays(index + 2),
                    Color = index % 5 == 0 ? "#EDE9FE" : null,
                    IsArchived = board.IsArchived && cardIndex == 2,
                    AssigneeId = people[(index + boardIndex) % people.Count].Id
                };
                column.Cards.Add(card);
                cards.Add(card);

                card.Labels.Add(new CardLabel { CardId = card.Id, LabelId = labels[index % labels.Count].Id });
                if (index % 3 == 0)
                    card.Labels.Add(new CardLabel { CardId = card.Id, LabelId = labels[(index + 1) % labels.Count].Id });
            }
        }
        return cards;
    }

    private static void AddChecklistContent(Card card, ICollection<Checklist> checklists, ICollection<ChecklistItem> items)
    {
        var checklist = new Checklist { CardId = card.Id, Title = "Launch readiness", Position = 0 };
        var checklistItems = new[] { "Confirm owner", "Review acceptance criteria", "Share the final update" }
            .Select((text, index) => new ChecklistItem { ChecklistId = checklist.Id, Text = text, Position = index, IsCompleted = index == 0 })
            .ToList();
        card.Checklists.Add(checklist);
        foreach (var item in checklistItems)
            checklist.Items.Add(item);
        checklists.Add(checklist);
        foreach (var item in checklistItems)
            items.Add(item);
    }

    private static void AddCommentContent(Card card, Workspace workspace, IReadOnlyList<AppUser> people, ICollection<CardComment> comments, DateTime now)
    {
        comments.Add(new CardComment { CardId = card.Id, AuthorId = people[1].Id, Text = "I added the first pass. Can someone review the edge cases before Friday?", CreatedAt = now.AddHours(-8) });
        comments.Add(new CardComment { CardId = card.Id, AuthorId = people[2].Id, Text = $"Morgan: I will take this in the {workspace.Name} review slot.", CreatedAt = now.AddHours(-3) });
    }

    private static void AddOwner(Workspace workspace, string userId, DateTime joinedAt) =>
        workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = userId, Role = WorkspaceRole.Owner, JoinedAt = joinedAt });

    private static void AddColumns(Board board, IReadOnlyList<string> titles)
    {
        foreach (var (title, index) in titles.Select((title, index) => (title, index)))
            board.Columns.Add(new Column { BoardId = board.Id, Title = title, Position = index, Color = index % 2 == 1 ? "#F5F3FF" : null });
    }

    private static void AddCard(ICollection<Card> cards, Column column, int position, string title, string description, CardPriority priority, DateTime? dueDate = null, string? color = null)
    {
        cards.Add(new Card { ColumnId = column.Id, Position = position, Title = title, Description = description, Priority = priority, DueDate = dueDate, Color = color });
    }
}
