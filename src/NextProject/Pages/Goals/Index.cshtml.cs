using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Goals;

/// <summary>The goals of the company and how far they are reached. The reaching is measured by key results; a goal without one is "not measured", not "0 percent".</summary>
public sealed class IndexModel(NpDbContext db, Lookups lookups, PlanningService planning, TimeProvider clock) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public GoalCards Cards { get; private set; } = null!;

    public ViewToggle Toggle { get; private set; } = null!;

    public string View { get; private set; } = "karten";

    public async Task OnGetAsync()
    {
        Head("Ziele & OKRs", "Was das Unternehmen erreichen will, und woran man es misst.", ("Ziele & OKRs", null));
        ViewData["ActiveHref"] = "/Goals";
        View = Request.Query["ansicht"] == "liste" ? "liste" : "karten";

        var users = await lookups.UsersAsync();
        var departments = await lookups.DepartmentsAsync();
        var thisYear = Fmt.Today(clock).Year;
        var spec = new ListSpec<Goal>
        {
            Id = "goals",
            Action = "/Goals",
            Label = "Ziele",
            Noun = "Ziele",
            SearchPlaceholder = "Titel, Bedeutung, Jahresziel ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(g => EF.Functions.ILike(g.Title, like) || EF.Functions.ILike(g.Description ?? "", like) || EF.Functions.ILike(g.YearTarget ?? "", like));
            },
            Filters =
            [
                new FilterSpec<Goal>("jahr", "Jahr", [.. Enumerable.Range(thisYear - 2, 6).Select(y => new FilterOption(y.ToString(), y.ToString()))], (q, v) => q.Where(g => g.Year == int.Parse(v))),
                new FilterSpec<Goal>("status", "Status", ListBuilder.EnumOptions<GoalStatus>(Labels.Of), (q, v) => q.Where(g => g.Status == Enum.Parse<GoalStatus>(v))),
                new FilterSpec<Goal>("abteilung", "Betrifft Bereich", [.. departments.Select(d => new FilterOption(d.Value!, d.Text))], (q, v) => q.Where(g => g.DepartmentId == long.Parse(v))),
                new FilterSpec<Goal>("owner", "Verantwortlich", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(g => g.OwnerUserId == long.Parse(v))),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Goal>, bool, IOrderedQueryable<Goal>>)>
            {
                ["year"] = ("Jahr", (q, d) => d ? q.OrderBy(g => g.Year).ThenBy(g => g.Title) : q.OrderByDescending(g => g.Year).ThenBy(g => g.Title)),
                ["title"] = ("Titel", (q, d) => d ? q.OrderByDescending(g => g.Title) : q.OrderBy(g => g.Title)),
                ["status"] = ("Status", (q, d) => d ? q.OrderByDescending(g => g.Status) : q.OrderBy(g => g.Status)),
            },
            DefaultSort = "year",
            PageSize = 18,
            Keep = new Dictionary<string, string?> { ["ansicht"] = View == "liste" ? "liste" : null },
        };

        var result = await ListBuilder.RunAsync(db.Goal.AsNoTracking().Include(g => g.OwnerUser).Include(g => g.Department), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        var facts = await planning.GoalFactsAsync([.. result.Items.Select(g => g.Id)], HttpContext.RequestAborted);
        Cards = new GoalCards([.. result.Items.Select(g => new GoalCard(g, facts.GetValueOrDefault(g.Id)))]);
        Toggle = ViewToggle.For(View, Toolbar.UrlWithPage(1));
        Table = TableModel.Of("goal-table", "Ziele", result.Items,
        [
            (new TableColumn("Ziel"), g => Ui.NameWithSub(g.Title, $"/Goals/Details?id={g.Id}", g.YearTarget)),
            (new TableColumn("Jahr", HideOnPhone: true), g => Ui.Text(g.Year.ToString())),
            (new TableColumn("Bereich", HideOnPhone: true), g => Ui.Text(g.Department?.Name)),
            (new TableColumn("Verantwortlich", HideOnPhone: true), g => Ui.Person(g.OwnerUser?.DisplayName)),
            (new TableColumn("Status"), g => Labels.Pill(g.Status)),
            (new TableColumn("Ziel erreicht"), g => Ui.Meter(facts.TryGetValue(g.Id, out var f) ? f.Attainment : null, "Erreichung der Schlüsselergebnisse")),
            (new TableColumn("Schlüsselergebnisse", Numeric: true, HideOnPhone: true), g => Ui.Text(facts.TryGetValue(g.Id, out var f) ? $"{f.KeyResultsReached}/{f.KeyResults}" : "")),
            (new TableColumn("Projekte", Numeric: true, HideOnPhone: true), g => Ui.Text(facts.TryGetValue(g.Id, out var f) ? f.Projects.ToString() : "")),
        ],
        g => [new RowAction("edit", "Bearbeiten", $"/Goals/Edit?id={g.Id}")])
        with
        {
            EmptyTitle = Toolbar.Total == 0 ? "Noch keine Ziele" : "Nichts gefunden",
            EmptyText = Toolbar.Total == 0 ? "Beginne mit dem, was das Unternehmen erreichen will. Projekte und Todos zahlen darauf ein." : "Ändere die Suche oder den Filter.",
            EmptyIcon = "target",
        };
    }
}
