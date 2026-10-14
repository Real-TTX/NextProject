namespace NextProject.Data.Entities;

public enum TodoStatus
{
    Open,
    InProgress,
    Blocked,
    InReview,
    Done,
    Discarded,
}

/// <summary>A concrete task with a defined result. It may stand alone or belong to a project, a goal or a key result.</summary>
public class Todo : AuditedEntity, ISoftDeletable
{
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    /// <summary>The one person who is responsible.</summary>
    public long OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    /// <summary>Who checks the result, when it needs a check (status "in review").</summary>
    public long? ReviewerUserId { get; set; }

    public User? ReviewerUser { get; set; }

    public TodoStatus Status { get; set; } = TodoStatus.Open;

    public Level Priority { get; set; } = Level.Medium;

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? ProjectId { get; set; }

    public Project? Project { get; set; }

    public long? GoalId { get; set; }

    public Goal? Goal { get; set; }

    public long? KeyResultId { get; set; }

    public KeyResult? KeyResult { get; set; }

    public long? MilestoneId { get; set; }

    public Milestone? Milestone { get; set; }

    /// <summary>When it was finished (UTC); set when the status becomes done, cleared when it is reopened.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>What came out of it, where that is worth writing down.</summary>
    public string? Result { get; set; }

    /// <summary>What blocks the task (while the status is blocked).</summary>
    public string? BlockReason { get; set; }

    /// <summary>What is needed to go on: a decision, help, material.</summary>
    public string? BlockNeeds { get; set; }

    public UpdateState UpdateState { get; set; } = UpdateState.Created;

    public List<TodoContributor> Contributors { get; set; } = [];

    public List<TodoChecklistItem> Checklist { get; set; } = [];

    public List<TodoComment> Comments { get; set; } = [];

    public List<TodoAttachment> Attachments { get; set; } = [];

    public List<TodoDependency> Dependencies { get; set; } = [];

    /// <summary>Done or discarded: nothing is expected any more.</summary>
    public bool IsClosed => Status is TodoStatus.Done or TodoStatus.Discarded;

    /// <summary>Overdue is not a status: it follows from the due date and the status. A task that is done or discarded is never overdue.</summary>
    public bool IsOverdue(DateOnly today) => !IsClosed && DueDate is { } due && due < today;
}

public class TodoContributor : AuditedEntity
{
    public long TodoId { get; set; }

    public Todo? Todo { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }
}

public class TodoChecklistItem : AuditedEntity
{
    public long TodoId { get; set; }

    public Todo? Todo { get; set; }

    public string Text { get; set; } = "";

    public bool IsDone { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A remark on a task. The author is the creating user.</summary>
public class TodoComment : AuditedEntity
{
    public long TodoId { get; set; }

    public Todo? Todo { get; set; }

    public string Text { get; set; } = "";

    public User? CreateUser { get; set; }
}

public enum AttachmentKind
{
    Link,
    File,
}

public class TodoAttachment : AuditedEntity
{
    public long TodoId { get; set; }

    public Todo? Todo { get; set; }

    public AttachmentKind Kind { get; set; } = AttachmentKind.Link;

    public string Title { get; set; } = "";

    /// <summary>The external address of a link.</summary>
    public string? Url { get; set; }

    public long? StoredFileId { get; set; }

    public StoredFile? StoredFile { get; set; }
}

/// <summary>The task cannot be finished before <see cref="DependsOnTodoId"/> is.</summary>
public class TodoDependency : AuditedEntity
{
    public long TodoId { get; set; }

    public Todo? Todo { get; set; }

    public long DependsOnTodoId { get; set; }

    public Todo? DependsOnTodo { get; set; }
}

/// <summary>A file in the store (the data volume or a NAS). Rows with a <see cref="Token"/> are what a download link may name.</summary>
public class StoredFile : AuditedEntity
{
    public Guid Token { get; set; } = Guid.NewGuid();

    /// <summary>Where the store keeps it, relative to its root ("2026/10/3f2a...").</summary>
    public string StorageKey { get; set; } = "";

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = "";
}

public enum HistoryEntity
{
    Todo,
    Project,
    Goal,
    KeyResult,
}

public enum HistoryKind
{
    Created,
    Status,
    Owner,
    DueDate,
    Priority,
    Comment,
    Updated,
    Deleted,
}

/// <summary>
/// The history of an item: who changed the status, the owner or the due date, and when. Finished tasks keep it, so it can be seen later how
/// a date moved and who took over.
/// </summary>
public class HistoryEntry : AuditedEntity
{
    public HistoryEntity EntityType { get; set; }

    public long EntityId { get; set; }

    public DateTime OccurredDate { get; set; }

    public long? UserId { get; set; }

    public string? UserName { get; set; }

    public HistoryKind Kind { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Note { get; set; }
}
