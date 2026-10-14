namespace NextProject.Data.Entities;

public enum ShareKind
{
    Project,
    Goal,
}

/// <summary>
/// The anonymous role: a link that shows a project or a goal read-only, for a limited time, optionally behind a password. The visitor has no account and
/// sees only what the link allows (never comments, history or attachments).
/// </summary>
public class ShareLink : AuditedEntity
{
    public Guid Token { get; set; } = Guid.NewGuid();

    public ShareKind Kind { get; set; } = ShareKind.Project;

    public long? ProjectId { get; set; }

    public Project? Project { get; set; }

    public long? GoalId { get; set; }

    public Goal? Goal { get; set; }

    public string Title { get; set; } = "";

    /// <summary>A message to the visitor, shown on the page.</summary>
    public string? Message { get; set; }

    public DateTime ExpiresDate { get; set; }

    public string? PasswordHash { get; set; }

    /// <summary>Show the single todos (title, status, owner, due date), not only the numbers.</summary>
    public bool ShowTodos { get; set; } = true;

    public string? RecipientName { get; set; }

    public DateTime? RevokedDate { get; set; }

    public int ViewCount { get; set; }

    public DateTime? LastViewDate { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedDate is null && ExpiresDate > nowUtc;
}
