using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Todos;

/// <summary>
/// "My todos": what is mine (owner or contributor), by when. The tabs narrow it to today and this week, or widen it to the whole team. Finished and discarded
/// todos are hidden until the status filter asks for them.
/// </summary>
public sealed class IndexModel(NpDbContext db, ICurrentUser me, Lookups lookups, PlanningService planning) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public LinkTabsModel Tabs { get; private set; } = null!;

    public string Scope { get; private set; } = "alle";

    public bool ClosedHidden { get; private set; }

    public async Task OnGetAsync([FromQuery(Name = "scope")] string? scope)
    {
        Scope = scope is "heute" or "woche" or "team" ? scope : "alle";
        Head("Meine Todos", Scope == "team" ? "Alle Todos im Team." : "Was ich zu tun habe, und bis wann.", ("Meine Todos", null));
        ViewData["ActiveHref"] = "/Todos";

        var today = planning.Today;
        var endOfWeek = today.AddDays(7 - ((int)today.DayOfWeek + 6) % 7 - 1);
        var mine = me.UserId ?? 0;
        var all = db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.Project).Include(t => t.Goal).Include(t => t.KeyResult).AsQueryable();
        all = Scope switch
        {
            "heute" => all.Where(t => (t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded && t.DueDate != null && t.DueDate <= today),
            "woche" => all.Where(t => (t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded && t.DueDate != null && t.DueDate <= endOfWeek),
            "team" => all,
            _ => all.Where(t => t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)),
        };

        // Finished and discarded todos only show when somebody asks for a status.
        ClosedHidden = !Request.Query.ContainsKey("status") || string.IsNullOrEmpty(Request.Query["status"]);
        if (ClosedHidden)
        {
            all = all.Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        }

        var users = await lookups.UsersAsync();
        var projects = await lookups.ProjectsAsync();
        var spec = new ListSpec<Todo>
        {
            Id = "todos",
            Action = "/Todos",
            Label = "Todos",
            Noun = "Todos",
            SearchPlaceholder = "Titel oder Beschreibung ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(t => EF.Functions.ILike(t.Title, like) || EF.Functions.ILike(t.Description ?? "", like));
            },
            Filters =
            [
                new FilterSpec<Todo>("status", "Status", ListBuilder.EnumOptions<TodoStatus>(Labels.Of), (q, v) => q.Where(t => t.Status == Enum.Parse<TodoStatus>(v))),
                new FilterSpec<Todo>("prio", "Priorität", ListBuilder.EnumOptions<Level>(Labels.Of), (q, v) => q.Where(t => t.Priority == Enum.Parse<Level>(v))),
                new FilterSpec<Todo>("projekt", "Projekt", [.. projects.Select(p => new FilterOption(p.Value!, p.Text))], (q, v) => q.Where(t => t.ProjectId == long.Parse(v))),
                new FilterSpec<Todo>("owner", "Verantwortlich", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(t => t.OwnerUserId == long.Parse(v))),
                new FilterSpec<Todo>("faellig", "Fälligkeit",
                    [new("ueberfaellig", "Überfällig"), new("heute", "Heute"), new("woche", "Diese Woche"), new("ohne", "Ohne Termin")],
                    (q, v) => v switch
                    {
                        "ueberfaellig" => q.Where(t => t.DueDate != null && t.DueDate < today && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded),
                        "heute" => q.Where(t => t.DueDate == today),
                        "woche" => q.Where(t => t.DueDate != null && t.DueDate >= today && t.DueDate <= endOfWeek),
                        _ => q.Where(t => t.DueDate == null),
                    }),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Todo>, bool, IOrderedQueryable<Todo>>)>
            {
                ["due"] = ("Fälligkeit", (q, d) => (d ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)).ThenByDescending(t => t.Priority == Level.High)),
                ["prio"] = ("Priorität", (q, d) => d ? q.OrderBy(t => t.Priority == Level.High ? 0 : t.Priority == Level.Medium ? 1 : 2).ThenBy(t => t.DueDate) : q.OrderByDescending(t => t.Priority == Level.High ? 0 : t.Priority == Level.Medium ? 1 : 2).ThenBy(t => t.DueDate)),
                ["title"] = ("Titel", (q, d) => d ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title)),
                ["status"] = ("Status", (q, d) => d ? q.OrderByDescending(t => t.Status).ThenBy(t => t.DueDate) : q.OrderBy(t => t.Status).ThenBy(t => t.DueDate)),
                ["created"] = ("Angelegt", (q, d) => d ? q.OrderBy(t => t.CreateDate) : q.OrderByDescending(t => t.CreateDate)),
            },
            DefaultSort = "due",
            Keep = new Dictionary<string, string?> { ["scope"] = Scope == "alle" ? null : Scope },
        };

        var result = await ListBuilder.RunAsync(all, Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        Table = TodoTable.Build("todo-table", "Todos", result.Items, today, withProject: true, withOwner: true);
        Table = Table with
        {
            EmptyTitle = Toolbar.Total == 0 ? "Nichts zu tun" : "Nichts gefunden",
            EmptyText = Toolbar.Total == 0 ? "Hier erscheinen die Todos, für die du verantwortlich bist." : "Ändere die Suche oder den Filter.",
        };

        var openMine = await db.Todo.CountAsync(t => (t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        var dueToday = await db.Todo.CountAsync(t => (t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded && t.DueDate != null && t.DueDate <= today);
        var dueWeek = await db.Todo.CountAsync(t => (t.OwnerUserId == mine || t.Contributors.Any(c => c.UserId == mine)) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded && t.DueDate != null && t.DueDate <= endOfWeek);
        Tabs = new LinkTabsModel("Ansicht", [
            new("heute", "Heute", "/Todos?scope=heute", "clock", dueToday),
            new("woche", "Diese Woche", "/Todos?scope=woche", "calendar", dueWeek),
            new("alle", "Alle meine", "/Todos", "check-square", openMine),
            new("team", "Team", "/Todos?scope=team", "users"),
        ], Scope);
    }
}
