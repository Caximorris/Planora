namespace Planora.Shared.DTOs.Account;

/// <summary>
/// A full, portable snapshot of a user's data: their profile plus every workspace they are a member
/// of, with the boards/columns/cards (and their comments, labels, checklists, and attachment
/// metadata) they can see. Workspaces the user does not belong to are never included. Archived items
/// are exported; trashed (soft-deleted) items are not. Serialized to JSON for the "export my data"
/// download.
/// </summary>
public class AccountExportDto
{
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public ExportUserDto User { get; set; } = new();
    public List<ExportWorkspaceDto> Workspaces { get; set; } = [];
}

public class ExportUserDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailOnAssigned { get; set; }
    public bool EmailOnComment { get; set; }
    public bool EmailOnWorkspaceInvite { get; set; }
}

public class ExportWorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The exporting user's role in this workspace (Owner/Admin/Member).</summary>
    public string Role { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public DateTime JoinedAt { get; set; }

    public List<ExportMemberDto> Members { get; set; } = [];
    public List<ExportLabelDto> Labels { get; set; } = [];
    public List<ExportBoardDto> Boards { get; set; } = [];
}

public class ExportMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ExportLabelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public class ExportBoardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ExportColumnDto> Columns { get; set; } = [];
}

public class ExportColumnDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public List<ExportCardDto> Cards { get; set; } = [];
}

public class ExportCardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsArchived { get; set; }
    public string? AssigneeId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Ids of the workspace labels applied to this card (resolve against the workspace Labels list).</summary>
    public List<Guid> LabelIds { get; set; } = [];
    public List<ExportCommentDto> Comments { get; set; } = [];
    public List<ExportChecklistDto> Checklists { get; set; } = [];
    public List<ExportAttachmentDto> Attachments { get; set; } = [];
}

public class ExportCommentDto
{
    public string AuthorId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExportChecklistDto
{
    public string Title { get; set; } = string.Empty;
    public List<ExportChecklistItemDto> Items { get; set; } = [];
}

public class ExportChecklistItemDto
{
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class ExportAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
