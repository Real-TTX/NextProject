using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Projects;

/// <summary>
/// One project from every side: the overview (what it is for, how far it is, what the software noticed), its todos as a list, a board and a schedule, the milestones, the
/// risks, the goals and key results it works on, and its history. Finished todos and reached goals are shown side by side and never as one number.
/// </summary>
public sealed class DetailsModel(NpDbContext db, PlanningService planning, HistoryService history, Lookups lookups) : PageModelBase
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public Project Project { get; private set; } = null!;

    public ProjectFacts Facts { get; private set; } = null!;

    public DateOnly Today => planning.Today;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel TodosTable { get; private set; } = null!;

    public BoardView Board { get; private set; } = null!;

    public TimelineView? Timeline { get; private set; }

    public List<HistoryEntry> History { get; private set; } = [];

    public List<KeyResult> KeyResults { get; private set; } = [];

    public List<Todo> OpenBlocked { get; private set; } = [];

    public Dictionary<long, int> MilestoneTodos { get; private set; } = [];

    public string Tab { get; private set; } = "overview";

    public async Task<IActionResult> OnGetAsync()
    {
        var project = await db.Project.AsNoTracking().AsSplitQuery()
            .Include(p => p.OwnerUser).Include(p => p.Department).Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Goals).ThenInclude(g => g.Goal).Include(p => p.Milestones).Include(p => p.Risks).ThenInclude(r => r.OwnerUser)
            .Include(p => p.Dependencies).ThenInclude(d => d.DependsOnProject)
            .FirstOrDefaultAsync(p => p.Id == Id);
        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        Tab = Request.Query["tab"].ToString() is { Length: > 0 } tab ? tab : "overview";
        Head(project.Name, null, ("Projekte", "/Projects"), (project.Name, null));
        ViewData["ActiveHref"] = "/Projects";
        ViewData["OwnHead"] = true;

        Facts = (await planning.ProjectFactsAsync([Id]))[Id];
        KeyResults = await db.KeyResult.AsNoTracking().Include(k => k.Goal).Include(k => k.CheckIns).Include(k => k.OwnerUser).Where(k => k.ProjectId == Id).OrderBy(k => k.Title).ToListAsync();
        History = await history.ForAsync(HistoryEntity.Project, Id);
        OpenBlocked = await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Where(t => t.ProjectId == Id && t.Status == TodoStatus.Blocked).ToListAsync();
        MilestoneTodos = await db.Todo.AsNoTracking().Where(t => t.ProjectId == Id && t.MilestoneId != null).GroupBy(t => t.MilestoneId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        var allTodos = await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Where(t => t.ProjectId == Id).ToListAsync();
        Board = new BoardView("project-board", allTodos, Today, ShowProject: false);
        Timeline = TimelineView.Build(project, allTodos, project.Milestones, Today);
        await BuildTodoListAsync();
        return Page();
    }

    private async Task BuildTodoListAsync()
    {
        var users = await lookups.UsersAsync();
        var today = Today;
        var spec = new ListSpec<Todo>
        {
            Id = "project-todos",
            Action = "/Projects/Details",
            Label = "Todos des Projekts",
            Noun = "Todos",
            SearchPlaceholder = "Titel oder Beschreibung ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(t => EF.Functions.ILike(t.Title, like) || EF.Functions.ILike(t.Description ?? "", like));
            },
            Filters =
            [
                new FilterSpec<Todo>("status", "Status", [new("offen", "Offen (nicht erledigt)"), .. ListBuilder.EnumOptions<TodoStatus>(Labels.Of)],
                    (q, v) => v == "offen" ? q.Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded) : q.Where(t => t.Status == Enum.Parse<TodoStatus>(v))),
                new FilterSpec<Todo>("prio", "Priorität", ListBuilder.EnumOptions<Level>(Labels.Of), (q, v) => q.Where(t => t.Priority == Enum.Parse<Level>(v))),
                new FilterSpec<Todo>("owner", "Verantwortlich", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(t => t.OwnerUserId == long.Parse(v))),
                new FilterSpec<Todo>("faellig", "Fälligkeit", [new("ueberfaellig", "Überfällig"), new("ohne", "Ohne Termin")],
                    (q, v) => v == "ueberfaellig" ? q.Where(t => t.DueDate != null && t.DueDate < today && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded) : q.Where(t => t.DueDate == null)),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Todo>, bool, IOrderedQueryable<Todo>>)>
            {
                ["due"] = ("Fälligkeit", (q, d) => d ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)),
                ["status"] = ("Status", (q, d) => d ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status)),
                ["title"] = ("Titel", (q, d) => d ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title)),
            },
            DefaultSort = "due",
            PageSize = 15,
            Keep = new Dictionary<string, string?> { ["id"] = Id.ToString(), ["tab"] = "todos" },
        };

        var result = await ListBuilder.RunAsync(db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.KeyResult).Include(t => t.Milestone).Where(t => t.ProjectId == Id), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        TodosTable = TodoTable.Build("project-todo-table", "Todos des Projekts", result.Items, today, withProject: false);
    }
}
