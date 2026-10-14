using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Share;

/// <summary>
/// The page behind a share link (the role "Anonym"): a project or a goal, read only. It says in words why it cannot show something (a link that has run out is no secret),
/// and shows no comments, no history, no attachments and, when the link says so, no single todos.
/// </summary>
[AllowAnonymous]
public sealed class IndexModel(ShareService shares, NpDbContext db, PlanningService planning, TimeProvider clock) : PageModelBase
{
    [BindProperty(SupportsGet = true)]
    public Guid Token { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public ShareLink Link { get; private set; } = null!;

    public ShareState State { get; private set; } = ShareState.Unknown;

    public bool NeedsPassword { get; private set; }

    public bool Locked { get; private set; }

    public bool CanShow => State == ShareState.Active && !NeedsPassword;

    public string Organization => shares.OrganizationName;

    public DateOnly Today => planning.Today;

    public string RemainingText => ShareService.Remaining(Link, clock.GetUtcNow().UtcDateTime);

    // a project
    public Project? Project { get; private set; }

    public ProjectFacts? ProjectFacts { get; private set; }

    public List<Todo> Todos { get; private set; } = [];

    public List<KeyResult> KeyResults { get; private set; } = [];

    // a goal
    public Goal? Goal { get; private set; }

    public GoalFacts? GoalFacts { get; private set; }

    public List<(Project Project, ProjectFacts Facts)> GoalProjects { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (await InitAsync())
        {
            await shares.CountViewAsync(Link, HttpContext.RequestAborted);
            await LoadAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        await InitAsync();
        if (State != ShareState.Active)
        {
            return Page();
        }

        if (!NeedsPassword || shares.TryPassword(Link, Password, HttpContext))
        {
            return RedirectToPage(new { token = Token });
        }

        Locked = shares.IsLocked(Link);
        ModelState.AddModelError(nameof(Password), Locked ? "Zu viele falsche Versuche. Bitte warte einige Minuten." : "Das Passwort stimmt nicht.");
        return Page();
    }

    private async Task<bool> InitAsync()
    {
        ViewData["Wide"] = true;
        ViewData["Title"] = "Freigabe";
        var resolution = await shares.ResolveAsync(Token, HttpContext.RequestAborted);
        State = resolution.State;
        if (resolution.Link is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return false;
        }

        Link = resolution.Link;
        if (State != ShareState.Active)
        {
            Response.StatusCode = StatusCodes.Status410Gone;
            return false;
        }

        NeedsPassword = shares.NeedsPassword(Link, HttpContext);
        Locked = NeedsPassword && shares.IsLocked(Link);
        ViewData["Title"] = NeedsPassword ? "Freigabe" : Link.Title;
        return !NeedsPassword;
    }

    private async Task LoadAsync()
    {
        if (Link.Kind == ShareKind.Project && Link.ProjectId is { } projectId)
        {
            Project = await db.Project.AsNoTracking().AsSplitQuery().Include(p => p.OwnerUser).Include(p => p.Department).Include(p => p.Milestones).Include(p => p.Risks)
                .Include(p => p.Goals).ThenInclude(g => g.Goal).FirstAsync(p => p.Id == projectId);
            ProjectFacts = (await planning.ProjectFactsAsync([projectId]))[projectId];
            KeyResults = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Where(k => k.ProjectId == projectId).OrderBy(k => k.Title).ToListAsync();
            if (Link.ShowTodos)
            {
                Todos = await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Where(t => t.ProjectId == projectId && t.Status != TodoStatus.Discarded)
                    .OrderBy(t => t.Status == TodoStatus.Done).ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).Take(200).ToListAsync();
            }
        }
        else if (Link.GoalId is { } goalId)
        {
            Goal = await db.Goal.AsNoTracking().AsSplitQuery().Include(g => g.OwnerUser).Include(g => g.Department).Include(g => g.KeyResults).ThenInclude(k => k.CheckIns)
                .Include(g => g.Projects).ThenInclude(p => p.Project).ThenInclude(p => p!.OwnerUser).FirstAsync(g => g.Id == goalId);
            GoalFacts = (await planning.GoalFactsAsync([goalId]))[goalId];
            var projectIds = Goal.Projects.Where(p => p.Project is not null).Select(p => p.ProjectId).ToList();
            var facts = await planning.ProjectFactsAsync(projectIds);
            GoalProjects = [.. Goal.Projects.Where(p => p.Project is not null).Select(p => (p.Project!, facts[p.ProjectId]))];
        }
    }
}
