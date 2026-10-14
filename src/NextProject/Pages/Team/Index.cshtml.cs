using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Team;

/// <summary>Who has what to do: the open, overdue and blocked todos of everybody, and the projects they own. Not a ranking: it shows where help is needed.</summary>
public sealed class IndexModel(NpDbContext db, Lookups lookups, PlanningService planning) : PageModelBase
{
    private sealed record Load(int Open, int Overdue, int Blocked, int DoneMonth, int Projects);

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Head("Team", "Wer was zu tun hat, und wo Hilfe gebraucht wird.", ("Team", null));
        ViewData["ActiveHref"] = "/Team";

        var departments = await lookups.DepartmentsAsync();
        var spec = new ListSpec<User>
        {
            Id = "team",
            Action = "/Team",
            Label = "Team",
            Noun = "Mitarbeiter",
            SearchPlaceholder = "Name oder Funktion ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(u => EF.Functions.ILike(u.DisplayName, like) || EF.Functions.ILike(u.JobTitle ?? "", like));
            },
            Filters = [new FilterSpec<User>("abteilung", "Abteilung", [.. departments.Select(d => new FilterOption(d.Value!, d.Text))], (q, v) => q.Where(u => u.DepartmentId == long.Parse(v)))],
            Sorts = new Dictionary<string, (string, Func<IQueryable<User>, bool, IOrderedQueryable<User>>)>
            {
                ["name"] = ("Name", (q, d) => d ? q.OrderByDescending(u => u.DisplayName) : q.OrderBy(u => u.DisplayName)),
                ["department"] = ("Abteilung", (q, d) => d ? q.OrderByDescending(u => u.Department!.Name).ThenBy(u => u.DisplayName) : q.OrderBy(u => u.Department!.Name).ThenBy(u => u.DisplayName)),
            },
            DefaultSort = "name",
        };

        var result = await ListBuilder.RunAsync(db.User.AsNoTracking().Include(u => u.Department).Where(u => u.IsActive), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;

        var ids = result.Items.Select(u => u.Id).ToList();
        var today = planning.Today;
        var monthAgo = DateTime.UtcNow.AddDays(-30);
        var todos = await db.Todo.AsNoTracking().Where(t => ids.Contains(t.OwnerUserId)).Select(t => new { t.OwnerUserId, t.Status, t.DueDate, t.CompletedDate }).ToListAsync();
        var projects = await db.Project.AsNoTracking().Where(p => ids.Contains(p.OwnerUserId) && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled).GroupBy(p => p.OwnerUserId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var loads = ids.ToDictionary(id => id, id =>
        {
            var mine = todos.Where(t => t.OwnerUserId == id).ToList();
            var open = mine.Where(t => t.Status is not (TodoStatus.Done or TodoStatus.Discarded)).ToList();
            return new Load(open.Count, open.Count(t => t.DueDate is { } d && d < today), open.Count(t => t.Status == TodoStatus.Blocked), mine.Count(t => t.Status == TodoStatus.Done && t.CompletedDate >= monthAgo), projects.GetValueOrDefault(id));
        });

        Table = TableModel.Of("team-table", "Team", result.Items,
        [
            (new TableColumn("Person"), u => Ui.Concat(Ui.Person(u.DisplayName), Sub(u.JobTitle))),
            (new TableColumn("Abteilung", HideOnPhone: true), u => Ui.Text(u.Department?.Name)),
            (new TableColumn("Offene Todos", Numeric: true), u => Ui.Link(loads[u.Id].Open.ToString(), $"/Todos?scope=team&owner={u.Id}")),
            (new TableColumn("Überfällig", Numeric: true), u => Cell(loads[u.Id].Overdue, "np-overdue")),
            (new TableColumn("Blockiert", Numeric: true), u => Cell(loads[u.Id].Blocked, "np-overdue")),
            (new TableColumn("Erledigt (30 Tage)", Numeric: true, HideOnPhone: true), u => Ui.Text(loads[u.Id].DoneMonth.ToString())),
            (new TableColumn("Projekte als Owner", Numeric: true, HideOnPhone: true), u => Ui.Text(loads[u.Id].Projects.ToString())),
        ])
        with
        {
            EmptyTitle = "Niemand gefunden",
            EmptyText = "Ändere die Suche oder den Filter.",
            EmptyIcon = "users",
        };
    }

    private static Microsoft.AspNetCore.Html.IHtmlContent Sub(string? text)
        => string.IsNullOrWhiteSpace(text) ? Ui.Text("") : new Microsoft.AspNetCore.Html.HtmlString("<small class=\"mat-muted np-sub\">" + System.Net.WebUtility.HtmlEncode(text) + "</small>");

    private static Microsoft.AspNetCore.Html.IHtmlContent Cell(int count, string cssWhenNotZero)
        => count == 0 ? Ui.Text("0") : new Microsoft.AspNetCore.Html.HtmlString($"<strong class=\"{cssWhenNotZero}\">{count}</strong>");
}
