using NextProject.Configuration;
using NextProject.Core.Planning;
using NextProject.Data;
using NextProject.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>How many todos of a group are in which state. <see cref="Completion"/> is the indicator for the progress of a project; it is not the reaching of a goal.</summary>
public sealed record TodoCounts(int Total, int Open, int InProgress, int Blocked, int InReview, int Done, int Discarded, int Overdue)
{
    public static readonly TodoCounts None = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Todos that still have to be done: all but done and discarded.</summary>
    public int Pending => Total - Done - Discarded;

    public decimal Completion => Progress.Completion(Done, Discarded, Total);
}

/// <summary>One thing the software noticed about a project: <see cref="Tone"/> is "error" or "warn".</summary>
public sealed record Signal(string Tone, string Text);

/// <summary>What the pages show about a project, worked out from its todos, milestones, risks and key results by fixed rules.</summary>
public sealed record ProjectFacts(
    TodoCounts Todos,
    decimal? Attainment,
    int KeyResults,
    int Milestones,
    int MilestonesReached,
    int MilestonesLate,
    int ActiveRisks,
    int HighRisks,
    IReadOnlyList<Signal> Signals)
{
    public bool IsEndangered => Signals.Any(s => s.Tone == "error");
}

/// <summary>What the pages show about a goal.</summary>
public sealed record GoalFacts(decimal? Attainment, int KeyResults, int KeyResultsReached, int Projects, TodoCounts Todos);

/// <summary>
/// The rules of progress and of "what is in danger". Everything a page shows about progress comes from here, so that two pages never tell two stories.
/// A project's <b>completion</b> is the share of its todos that are done; the <b>attainment</b> is how far its key results are on their way to the target. They
/// are shown side by side and never merged: finished work is not reached goals.
/// </summary>
public sealed class PlanningService(NpDbContext db, ConfigStore config, TimeProvider clock)
{
    public DateOnly Today => Fmt.Today(clock);

    /// <summary>The todo counts of a query, grouped by project (todos without a project are not in the result).</summary>
    public async Task<Dictionary<long, TodoCounts>> CountsByProjectAsync(IReadOnlyCollection<long>? projectIds = null, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.Todo.AsNoTracking().Where(t => t.ProjectId != null);
        if (projectIds is not null)
        {
            query = query.Where(t => projectIds.Contains(t.ProjectId!.Value));
        }

        var rows = await query.GroupBy(t => t.ProjectId!.Value).Select(g => new
        {
            g.Key,
            Total = g.Count(),
            Open = g.Count(t => t.Status == TodoStatus.Open),
            InProgress = g.Count(t => t.Status == TodoStatus.InProgress),
            Blocked = g.Count(t => t.Status == TodoStatus.Blocked),
            InReview = g.Count(t => t.Status == TodoStatus.InReview),
            Done = g.Count(t => t.Status == TodoStatus.Done),
            Discarded = g.Count(t => t.Status == TodoStatus.Discarded),
            Overdue = g.Count(t => t.DueDate != null && t.DueDate < today && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded),
        }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new TodoCounts(r.Total, r.Open, r.InProgress, r.Blocked, r.InReview, r.Done, r.Discarded, r.Overdue));
    }

    /// <summary>The todo counts of a goal: its own todos, those of its key results and those of the projects that serve it.</summary>
    public async Task<TodoCounts> CountsForGoalAsync(long goalId, CancellationToken ct = default)
    {
        var today = Today;
        var projectIds = await db.ProjectGoal.AsNoTracking().Where(g => g.GoalId == goalId).Select(g => g.ProjectId).ToListAsync(ct);
        var todos = await db.Todo.AsNoTracking()
            .Where(t => t.GoalId == goalId || (t.KeyResult != null && t.KeyResult.GoalId == goalId) || (t.ProjectId != null && projectIds.Contains(t.ProjectId.Value)))
            .Select(t => new { t.Status, t.DueDate }).ToListAsync(ct);
        return Count(todos.Select(t => (t.Status, t.DueDate)), today);
    }

    public static TodoCounts Count(IEnumerable<(TodoStatus Status, DateOnly? Due)> todos, DateOnly today)
    {
        int total = 0, open = 0, progress = 0, blocked = 0, review = 0, done = 0, discarded = 0, overdue = 0;
        foreach (var (status, due) in todos)
        {
            total++;
            switch (status)
            {
                case TodoStatus.Open: open++; break;
                case TodoStatus.InProgress: progress++; break;
                case TodoStatus.Blocked: blocked++; break;
                case TodoStatus.InReview: review++; break;
                case TodoStatus.Done: done++; break;
                case TodoStatus.Discarded: discarded++; break;
            }

            if (due is { } d && d < today && status is not (TodoStatus.Done or TodoStatus.Discarded))
            {
                overdue++;
            }
        }

        return new TodoCounts(total, open, progress, blocked, review, done, discarded, overdue);
    }

