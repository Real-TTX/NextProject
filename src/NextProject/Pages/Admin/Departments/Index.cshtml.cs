using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Admin.Departments;

/// <summary>The areas of the company: what a goal concerns, where people and projects belong.</summary>
public sealed class IndexModel(NpDbContext db) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Head("Abteilungen", "Die Bereiche des Unternehmens.", ("Einstellungen", null), ("Abteilungen", null));

        var spec = new ListSpec<Department>
        {
            Id = "departments",
            Action = "/Admin/Departments",
            Label = "Abteilungen",
            Noun = "Abteilungen",
            SearchPlaceholder = "Name ...",
            Search = (q, text) => q.Where(d => EF.Functions.ILike(d.Name, Like.Contains(text)) || EF.Functions.ILike(d.Description ?? "", Like.Contains(text))),
            Sorts = new Dictionary<string, (string, Func<IQueryable<Department>, bool, IOrderedQueryable<Department>>)>
            {
                ["name"] = ("Name", (q, d) => d ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name)),
            },
            DefaultSort = "name",
        };

        var result = await ListBuilder.RunAsync(db.Department.AsNoTracking(), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        var ids = result.Items.Select(d => d.Id).ToList();
        var people = await db.User.Where(u => u.DepartmentId != null && ids.Contains(u.DepartmentId.Value)).GroupBy(u => u.DepartmentId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var projects = await db.Project.Where(p => p.DepartmentId != null && ids.Contains(p.DepartmentId.Value)).GroupBy(p => p.DepartmentId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Table = TableModel.Of("department-table", "Abteilungen", result.Items,
        [
            (new TableColumn("Abteilung"), d => Ui.NameWithSub(d.Name, $"/Admin/Departments/Edit?id={d.Id}", d.Description)),
            (new TableColumn("Mitarbeiter", Numeric: true), d => Ui.Text(people.GetValueOrDefault(d.Id).ToString())),
            (new TableColumn("Projekte", Numeric: true), d => Ui.Text(projects.GetValueOrDefault(d.Id).ToString())),
        ],
        d => [new RowAction("edit", "Bearbeiten", $"/Admin/Departments/Edit?id={d.Id}")])
        with { EmptyTitle = Toolbar.Total == 0 ? "Noch keine Abteilung" : "Nichts gefunden", EmptyText = Toolbar.Total == 0 ? "Lege die Bereiche an, zum Beispiel Vertrieb, Entwicklung und Verwaltung." : "Ändere die Suche.", EmptyIcon = "briefcase" };
    }
}
