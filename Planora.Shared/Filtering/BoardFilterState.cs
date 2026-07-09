using Planora.Shared.Enums;

namespace Planora.Shared.Filtering;

public enum PriorityFilterOperator
{
    Exact,
    AtOrAbove,
    AtOrBelow
}

public enum LabelMatchMode
{
    Any,
    All
}

public enum DueDateFilter
{
    Any,
    Overdue,
    DueToday,
    DueThisWeek,
    NextSevenDays,
    NoDueDate
}

public class BoardFilterState
{
    public string SearchText { get; set; } = string.Empty;
    public CardPriority? Priority { get; set; }
    public PriorityFilterOperator PriorityOperator { get; set; } = PriorityFilterOperator.Exact;
    public HashSet<string> AssigneeIds { get; set; } = [];
    public bool AssignedToCurrentUser { get; set; }
    public bool IncludeUnassigned { get; set; }
    public HashSet<Guid> LabelIds { get; set; } = [];
    public LabelMatchMode LabelMatchMode { get; set; } = LabelMatchMode.Any;
    public DueDateFilter DueDate { get; set; } = DueDateFilter.Any;
    public HashSet<Guid> ColumnIds { get; set; } = [];
    public int? UpdatedWithinDays { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || Priority.HasValue
        || AssigneeIds.Count > 0
        || AssignedToCurrentUser
        || IncludeUnassigned
        || LabelIds.Count > 0
        || DueDate != DueDateFilter.Any
        || ColumnIds.Count > 0
        || UpdatedWithinDays.HasValue;

    public BoardFilterState Clone() => new()
    {
        SearchText = SearchText,
        Priority = Priority,
        PriorityOperator = PriorityOperator,
        AssigneeIds = AssigneeIds.ToHashSet(StringComparer.Ordinal),
        AssignedToCurrentUser = AssignedToCurrentUser,
        IncludeUnassigned = IncludeUnassigned,
        LabelIds = LabelIds.ToHashSet(),
        LabelMatchMode = LabelMatchMode,
        DueDate = DueDate,
        ColumnIds = ColumnIds.ToHashSet(),
        UpdatedWithinDays = UpdatedWithinDays
    };

    public static BoardFilterState Empty() => new();
}
