using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.Enums;

namespace Planora.Shared.Filtering;

public static class BoardFilterEngine
{
    public static readonly IReadOnlyList<CardPriority> OrderedPriorities =
    [
        CardPriority.Low,
        CardPriority.Medium,
        CardPriority.High,
        CardPriority.Critical
    ];

    private static readonly IReadOnlyDictionary<CardPriority, int> PriorityRanks = new Dictionary<CardPriority, int>
    {
        [CardPriority.Low] = 1,
        [CardPriority.Medium] = 2,
        [CardPriority.High] = 3,
        [CardPriority.Critical] = 4
    };

    private static readonly HashSet<string> CompletedColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "done",
        "complete",
        "completed",
        "closed"
    };

    public static BoardFilterResult Apply(
        BoardDetailDto board,
        BoardFilterState? filters,
        DateTime utcNow,
        string? currentUserId = null)
    {
        filters ??= BoardFilterState.Empty();

        var totalCards = board.Columns.Sum(c => c.Cards.Count);
        var columns = new List<ColumnDto>();
        var matchingCards = 0;

        foreach (var column in board.Columns.OrderBy(c => c.Position))
        {
            if (filters.ColumnIds.Count > 0 && !filters.ColumnIds.Contains(column.Id))
                continue;

            var cards = column.Cards
                .OrderBy(c => c.Position)
                .Where(card => Matches(card, column, filters, utcNow, currentUserId))
                .ToList();

            matchingCards += cards.Count;
            columns.Add(CloneColumnWithCards(column, cards));
        }

        return new BoardFilterResult
        {
            Columns = columns,
            TotalCards = totalCards,
            MatchingCards = matchingCards,
            HasActiveFilters = filters.HasActiveFilters
        };
    }

    public static bool Matches(
        CardDto card,
        ColumnDto column,
        BoardFilterState filters,
        DateTime utcNow,
        string? currentUserId = null) =>
        MatchesSearch(card, filters.SearchText)
        && MatchesPriority(card, filters)
        && MatchesAssignee(card, filters, currentUserId)
        && MatchesLabels(card, filters)
        && MatchesDueDate(card, column, filters.DueDate, utcNow)
        && MatchesUpdatedAt(card, filters.UpdatedWithinDays, utcNow);

    public static string DescribePriority(CardPriority priority, PriorityFilterOperator op) =>
        op switch
        {
            PriorityFilterOperator.AtOrAbove => $"{priority} and above",
            PriorityFilterOperator.AtOrBelow => $"{priority} and below",
            _ => priority == CardPriority.Critical ? "Critical only" : $"{priority} only"
        };

    private static bool MatchesSearch(CardDto card, string searchText)
    {
        var term = searchText.Trim();
        if (term.Length == 0) return true;

        return Contains(card.Title, term) || Contains(card.Description, term);
    }

    private static bool MatchesPriority(CardDto card, BoardFilterState filters)
    {
        if (!filters.Priority.HasValue) return true;

        var selected = filters.Priority.Value;
        if (selected == CardPriority.None)
            return filters.PriorityOperator == PriorityFilterOperator.Exact && card.Priority == CardPriority.None;

        if (!PriorityRanks.TryGetValue(selected, out var selectedRank))
            return false;

        if (!PriorityRanks.TryGetValue(card.Priority, out var cardRank))
            return false;

        return filters.PriorityOperator switch
        {
            PriorityFilterOperator.AtOrAbove => cardRank >= selectedRank,
            PriorityFilterOperator.AtOrBelow => cardRank <= selectedRank,
            _ => cardRank == selectedRank
        };
    }

    private static bool MatchesAssignee(CardDto card, BoardFilterState filters, string? currentUserId)
    {
        var hasAssigneeFilters = filters.AssigneeIds.Count > 0
            || filters.AssignedToCurrentUser
            || filters.IncludeUnassigned;

        if (!hasAssigneeFilters) return true;

        if (string.IsNullOrWhiteSpace(card.AssigneeId))
            return filters.IncludeUnassigned;

        if (filters.AssigneeIds.Contains(card.AssigneeId))
            return true;

        return filters.AssignedToCurrentUser
            && !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(card.AssigneeId, currentUserId, StringComparison.Ordinal);
    }

    private static bool MatchesLabels(CardDto card, BoardFilterState filters)
    {
        if (filters.LabelIds.Count == 0) return true;

        var cardLabelIds = card.Labels.Select(l => l.Id).ToHashSet();
        return filters.LabelMatchMode == LabelMatchMode.All
            ? filters.LabelIds.All(cardLabelIds.Contains)
            : filters.LabelIds.Any(cardLabelIds.Contains);
    }

    private static bool MatchesDueDate(CardDto card, ColumnDto column, DueDateFilter filter, DateTime utcNow)
    {
        if (filter == DueDateFilter.Any) return true;
        if (!card.DueDate.HasValue) return filter == DueDateFilter.NoDueDate;
        if (filter == DueDateFilter.NoDueDate) return false;

        var today = utcNow.ToLocalTime().Date;
        var dueDate = card.DueDate.Value.ToLocalTime().Date;

        return filter switch
        {
            DueDateFilter.Overdue => dueDate < today && !IsCompletedColumn(column),
            DueDateFilter.DueToday => dueDate == today,
            DueDateFilter.DueThisWeek => dueDate >= StartOfWeek(today) && dueDate < StartOfWeek(today).AddDays(7),
            DueDateFilter.NextSevenDays => dueDate >= today && dueDate <= today.AddDays(7),
            _ => true
        };
    }

    private static bool MatchesUpdatedAt(CardDto card, int? updatedWithinDays, DateTime utcNow)
    {
        if (!updatedWithinDays.HasValue) return true;
        if (updatedWithinDays.Value <= 0) return true;

        return card.UpdatedAt >= utcNow.AddDays(-updatedWithinDays.Value);
    }

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static bool IsCompletedColumn(ColumnDto column) =>
        CompletedColumnNames.Contains(column.Title.Trim());

    private static ColumnDto CloneColumnWithCards(ColumnDto column, List<CardDto> cards) => new()
    {
        Id = column.Id,
        Title = column.Title,
        Position = column.Position,
        Color = column.Color,
        RowVersion = column.RowVersion,
        BoardId = column.BoardId,
        Cards = cards,
        CreatedAt = column.CreatedAt,
        UpdatedAt = column.UpdatedAt
    };
}
