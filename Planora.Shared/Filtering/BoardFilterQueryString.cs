using Planora.Shared.Enums;

namespace Planora.Shared.Filtering;

public static class BoardFilterQueryString
{
    public static BoardFilterState FromQueryString(string? query)
    {
        var values = Parse(query);
        var state = new BoardFilterState();

        if (values.TryGetValue("q", out var search))
            state.SearchText = search;

        if (values.TryGetValue("prio", out var priority)
            && Enum.TryParse<CardPriority>(priority, true, out var parsedPriority)
            && Enum.IsDefined(parsedPriority))
        {
            state.Priority = parsedPriority;
        }

        if (values.TryGetValue("prioOp", out var priorityOp)
            && Enum.TryParse<PriorityFilterOperator>(priorityOp, true, out var parsedPriorityOp)
            && Enum.IsDefined(parsedPriorityOp))
        {
            state.PriorityOperator = parsedPriorityOp;
        }

        if (values.TryGetValue("assignees", out var assignees))
            state.AssigneeIds = Split(assignees).ToHashSet(StringComparer.Ordinal);

        if (values.TryGetValue("me", out var me))
            state.AssignedToCurrentUser = me == "1" || bool.TryParse(me, out var meBool) && meBool;

        if (values.TryGetValue("unassigned", out var unassigned))
            state.IncludeUnassigned = unassigned == "1" || bool.TryParse(unassigned, out var unassignedBool) && unassignedBool;

        if (values.TryGetValue("labels", out var labels))
            state.LabelIds = Split(labels).Select(TryGuid).OfType<Guid>().ToHashSet();

        if (values.TryGetValue("labelMode", out var labelMode)
            && Enum.TryParse<LabelMatchMode>(labelMode, true, out var parsedLabelMode)
            && Enum.IsDefined(parsedLabelMode))
        {
            state.LabelMatchMode = parsedLabelMode;
        }

        if (values.TryGetValue("due", out var due)
            && Enum.TryParse<DueDateFilter>(due, true, out var parsedDue)
            && Enum.IsDefined(parsedDue))
        {
            state.DueDate = parsedDue;
        }

        if (values.TryGetValue("cols", out var columns))
            state.ColumnIds = Split(columns).Select(TryGuid).OfType<Guid>().ToHashSet();

        if (values.TryGetValue("updated", out var updated)
            && int.TryParse(updated, out var updatedDays)
            && updatedDays > 0)
        {
            state.UpdatedWithinDays = updatedDays;
        }

        return state;
    }

    public static string ToQueryString(BoardFilterState state)
    {
        var pairs = new List<(string Key, string Value)>();

        if (!string.IsNullOrWhiteSpace(state.SearchText))
            pairs.Add(("q", state.SearchText.Trim()));
        if (state.Priority.HasValue)
            pairs.Add(("prio", state.Priority.Value.ToString()));
        if (state.Priority.HasValue && state.PriorityOperator != PriorityFilterOperator.Exact)
            pairs.Add(("prioOp", state.PriorityOperator.ToString()));
        if (state.AssigneeIds.Count > 0)
            pairs.Add(("assignees", string.Join(",", state.AssigneeIds.Order(StringComparer.Ordinal))));
        if (state.AssignedToCurrentUser)
            pairs.Add(("me", "1"));
        if (state.IncludeUnassigned)
            pairs.Add(("unassigned", "1"));
        if (state.LabelIds.Count > 0)
            pairs.Add(("labels", string.Join(",", state.LabelIds.Order())));
        if (state.LabelIds.Count > 0 && state.LabelMatchMode != LabelMatchMode.Any)
            pairs.Add(("labelMode", state.LabelMatchMode.ToString()));
        if (state.DueDate != DueDateFilter.Any)
            pairs.Add(("due", state.DueDate.ToString()));
        if (state.ColumnIds.Count > 0)
            pairs.Add(("cols", string.Join(",", state.ColumnIds.Order())));
        if (state.UpdatedWithinDays is > 0)
            pairs.Add(("updated", state.UpdatedWithinDays.Value.ToString()));

        return string.Join("&", pairs.Select(p => $"{Escape(p.Key)}={Escape(p.Value)}"));
    }

    private static Dictionary<string, string> Parse(string? query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query)) return parsed;

        var q = query[0] == '?' ? query[1..] : query;
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = pair.IndexOf('=');
            var key = idx >= 0 ? pair[..idx] : pair;
            var value = idx >= 0 ? pair[(idx + 1)..] : string.Empty;
            parsed[Unescape(key)] = Unescape(value);
        }

        return parsed;
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Guid? TryGuid(string value) =>
        Guid.TryParse(value, out var id) ? id : null;

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string Unescape(string value) =>
        Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
}