    /// <summary>Facts about projects: todos, key results, milestones, risks and the signals that follow from them. One query per kind, not per project.</summary>
    public async Task<Dictionary<long, ProjectFacts>> ProjectFactsAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct = default)
    {
        var today = Today;
        var projects = await db.Project.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToListAsync(ct);
        var counts = await CountsByProjectAsync(projectIds, ct);
        var results = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Where(k => k.ProjectId != null && projectIds.Contains(k.ProjectId.Value)).ToListAsync(ct);
        var milestones = await db.Milestone.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId)).ToListAsync(ct);
        var risks = await db.Risk.AsNoTracking().Where(r => projectIds.Contains(r.ProjectId)).ToListAsync(ct);

        var facts = new Dictionary<long, ProjectFacts>();
        foreach (var project in projects)
        {
            var c = counts.GetValueOrDefault(project.Id, TodoCounts.None);
            var krs = results.Where(k => k.ProjectId == project.Id).ToList();
            var ms = milestones.Where(m => m.ProjectId == project.Id).ToList();
            var rs = risks.Where(r => r.ProjectId == project.Id && r.IsActive).ToList();
            var late = ms.Count(m => m.ReachedDate is null && m.DueDate < today);
            var signals = Signals(project, c, late, rs.Count(r => r.Severity == Level.High), today);
            facts[project.Id] = new ProjectFacts(c, Progress.Average(krs.Select(k => k.Attainment)), krs.Count, ms.Count, ms.Count(m => m.ReachedDate is not null), late, rs.Count, rs.Count(r => r.Severity == Level.High), signals);
        }

        return facts;
    }

    /// <summary>What speaks for "this project is in danger", by fixed rules: a closed project has none.</summary>
    public IReadOnlyList<Signal> Signals(Project project, TodoCounts todos, int lateMilestones, int highRisks, DateOnly today)
    {
        var list = new List<Signal>();
        if (project.IsClosed || project.Status == ProjectStatus.Paused)
        {
            return list;
        }

        if (project.EndDate is { } end && end < today)
        {
            list.Add(new("error", $"Das Enddatum ({Fmt.Date(end)}) ist überschritten."));
        }

        if (todos.Blocked > 0)
        {
            list.Add(new("warn", todos.Blocked == 1 ? "1 Aufgabe ist blockiert." : $"{todos.Blocked} Aufgaben sind blockiert."));
        }

        if (todos.Overdue > 0)
        {
            var tone = todos.Overdue >= config.Current.Organization.OverdueLimit ? "error" : "warn";
            list.Add(new(tone, todos.Overdue == 1 ? "1 Aufgabe ist überfällig." : $"{todos.Overdue} Aufgaben sind überfällig."));
        }

        if (lateMilestones > 0)
        {
            list.Add(new("warn", lateMilestones == 1 ? "1 Meilenstein ist überfällig." : $"{lateMilestones} Meilensteine sind überfällig."));
        }

        if (highRisks > 0)
        {
            list.Add(new("warn", highRisks == 1 ? "1 hohes Risiko ist offen." : $"{highRisks} hohe Risiken sind offen."));
        }

        if (project is { StartDate: { } start, EndDate: { } finish } && finish > start && today >= start && today <= finish && todos.Total - todos.Discarded >= 3)
        {
            var elapsed = (decimal)(today.DayNumber - start.DayNumber) / (finish.DayNumber - start.DayNumber);
            if (todos.Completion + 0.25m < elapsed)
            {
                list.Add(new("warn", $"Die Umsetzung liegt hinter der Zeit: {Fmt.Percent(todos.Completion)} der Aufgaben sind erledigt, {Fmt.Percent(elapsed)} der Zeit sind um."));
            }
        }

        return list;
    }

    /// <summary>Facts about goals, one query per kind.</summary>
    public async Task<Dictionary<long, GoalFacts>> GoalFactsAsync(IReadOnlyCollection<long> goalIds, CancellationToken ct = default)
    {
        var results = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Where(k => goalIds.Contains(k.GoalId)).ToListAsync(ct);
        var links = await db.ProjectGoal.AsNoTracking().Where(g => goalIds.Contains(g.GoalId)).Select(g => new { g.GoalId, g.ProjectId }).ToListAsync(ct);
        var facts = new Dictionary<long, GoalFacts>();
        foreach (var id in goalIds)
        {
            var krs = results.Where(k => k.GoalId == id).ToList();
            facts[id] = new GoalFacts(
                Progress.Average(krs.Select(k => k.Attainment)),
                krs.Count,
                krs.Count(k => k.Attainment >= 1m),
                links.Count(l => l.GoalId == id),
                await CountsForGoalAsync(id, ct));
        }

        return facts;
    }

    /// <summary>The percentage of the todos of a key result that were done now (written into a check-in); null without todos.</summary>
    public async Task<int?> DonePercentAsync(long keyResultId, CancellationToken ct = default)
    {
        var statuses = await db.Todo.AsNoTracking().Where(t => t.KeyResultId == keyResultId).Select(t => t.Status).ToListAsync(ct);
        var counted = statuses.Count(s => s != TodoStatus.Discarded);
        return counted == 0 ? null : (int)Math.Round(statuses.Count(s => s == TodoStatus.Done) * 100m / counted, MidpointRounding.AwayFromZero);
    }
}
