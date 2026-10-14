using NextProject.Core.Planning;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>What the calendar shows and what is left out. <see cref="Mine"/> (and no <see cref="OwnerId"/>) narrows it to the dates that concern <see cref="UserId"/>.</summary>
public sealed record CalendarFilter(
    long UserId,
    bool Mine = true,
    long? ProjectId = null,
    long? OwnerId = null,
    long? DepartmentId = null,
    long? GoalId = null,
    string? Kind = null,
    string? State = null,
    string? Text = null);

/// <summary>One date in the calendar. <see cref="Kind"/> is todo, milestone, project, keyresult or review; <see cref="Movable"/> says that its date can be changed in the calendar.</summary>
public sealed record CalendarItem(string Id, string Kind, long EntityId, string Title, DateOnly Start, DateOnly? End, string Tone, string? Status, string Url, string? Note, bool Movable, string? Description, bool Done);

/// <summary>
/// The dates of the work from every place that has one: todos (from the start to the due date), milestones, the end of a project, key results that are due and the next
/// reviews. The calendar page, the file download and the subscription (iCal) ask this one service, so that they show the same.
/// </summary>
public sealed class CalendarService(NpDbContext db, TimeProvider clock)
{
    public static readonly string[] Kinds = ["todo", "milestone", "project", "keyresult", "review"];

    public static string KindLabel(string kind) => kind switch
    {
        "todo" => "Todos",
        "milestone" => "Meilensteine",
        "project" => "Projektenden",
        "keyresult" => "Schlüsselergebnisse",
        _ => "Reviews",
    };

    public async Task<List<CalendarItem>> GetAsync(CalendarFilter f, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var today = Fmt.Today(clock);
        var items = new List<CalendarItem>();
        if (Wants(f, "todo"))
        {
            items.AddRange(await TodosAsync(f, from, to, today, ct));
        }

        if (Wants(f, "milestone"))
        {
            items.AddRange(await MilestonesAsync(f, from, to, today, ct));
        }

        if (Wants(f, "project"))
        {
            items.AddRange(await ProjectsAsync(f, from, to, ct));
        }

        if (Wants(f, "keyresult"))
        {
            items.AddRange(await KeyResultsAsync(f, from, to, ct));
        }

        if (Wants(f, "review") && f.State is not "erledigt")
        {
            items.AddRange(await ReviewsAsync(f, from, to, today, ct));
        }

        return items;
    }

    private static bool Wants(CalendarFilter f, string kind) => string.IsNullOrEmpty(f.Kind) || f.Kind == kind;

    private static bool OnlyMine(CalendarFilter f) => f.Mine && f.OwnerId is null;

    // ------------------------------------------------------------------------------------------------ todos

    private async Task<IEnumerable<CalendarItem>> TodosAsync(CalendarFilter f, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var me = f.UserId;
        var q = db.Todo.AsNoTracking().Include(t => t.Project).Include(t => t.OwnerUser)
            .Where(t => t.DueDate != null && t.DueDate >= from && (t.StartDate ?? t.DueDate) <= to && t.Status != TodoStatus.Discarded);
        if (OnlyMine(f))
        {
            q = q.Where(t => t.OwnerUserId == me || t.Contributors.Any(c => c.UserId == me));
        }

        if (f.OwnerId is { } owner)
        {
            q = q.Where(t => t.OwnerUserId == owner || t.Contributors.Any(c => c.UserId == owner));
        }

        if (f.ProjectId is { } project)
        {
            q = q.Where(t => t.ProjectId == project);
        }

        if (f.DepartmentId is { } department)
        {
            q = q.Where(t => t.OwnerUser!.DepartmentId == department || (t.Project != null && t.Project.DepartmentId == department));
        }

        if (f.GoalId is { } goal)
        {
            q = q.Where(t => t.GoalId == goal || (t.KeyResult != null && t.KeyResult.GoalId == goal) || (t.Project != null && t.Project.Goals.Any(g => g.GoalId == goal)));
        }

        q = f.State switch
        {
            "offen" => q.Where(t => t.Status != TodoStatus.Done),
            "erledigt" => q.Where(t => t.Status == TodoStatus.Done),
            "ueberfaellig" => q.Where(t => t.DueDate < today && t.Status != TodoStatus.Done),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var like = Like.Contains(f.Text);
            q = q.Where(t => EF.Functions.ILike(t.Title, like));
        }

        var list = await q.OrderBy(t => t.DueDate).Take(2000).ToListAsync(ct);
        return list.Select(t =>
        {
            var overdue = t.IsOverdue(today);
            var tone = t.Status == TodoStatus.Done ? "ok" : t.Status == TodoStatus.Blocked || overdue ? "error" : t.Status == TodoStatus.InProgress ? "info" : "";
            var start = t.StartDate is { } s && s < t.DueDate ? s : t.DueDate!.Value;
            var description = $"Status: {Labels.Of(t.Status)}{(overdue ? " (überfällig)" : "")} · Verantwortlich: {t.OwnerUser?.DisplayName}{(t.Project is not null ? " · Projekt: " + t.Project.Name : "")}";
            return new CalendarItem($"t{t.Id}", "todo", t.Id, t.Title, start, start < t.DueDate ? t.DueDate : null, tone, Labels.Of(t.Status) + (overdue ? ", überfällig" : ""), $"/Todos/Details?id={t.Id}",
                t.Project?.Name, !t.IsClosed, description, t.Status == TodoStatus.Done);
        });
    }

