using NextProject.Core.Planning;

namespace NextProject.Data.Entities;

/// <summary>How much something matters, or how bad it is: priorities of todos and projects, the severity of a risk.</summary>
public enum Level
{
    Low,
    Medium,
    High,
}

/// <summary>An area of the company ("Vertrieb", "Entwicklung"): what a goal concerns, where a project and a person belong.</summary>
public class Department : AuditedEntity, ISoftDeletable
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public UpdateState UpdateState { get; set; } = UpdateState.Created;
}

public enum GoalStatus
{
    Planned,
    Active,
    Achieved,
    Dropped,
}

/// <summary>
/// A strategic goal: what the company wants to reach and why it is worth it. Its yearly and quarterly targets are the words the management agreed on; how far
/// it is reached is measured by its key results, not by how many todos are done.
/// </summary>
public class Goal : AuditedEntity, ISoftDeletable
{
    public string Title { get; set; } = "";

    /// <summary>Why this goal matters for the company (the added value).</summary>
    public string? Description { get; set; }

    public long? OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    public long? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public int Year { get; set; }

    public string? YearTarget { get; set; }

    public string? Q1Target { get; set; }

    public string? Q2Target { get; set; }

    public string? Q3Target { get; set; }

    public string? Q4Target { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public UpdateState UpdateState { get; set; } = UpdateState.Created;

    public List<KeyResult> KeyResults { get; set; } = [];

    public List<ProjectGoal> Projects { get; set; } = [];
}

/// <summary>
/// A key result: a measurable step towards a goal ("calls about order tracking: from 50 to 25 a week"). Its value is read at check-ins; the share of
/// the way from the start value to the target is the attainment.
/// </summary>
public class KeyResult : AuditedEntity, ISoftDeletable
{
    public long GoalId { get; set; }

    public Goal? Goal { get; set; }

    /// <summary>The project that works on this result, if there is one.</summary>
    public long? ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Title { get; set; } = "";

    public long? OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    /// <summary>What is measured ("calls per week").</summary>
    public string? Metric { get; set; }

    /// <summary>How the value is collected ("tally sheet", "ticket system").</summary>
    public string? Collection { get; set; }

    public string? Unit { get; set; }

    public decimal StartValue { get; set; }

    public decimal TargetValue { get; set; }

    public DateOnly? DueDate { get; set; }

    public UpdateState UpdateState { get; set; } = UpdateState.Created;

    public List<KeyResultCheckIn> CheckIns { get; set; } = [];

    /// <summary>The newest measured value, or the start value as long as nothing was measured.</summary>
    public decimal CurrentValue => CheckIns.OrderByDescending(c => c.CheckInDate).ThenByDescending(c => c.Id).Select(c => (decimal?)c.Value).FirstOrDefault() ?? StartValue;

    /// <summary>0 to 1: how far the current value is on the way from the start value to the target (it also works for a value that has to go down).</summary>
    public decimal Attainment => Progress.Attainment(StartValue, TargetValue, CurrentValue);
}

/// <summary>A reading of a key result: the measured value, and how much of the todos that serve it were done at that time.</summary>
public class KeyResultCheckIn : AuditedEntity
{
    public long KeyResultId { get; set; }

    public KeyResult? KeyResult { get; set; }

    public DateOnly CheckInDate { get; set; }

    public decimal Value { get; set; }

    /// <summary>Percent of the linked todos that were done at the time (the "key action done" of the planning sheet); null when no todo is linked.</summary>
    public int? DonePercent { get; set; }

    public string? Comment { get; set; }
}

public enum ProjectStatus
{
    Planning,
    InProgress,
    AtRisk,
    Paused,
    Completed,
    Cancelled,
}

public class Project : AuditedEntity, ISoftDeletable
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    /// <summary>What the project is meant to achieve.</summary>
    public string? Objective { get; set; }

    /// <summary>How the end result looks: how we know the project delivered.</summary>
    public string? ExpectedResult { get; set; }

    /// <summary>What the company gets out of it.</summary>
    public string? AddedValue { get; set; }

    /// <summary>The factors that help or hinder (+/-).</summary>
    public string? InfluenceFactors { get; set; }

    /// <summary>The owner's short report on where the project stands.</summary>
    public string? StatusNote { get; set; }

    public long OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    public long? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    public Level Priority { get; set; } = Level.Medium;

    /// <summary>The planned cost in euros.</summary>
    public decimal? Budget { get; set; }

    /// <summary>The planned effort ("40 person days").</summary>
    public string? Resources { get; set; }

    public UpdateState UpdateState { get; set; } = UpdateState.Created;

    public List<Todo> Todos { get; set; } = [];

    public List<ProjectMember> Members { get; set; } = [];

    public List<ProjectGoal> Goals { get; set; } = [];

    public List<KeyResult> KeyResults { get; set; } = [];

    public List<Milestone> Milestones { get; set; } = [];

    public List<Risk> Risks { get; set; } = [];

    public List<ProjectDependency> Dependencies { get; set; } = [];

    public bool IsClosed => Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
}

public class ProjectMember : AuditedEntity
{
    public long ProjectId { get; set; }

    public Project? Project { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }
}

/// <summary>A project can serve several goals.</summary>
public class ProjectGoal : AuditedEntity
{
    public long ProjectId { get; set; }

    public Project? Project { get; set; }

    public long GoalId { get; set; }

    public Goal? Goal { get; set; }
}

/// <summary>The project cannot finish before <see cref="DependsOnProjectId"/> has.</summary>
public class ProjectDependency : AuditedEntity
{
    public long ProjectId { get; set; }

    public Project? Project { get; set; }

    public long DependsOnProjectId { get; set; }

    public Project? DependsOnProject { get; set; }
}

/// <summary>A point on the way of a project. It is reached when somebody says so: finished todos are a hint, not the proof.</summary>
public class Milestone : AuditedEntity
{
    public long ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly? ReachedDate { get; set; }
}

public enum RiskStatus
{
    Open,
    Mitigated,
    Occurred,
    Closed,
}

public class Risk : AuditedEntity
{
    public long ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public Level Severity { get; set; } = Level.Medium;

    public RiskStatus Status { get; set; } = RiskStatus.Open;

    public string? Mitigation { get; set; }

    public long? OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    public bool IsActive => Status is RiskStatus.Open or RiskStatus.Occurred;
}

public enum ReviewHealth
{
    OnTrack,
    Watch,
    OffTrack,
}

/// <summary>
/// The regular look at a project or a goal: where does the implementation differ from the strategy and why, what did we learn, what do we adjust, how do we steer.
/// The four texts are the four columns of the review sheet.
/// </summary>
public class Review : AuditedEntity
{
    public long? ProjectId { get; set; }

    public Project? Project { get; set; }

    public long? GoalId { get; set; }

    public Goal? Goal { get; set; }

    public DateOnly ReviewDate { get; set; }

    public ReviewHealth Health { get; set; } = ReviewHealth.OnTrack;

    /// <summary>Implementation against strategy: where is the cause of a deviation?</summary>
    public string? Deviation { get; set; }

    /// <summary>What do we learn from the implementation?</summary>
    public string? Learnings { get; set; }

    /// <summary>Where do we have to adjust?</summary>
    public string? Adjustments { get; set; }

    /// <summary>How does the strategy stay alive?</summary>
    public string? Steering { get; set; }

    public DateOnly? NextReviewDate { get; set; }

    /// <summary>The author is the creating user.</summary>
    public User? CreateUser { get; set; }
}
