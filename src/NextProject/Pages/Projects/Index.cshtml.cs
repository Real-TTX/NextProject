using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Html;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Projects;

/// <summary>All projects with what matters at a glance: who owns them, the status, the share of finished todos and how far the key results are.</summary>
public sealed class IndexModel(NpDbContext db, Lookups lookups, PlanningService planning) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Head("Projekte", "Initiativen, die auf die Ziele des Unternehmens einzahlen.", ("Projekte", null));

        var users = await lookups.UsersAsync();
        var departments = await lookups.DepartmentsAsync();
        var goals = await lookups.GoalsAsync();
        var spec = new ListSpec<Project>
        {
            Id = "projects",
            Action = "/Projects",
            Label = "Projekte",
            Noun = "Projekte",
            SearchPlaceholder = "Name, Beschreibung, Zielsetzung ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(p => EF.Functions.ILike(p.Name, like) || EF.Functions.ILike(p.Description ?? "", like) || EF.Functions.ILike(p.Objective ?? "", like));
            },
            Filters =
            [
                new FilterSpec<Project>("status", "Status", [new("aktiv", "Offen (nicht abgeschlossen)"), .. ListBuilder.EnumOptions<ProjectStatus>(Labels.Of)],
                    (q, v) => v == "aktiv" ? q.Where(p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled) : q.Where(p => p.Status == Enum.Parse<ProjectStatus>(v))),
                new FilterSpec<Project>("prio", "Priorität", ListBuilder.EnumOptions<Level>(Labels.Of), (q, v) => q.Where(p => p.Priority == Enum.Parse<Level>(v))),
                new FilterSpec<Project>("owner", "Projektowner", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(p => p.OwnerUserId == long.Parse(v))),
                new FilterSpec<Project>("abteilung", "Abteilung", [.. departments.Select(d => new FilterOption(d.Value!, d.Text))], (q, v) => q.Where(p => p.DepartmentId == long.Parse(v))),
                new FilterSpec<Project>("ziel", "Ziel", [.. goals.Select(g => new FilterOption(g.Value!, g.Text))], (q, v) => q.Where(p => p.Goals.Any(g => g.GoalId == long.Parse(v)))),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Project>, bool, IOrderedQueryable<Project>>)>
            {
                ["name"] = ("Name", (q, d) => d ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name)),
                ["end"] = ("Enddatum", (q, d) => d ? q.OrderByDescending(p => p.EndDate) : q.OrderBy(p => p.EndDate == null).ThenBy(p => p.EndDate)),
                ["prio"] = ("Priorität", (q, d) => d ? q.OrderBy(p => p.Priority == Level.High ? 0 : p.Priority == Level.Medium ? 1 : 2) : q.OrderByDescending(p => p.Priority == Level.High ? 0 : p.Priority == Level.Medium ? 1 : 2)),
                ["status"] = ("Status", (q, d) => d ? q.OrderByDescending(p => p.Status) : q.OrderBy(p => p.Status)),
                ["created"] = ("Angelegt", (q, d) => d ? q.OrderBy(p => p.CreateDate) : q.OrderByDescending(p => p.CreateDate)),
            },
            DefaultSort = "name",
        };

        var result = await ListBuilder.RunAsync(db.Project.AsNoTracking().Include(p => p.OwnerUser).Include(p => p.Goals).ThenInclude(g => g.Goal), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        var facts = await planning.ProjectFactsAsync([.. result.Items.Select(p => p.Id)], HttpContext.RequestAborted);
        Table = TableModel.Of("project-table", "Projekte", result.Items,
        [
            (new TableColumn("Projekt"), p => Ui.NameWithSub(p.Name, $"/Projects/Details?id={p.Id}", GoalLine(p))),
            (new TableColumn("Projektowner", HideOnPhone: true), p => Ui.Person(p.OwnerUser?.DisplayName)),
            (new TableColumn("Status"), p => StatusCell(p, facts)),
            (new TableColumn("Zeitraum", HideOnPhone: true), p => Ui.Text(Fmt.Period(p.StartDate, p.EndDate))),
            (new TableColumn("Todos erledigt"), p => Ui.Meter(facts.TryGetValue(p.Id, out var f) && f.Todos.Total - f.Todos.Discarded > 0 ? f.Todos.Completion : null, "Anteil der erledigten Todos")),
            (new TableColumn("Ziel erreicht", HideOnPhone: true), p => Ui.Meter(facts.TryGetValue(p.Id, out var f) ? f.Attainment : null, "Erreichung der Schlüsselergebnisse")),
        ],
        p => [new RowAction("edit", "Bearbeiten", $"/Projects/Edit?id={p.Id}")])
        with
        {
            EmptyTitle = Toolbar.Total == 0 ? "Noch keine Projekte" : "Nichts gefunden",
            EmptyText = Toolbar.Total == 0 ? "Lege das erste Projekt an und hänge Todos, Meilensteine und Schlüsselergebnisse daran." : "Ändere die Suche oder den Filter.",
            EmptyIcon = "folder",
        };
    }

    private static string? GoalLine(Project p)
        => p.Goals.Count == 0 ? null : "Ziel: " + string.Join(", ", p.Goals.Select(g => g.Goal?.Title).Where(t => t is not null));

    private static IHtmlContent StatusCell(Project p, Dictionary<long, ProjectFacts> facts)
    {
        var pill = Labels.Pill(p.Status);
        if (facts.TryGetValue(p.Id, out var f) && f.IsEndangered && p.Status == ProjectStatus.InProgress)
        {
            return Ui.Concat(pill, new HtmlString("<small class=\"np-sub np-overdue\">Achtung: " + System.Net.WebUtility.HtmlEncode(f.Signals.First(s => s.Tone == "error").Text) + "</small>"));
        }

        return pill;
    }
}
