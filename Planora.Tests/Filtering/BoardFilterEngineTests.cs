using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.DTOs.Label;
using Planora.Shared.Enums;
using Planora.Shared.Filtering;

namespace Planora.Tests.Filtering;

public class BoardFilterEngineTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Priority_filters_support_exact_above_and_below()
    {
        var board = TestBoard(TestColumn("Todo",
            TestCard("None", priority: CardPriority.None),
            TestCard("Low", priority: CardPriority.Low),
            TestCard("Medium", priority: CardPriority.Medium),
            TestCard("High", priority: CardPriority.High),
            TestCard("Critical", priority: CardPriority.Critical),
            TestCard("Deprecated", priority: (CardPriority)99)));

        AssertTitles(["High"], Apply(board, new() { Priority = CardPriority.High }));
        AssertTitles(["High", "Critical"], Apply(board, new()
        {
            Priority = CardPriority.High,
            PriorityOperator = PriorityFilterOperator.AtOrAbove
        }));
        AssertTitles(["Low", "Medium"], Apply(board, new()
        {
            Priority = CardPriority.Medium,
            PriorityOperator = PriorityFilterOperator.AtOrBelow
        }));
        AssertTitles(["None"], Apply(board, new() { Priority = CardPriority.None }));
    }

    [Fact]
    public void Text_search_matches_title_and_description_case_insensitively()
    {
        var board = TestBoard(TestColumn("Todo",
            TestCard("Write API docs"),
            TestCard("Fix auth", description: "Token refresh fails in Safari"),
            TestCard("Polish board")));

        AssertTitles(["Write API docs"], Apply(board, new() { SearchText = "api" }));
        AssertTitles(["Fix auth"], Apply(board, new() { SearchText = "REFRESH" }));
    }

    [Fact]
    public void Assignee_filters_support_multi_select_me_and_unassigned()
    {
        var board = TestBoard(TestColumn("Todo",
            TestCard("Mine", assigneeId: "user-1"),
            TestCard("Also selected", assigneeId: "user-2"),
            TestCard("Someone else", assigneeId: "user-3"),
            TestCard("No owner")));

        var filters = new BoardFilterState
        {
            AssignedToCurrentUser = true,
            IncludeUnassigned = true,
            AssigneeIds = ["user-2"]
        };

        AssertTitles(["Mine", "Also selected", "No owner"], Apply(board, filters, currentUserId: "user-1"));
    }

    [Fact]
    public void Label_filters_support_any_and_all_modes()
    {
        var bug = Guid.NewGuid();
        var urgent = Guid.NewGuid();
        var design = Guid.NewGuid();

        var board = TestBoard(TestColumn("Todo",
            TestCard("Bug only", labels: [bug]),
            TestCard("Bug urgent", labels: [bug, urgent]),
            TestCard("Design", labels: [design]),
            TestCard("No labels")));

        AssertTitles(["Bug only", "Bug urgent"], Apply(board, new()
        {
            LabelIds = [bug, urgent],
            LabelMatchMode = LabelMatchMode.Any
        }));

        AssertTitles(["Bug urgent"], Apply(board, new()
        {
            LabelIds = [bug, urgent],
            LabelMatchMode = LabelMatchMode.All
        }));
    }

    [Fact]
    public void Due_date_filters_handle_overdue_today_week_next_seven_days_and_missing_dates()
    {
        var board = TestBoard(
            TestColumn("Todo",
                TestCard("Overdue active", dueDate: NowUtc.AddDays(-1)),
                TestCard("Due today", dueDate: NowUtc),
                TestCard("Due this week", dueDate: NowUtc.AddDays(2)),
                TestCard("Due next week", dueDate: NowUtc.AddDays(7)),
                TestCard("No due date")),
            TestColumn("Done",
                TestCard("Overdue done", dueDate: NowUtc.AddDays(-2))));

        AssertTitles(["Overdue active"], Apply(board, new() { DueDate = DueDateFilter.Overdue }));
        AssertTitles(["Due today"], Apply(board, new() { DueDate = DueDateFilter.DueToday }));
        AssertTitles(["Overdue active", "Due today", "Due this week", "Overdue done"], Apply(board, new() { DueDate = DueDateFilter.DueThisWeek }));
        AssertTitles(["Due today", "Due this week", "Due next week"], Apply(board, new() { DueDate = DueDateFilter.NextSevenDays }));
        AssertTitles(["No due date"], Apply(board, new() { DueDate = DueDateFilter.NoDueDate }));
    }

    [Fact]
    public void Combined_filters_require_cards_to_match_every_active_dimension()
    {
        var urgent = Guid.NewGuid();
        var board = TestBoard(TestColumn("Todo",
            TestCard("Urgent API bug", description: "Escalated", priority: CardPriority.Critical, assigneeId: "user-1", dueDate: NowUtc.AddDays(1), labels: [urgent]),
            TestCard("Urgent API bug without assignee", priority: CardPriority.Critical, dueDate: NowUtc.AddDays(1), labels: [urgent]),
            TestCard("Urgent API bug low", priority: CardPriority.Low, assigneeId: "user-1", dueDate: NowUtc.AddDays(1), labels: [urgent]),
            TestCard("Other critical", priority: CardPriority.Critical, assigneeId: "user-1", dueDate: NowUtc.AddDays(1))));

        var filters = new BoardFilterState
        {
            SearchText = "api",
            Priority = CardPriority.High,
            PriorityOperator = PriorityFilterOperator.AtOrAbove,
            AssigneeIds = ["user-1"],
            LabelIds = [urgent],
            LabelMatchMode = LabelMatchMode.All,
            DueDate = DueDateFilter.NextSevenDays
        };

        AssertTitles(["Urgent API bug"], Apply(board, filters));
    }

    [Fact]
    public void Query_string_parser_handles_priority_casing()
    {
        var state = BoardFilterQueryString.FromQueryString("?prio=hIgH&prioOp=atorabove");

        Assert.Equal(CardPriority.High, state.Priority);
        Assert.Equal(PriorityFilterOperator.AtOrAbove, state.PriorityOperator);
    }

    private static BoardFilterResult Apply(BoardDetailDto board, BoardFilterState filters, string? currentUserId = null) =>
        BoardFilterEngine.Apply(board, filters, NowUtc, currentUserId);

    private static BoardDetailDto TestBoard(params ColumnDto[] columns) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Board",
        Columns = columns.ToList()
    };

    private static ColumnDto TestColumn(string title, params CardDto[] cards)
    {
        var id = Guid.NewGuid();
        for (var i = 0; i < cards.Length; i++)
        {
            cards[i].ColumnId = id;
            cards[i].Position = i;
        }

        return new ColumnDto
        {
            Id = id,
            Title = title,
            Position = 0,
            Cards = cards.ToList()
        };
    }

    private static CardDto TestCard(
        string title,
        string? description = null,
        CardPriority priority = CardPriority.None,
        string? assigneeId = null,
        DateTime? dueDate = null,
        params Guid[] labels) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = description,
        Priority = priority,
        AssigneeId = assigneeId,
        DueDate = dueDate,
        CreatedAt = NowUtc.AddDays(-10),
        UpdatedAt = NowUtc.AddDays(-1),
        Labels = labels.Select(id => new LabelDto { Id = id, Name = id.ToString("N"), WorkspaceId = Guid.NewGuid() }).ToList()
    };

    private static void AssertTitles(string[] expected, BoardFilterResult result) =>
        Assert.Equal(expected.OrderBy(x => x), result.Columns.SelectMany(c => c.Cards).Select(c => c.Title).OrderBy(x => x));
}