    // ------------------------------------------------------------------------------------------------ milestones, project ends

    private async Task<IEnumerable<CalendarItem>> MilestonesAsync(CalendarFilter f, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var me = f.UserId;
        var q = db.Milestone.AsNoTracking().Include(m => m.Project).Where(m => m.DueDate >= from && m.DueDate <= to);
        if (OnlyMine(f))
        {
            q = q.Where(m => m.Project!.OwnerUserId == me || m.Project.Members.Any(x => x.UserId == me));
        }

        if (f.OwnerId is { } owner)
        {
            q = q.Where(m => m.Project!.OwnerUserId == owner || m.Project.Members.Any(x => x.UserId == owner));
        }

        if (f.ProjectId is { } project)
        {
            q = q.Where(m => m.ProjectId == project);
        }

        if (f.DepartmentId is { } department)
        {
            q = q.Where(m => m.Project!.DepartmentId == department);
        }

        if (f.GoalId is { } goal)
        {
            q = q.Where(m => m.Project!.Goals.Any(g => g.GoalId == goal));
        }

        q = f.State switch
        {
            "offen" => q.Where(m => m.ReachedDate == null),
            "erledigt" => q.Where(m => m.ReachedDate != null),
            "ueberfaellig" => q.Where(m => m.ReachedDate == null && m.DueDate < today),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var like = Like.Contains(f.Text);
            q = q.Where(m => EF.Functions.ILike(m.Title, like));
        }

        return (await q.ToListAsync(ct)).Select(m => new CalendarItem($"m{m.Id}", "milestone", m.Id, "Meilenstein: " + m.Title, m.DueDate, null,
            m.ReachedDate is not null ? "ok" : m.DueDate < today ? "error" : "accent", m.ReachedDate is not null ? "erreicht" : m.DueDate < today ? "überfällig" : "Meilenstein",
            $"/Projects/Milestone?projectId={m.ProjectId}&id={m.Id}", m.Project?.Name, false, m.Description, m.ReachedDate is not null));
    }

