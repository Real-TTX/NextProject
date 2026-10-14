using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Goals;

/// <summary>
/// One goal: its targets, the key results that measure it (with the latest value), the projects that serve it, the todos that belong to it and the reviews. How far it is
/// reached is the average of its key results; the todos that are done are shown next to it as the work behind it.
/// </summary>
public sealed class DetailsModel(NpDbContext db, PlanningService planning, HistoryService history, Lookups lookups) : PageModelBase
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public Goal Goal { get; private set; } = null!;

    public GoalFacts Facts { get; private set; } = null!;

    public Dictionary<long, ProjectFacts> ProjectFacts { get; private set; } = [];

    public List<Review> Reviews { get; private set; } = [];

    public List<HistoryEntry> History { get; private set; } = [];

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel TodosTable { get; private set; } = null!;

    public string Tab { get; private set; } = "overview";

    public DateOnly Today => planning.Today;

    public async Task<IActionResult> OnGetAsync()
    {
        var goal = await db.Goal.AsNoTracking().AsSplitQuery()
            .Include(g => g.OwnerUser).Include(g => g.Department)
            .Include(g => g.KeyResults).ThenInclude(k => k.CheckIns).Include(g => g.KeyResults).ThenInclude(k => k.Project).Include(g => g.KeyResults).ThenInclude(k => k.OwnerUser)
            .Include(g => g.Projects).ThenInclude(p => p.Project).ThenInclude(p => p!.OwnerUser)
            .FirstOrDefaultAsync(g => g.Id == Id);
        if (goal is null)
        {
            return NotFound();
        }

        Goal = goal;
        Tab = Request.Query["tab"].ToString() is { Length: > 0 } tab ? tab : "overview";
        Head(goal.Title, null, ("Ziele & OKRs", "/Goals"), (goal.Title, null));
        ViewData["ActiveHref"] = "/Goals";
        ViewData["OwnHead"] = true;

        Facts = (await planning.GoalFactsAsync([Id]))[Id];
        ProjectFacts = await planning.ProjectFactsAsync([.. goal.Projects.Where(p => p.Project is not null).Select(p => p.ProjectId)]);
        Reviews = await db.Review.AsNoTracking().Include(r => r.CreateUser).Where(r => r.GoalId == Id).OrderByDescending(r => r.ReviewDate).ToListAsync();
        History = await history.ForAsync(HistoryEntity.Goal, Id);
        await BuildTodoListAsync(goal);
        return Page();
    }

    private async Task BuildTodoListAsync(Goal goal)
    {
        var users = await lookups.UsersAsync();
        var today = Today;
        var projectIds = goal.Projects.Select(p => p.ProjectId).ToList();
        var id = Id;
        var spec = new ListSpec<Todo>
        {
            Id = "goal-todos",
            Action = "/Goals/Details",
            Label = "Todos des Ziels",
            Noun = "Todos",
            SearchPlaceholder = "Titel ...",
            Search = (q, text) => q.Where(t => EF.Functions.ILike(t.Title, Like.Contains(text))),
            Filters =
            [
                new FilterSpec<Todo>("status", "Status", [new("offen", "Offen (nicht erledigt)"), .. ListBuilder.EnumOptions<TodoStatus>(Labels.Of)],
                    (q, v) => v == "offen" ? q.Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded) : q.Where(t => t.Status == Enum.Parse<TodoStatus>(v))),
                new FilterSpec<Todo>("owner", "Verantwortlich", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(t => t.OwnerUserId == long.Parse(v))),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Todo>, bool, IOrderedQueryable<Todo>>)>
            {
                ["due"] = ("Fälligkeit", (q, d) => d ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)),
                ["title"] = ("Titel", (q, d) => d ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title)),
            },
            DefaultSort = "due",
            PageSize = 15,
            Keep = new Dictionary<string, string?> { ["id"] = Id.ToString(), ["tab"] = "todos" },
        };

        var all = db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.Project).Include(t => t.KeyResult)
            .Where(t => t.GoalId == id || (t.KeyResult != null && t.KeyResult.GoalId == id) || (t.ProjectId != null && projectIds.Contains(t.ProjectId.Value)));
        var result = await ListBuilder.RunAsync(all, Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        TodosTable = TodoTable.Build("goal-todo-table", "Todos des Ziels", result.Items, today, withProject: true);
    }
}
