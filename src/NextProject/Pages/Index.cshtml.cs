using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages;

/// <summary>The start page: what is mine today, how my projects stand, whether the goals are on their way and where the team is stuck.</summary>
public sealed class IndexModel(NpDbContext db, ICurrentUser me, PlanningService planning, Configuration.ConfigStore config) : PageModelBase
{
    public sealed record ProjectLine(Project Project, ProjectFacts Facts);

    public string FirstName { get; private set; } = "";

    public DateOnly Today { get; private set; }

    public int MyOpen { get; private set; }

    public int MyOverdue { get; private set; }

    public int MyProjects { get; private set; }

    public int MyProjectsAtRisk { get; private set; }

    public int ActiveGoals { get; private set; }

    public int KeyResults { get; private set; }

    public int KeyResultsLate { get; private set; }

    public int TeamBlocked { get; private set; }

    public int TeamOverdue { get; private set; }

    public string TeamState => TeamBlocked == 0 && TeamOverdue == 0 ? "Gut" : TeamBlocked > 0 && TeamOverdue > config.Current.Organization.OverdueLimit ? "Achtung" : "Hinweise";

    public List<Todo> TodayTodos { get; private set; } = [];

    public List<Todo> SoonTodos { get; private set; } = [];

    public List<ProjectLine> Projects { get; private set; } = [];

    public List<Todo> Blocked { get; private set; } = [];

    public List<Risk> Risks { get; private set; } = [];

    public bool IsEmpty { get; private set; }

    public async Task OnGetAsync()
    {
        Head("Dashboard", null);
        ViewData["Crumbs"] = Crumbs.Of();
        ViewData["OwnHead"] = true;
        var meId = me.UserId ?? 0;
        Today = planning.Today;
        var today = Today;
        var soon = today.AddDays(Math.Max(1, config.Current.Organization.DueSoonDays));
        FirstName = (me.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        var mine = db.Todo.AsNoTracking().Where(t => (t.OwnerUserId == meId || t.Contributors.Any(c => c.UserId == meId)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        MyOpen = await mine.CountAsync();
        MyOverdue = await mine.CountAsync(t => t.DueDate != null && t.DueDate < today);
        TodayTodos = await mine.Include(t => t.Project).Include(t => t.OwnerUser).Where(t => (t.DueDate != null && t.DueDate <= today) || t.Status == TodoStatus.InProgress || t.Status == TodoStatus.Blocked)
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority == Level.High).Take(8).ToListAsync();
        SoonTodos = await mine.Include(t => t.Project).Include(t => t.OwnerUser).Where(t => t.DueDate != null && t.DueDate > today && t.DueDate <= soon).OrderBy(t => t.DueDate).Take(6).ToListAsync();

        var myProjects = await db.Project.AsNoTracking().Include(p => p.OwnerUser)
            .Where(p => (p.OwnerUserId == meId || p.Members.Any(m => m.UserId == meId)) && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled)
            .OrderBy(p => p.EndDate == null).ThenBy(p => p.EndDate).Take(8).ToListAsync();
        var facts = await planning.ProjectFactsAsync([.. myProjects.Select(p => p.Id)]);
        Projects = [.. myProjects.Select(p => new ProjectLine(p, facts[p.Id]))];
        MyProjects = await db.Project.CountAsync(p => (p.OwnerUserId == meId || p.Members.Any(m => m.UserId == meId)) && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled);
        MyProjectsAtRisk = await db.Project.CountAsync(p => (p.OwnerUserId == meId || p.Members.Any(m => m.UserId == meId)) && p.Status == ProjectStatus.AtRisk);

        ActiveGoals = await db.Goal.CountAsync(g => g.Status == GoalStatus.Active);
        var results = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Where(k => k.Goal!.Status == GoalStatus.Active).ToListAsync();
        KeyResults = results.Count;
        KeyResultsLate = results.Count(k => k.DueDate is { } d && d < today && k.Attainment < 1m);

        var open = db.Todo.AsNoTracking().Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        TeamBlocked = await open.CountAsync(t => t.Status == TodoStatus.Blocked);
        TeamOverdue = await open.CountAsync(t => t.DueDate != null && t.DueDate < today);
        Blocked = await open.Include(t => t.Project).Include(t => t.OwnerUser).Where(t => t.Status == TodoStatus.Blocked).OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).Take(5).ToListAsync();
        Risks = await db.Risk.AsNoTracking().Include(r => r.Project).Where(r => r.Severity == Level.High && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Occurred)).OrderBy(r => r.Project!.Name).Take(5).ToListAsync();

        IsEmpty = !await db.Goal.AnyAsync() && !await db.Project.AnyAsync() && !await db.Todo.AnyAsync();
    }
}