    private async Task<IEnumerable<CalendarItem>> ProjectsAsync(CalendarFilter f, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var me = f.UserId;
        var today = Fmt.Today(clock);
        var q = db.Project.AsNoTracking().Where(p => p.EndDate != null && p.EndDate >= from && p.EndDate <= to && p.Status != ProjectStatus.Cancelled);
        if (OnlyMine(f))
        {
            q = q.Where(p => p.OwnerUserId == me || p.Members.Any(x => x.UserId == me));
        }

        if (f.OwnerId is { } owner)
        {
            q = q.Where(p => p.OwnerUserId == owner || p.Members.Any(x => x.UserId == owner));
        }

        if (f.ProjectId is { } project)
        {
            q = q.Where(p => p.Id == project);
        }

        if (f.DepartmentId is { } department)
        {
            q = q.Where(p => p.DepartmentId == department);
        }

        if (f.GoalId is { } goal)
        {
            q = q.Where(p => p.Goals.Any(g => g.GoalId == goal));
        }

        q = f.State switch
        {
            "offen" => q.Where(p => p.Status != ProjectStatus.Completed),
            "erledigt" => q.Where(p => p.Status == ProjectStatus.Completed),
            "ueberfaellig" => q.Where(p => p.EndDate < today && p.Status != ProjectStatus.Completed),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, Like.Contains(f.Text)));
        }

        return (await q.ToListAsync(ct)).Select(p => new CalendarItem($"p{p.Id}", "project", p.Id, "Projektende: " + p.Name, p.EndDate!.Value, null,
            p.Status == ProjectStatus.Completed ? "ok" : p.EndDate < today ? "error" : "accent", Labels.Of(p.Status), $"/Projects/Details?id={p.Id}", null, false, p.Objective, p.Status == ProjectStatus.Completed));
    }

    // ------------------------------------------------------------------------------------------------ key results, reviews

    private async Task<IEnumerable<CalendarItem>> KeyResultsAsync(CalendarFilter f, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var me = f.UserId;
        var today = Fmt.Today(clock);
        var q = db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Include(k => k.Goal).Where(k => k.DueDate != null && k.DueDate >= from && k.DueDate <= to);
        if (OnlyMine(f))
        {
            q = q.Where(k => k.OwnerUserId == me || k.Goal!.OwnerUserId == me);
        }

        if (f.OwnerId is { } owner)
        {
            q = q.Where(k => k.OwnerUserId == owner);
        }

        if (f.ProjectId is { } project)
        {
            q = q.Where(k => k.ProjectId == project);
        }

        if (f.DepartmentId is { } department)
        {
            q = q.Where(k => k.Goal!.DepartmentId == department);
        }

        if (f.GoalId is { } goal)
        {
            q = q.Where(k => k.GoalId == goal);
        }

        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            q = q.Where(k => EF.Functions.ILike(k.Title, Like.Contains(f.Text)));
        }

        var list = (await q.ToListAsync(ct)).Where(k => f.State switch
        {
            "offen" => k.Attainment < 1m,
            "erledigt" => k.Attainment >= 1m,
            "ueberfaellig" => k.Attainment < 1m && k.DueDate < today,
            _ => true,
        });
        return list.Select(k => new CalendarItem($"k{k.Id}", "keyresult", k.Id, "Schlüsselergebnis: " + k.Title, k.DueDate!.Value, null,
            k.Attainment >= 1m ? "ok" : k.DueDate < today ? "error" : "info", Fmt.Percent(k.Attainment) + " erreicht", $"/Goals/KeyResult?id={k.Id}", k.Goal?.Title, false,
            $"{Fmt.Number(k.CurrentValue, 1)} von {Fmt.Number(k.TargetValue, 1)} {k.Unit}".Trim(), k.Attainment >= 1m));
    }

    private async Task<IEnumerable<CalendarItem>> ReviewsAsync(CalendarFilter f, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var me = f.UserId;
        var q = db.Review.AsNoTracking().Include(r => r.Project).Include(r => r.Goal).Where(r => r.NextReviewDate != null && r.NextReviewDate >= from && r.NextReviewDate <= to);
        if (OnlyMine(f))
        {
            q = q.Where(r => r.CreateUserId == me || (r.Project != null && (r.Project.OwnerUserId == me || r.Project.Members.Any(x => x.UserId == me))) || (r.Goal != null && r.Goal.OwnerUserId == me));
        }

        if (f.OwnerId is { } owner)
        {
            q = q.Where(r => r.CreateUserId == owner || (r.Project != null && r.Project.OwnerUserId == owner) || (r.Goal != null && r.Goal.OwnerUserId == owner));
        }

        if (f.ProjectId is { } project)
        {
            q = q.Where(r => r.ProjectId == project);
        }

        if (f.DepartmentId is { } department)
        {
            q = q.Where(r => (r.Project != null && r.Project.DepartmentId == department) || (r.Goal != null && r.Goal.DepartmentId == department));
        }

        if (f.GoalId is { } goal)
        {
            q = q.Where(r => r.GoalId == goal || (r.Project != null && r.Project.Goals.Any(g => g.GoalId == goal)));
        }

        if (f.State == "ueberfaellig")
        {
            q = q.Where(r => r.NextReviewDate < today);
        }

        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var like = Like.Contains(f.Text);
            q = q.Where(r => (r.Project != null && EF.Functions.ILike(r.Project.Name, like)) || (r.Goal != null && EF.Functions.ILike(r.Goal.Title, like)));
        }

        return (await q.ToListAsync(ct)).Select(r => new CalendarItem($"r{r.Id}", "review", r.Id, "Nächstes Review: " + (r.Project?.Name ?? r.Goal?.Title), r.NextReviewDate!.Value, null,
            r.NextReviewDate < today ? "error" : "warn", r.NextReviewDate < today ? "überfällig" : "Review", $"/Reviews/Edit?id={r.Id}", null, false, null, false));
    }
}
