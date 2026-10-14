using System.Text;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Reports;

/// <summary>
/// The view for the management: how far the goals are reached, which projects are in danger and why, where the work piles up. Reaching a goal and finishing todos are shown
/// in separate columns on purpose. The tables can be taken to a spreadsheet (CSV).
/// </summary>
public sealed class IndexModel(NpDbContext db, PlanningService planning) : PageModelBase
{
    public sealed record EndangeredProject(Project Project, ProjectFacts Facts);

    public int ActiveGoals { get; private set; }

    public decimal? AverageAttainment { get; private set; }

    public int ProjectsRunning { get; private set; }

    public int ProjectsAtRisk { get; private set; }

    public TodoCounts Todos { get; private set; } = TodoCounts.None;

    public TableModel StrategyTable { get; private set; } = null!;

    public IHtmlContent ProjectChart { get; private set; } = HtmlString.Empty;

    public IHtmlContent OwnerChart { get; private set; } = HtmlString.Empty;

    public List<EndangeredProject> Endangered { get; private set; } = [];

    public List<Todo> BlockedTodos { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Head("Reports", "Wie weit sind die Ziele, welche Projekte sind gefährdet, wo staut sich die Arbeit?", ("Reports", null));
        var today = planning.Today;

        var goals = await db.Goal.AsNoTracking().Include(g => g.Department).Include(g => g.OwnerUser).Include(g => g.Projects).ThenInclude(p => p.Project)
            .Where(g => g.Status != GoalStatus.Dropped).OrderByDescending(g => g.Year).ThenBy(g => g.Title).ToListAsync();
        var goalFacts = await planning.GoalFactsAsync([.. goals.Select(g => g.Id)]);
        ActiveGoals = goals.Count(g => g.Status == GoalStatus.Active);
        AverageAttainment = Core.Planning.Progress.Average(goals.Where(g => g.Status == GoalStatus.Active && goalFacts[g.Id].Attainment is not null).Select(g => goalFacts[g.Id].Attainment!.Value));

        var projects = await db.Project.AsNoTracking().Include(p => p.OwnerUser).Where(p => p.Status != ProjectStatus.Cancelled).ToListAsync();
        var projectFacts = await planning.ProjectFactsAsync([.. projects.Select(p => p.Id)]);
        ProjectsRunning = projects.Count(p => p.Status is ProjectStatus.InProgress or ProjectStatus.AtRisk);
        ProjectsAtRisk = projects.Count(p => p.Status == ProjectStatus.AtRisk);
        Endangered = [.. projects.Where(p => !p.IsClosed && p.Status != ProjectStatus.Paused && projectFacts.TryGetValue(p.Id, out var f) && f.Signals.Count > 0)
            .Select(p => new EndangeredProject(p, projectFacts[p.Id])).OrderByDescending(e => e.Facts.IsEndangered).ThenByDescending(e => e.Facts.Signals.Count)];

        var statuses = await db.Todo.AsNoTracking().Select(t => new { t.Status, t.DueDate }).ToListAsync();
        Todos = PlanningService.Count(statuses.Select(s => (s.Status, s.DueDate)), today);
        BlockedTodos = await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.Project).Where(t => t.Status == TodoStatus.Blocked).OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).Take(20).ToListAsync();

        StrategyTable = TableModel.Of("strategy-table", "Strategische Umsetzung", goals,
        [
            (new TableColumn("Ziel"), g => Ui.NameWithSub(g.Title, $"/Goals/Details?id={g.Id}", g.YearTarget)),
            (new TableColumn("Jahr", HideOnPhone: true), g => Ui.Text(g.Year.ToString())),
            (new TableColumn("Bereich", HideOnPhone: true), g => Ui.Text(g.Department?.Name ?? "Unternehmen")),
            (new TableColumn("Projekte"), g => Ui.Text(string.Join(", ", g.Projects.Where(p => p.Project is not null).Select(p => p.Project!.Name)))),
            (new TableColumn("Ziel erreicht"), g => Ui.Meter(goalFacts[g.Id].Attainment, "Erreichung der Schlüsselergebnisse")),
            (new TableColumn("Todos erledigt", HideOnPhone: true), g => Ui.Meter(goalFacts[g.Id].Todos.Total - goalFacts[g.Id].Todos.Discarded > 0 ? goalFacts[g.Id].Todos.Completion : null, "Erledigte Todos")),
            (new TableColumn("Status"), g => Labels.Pill(g.Status)),
        ])
        with { EmptyTitle = "Noch keine Ziele", EmptyText = "Sobald es Ziele gibt, erscheint hier, wie weit sie sind.", EmptyIcon = "target" };

        ProjectChart = BarChart.Render("project-status-chart", "Projekte nach Status",
            [.. Enum.GetValues<ProjectStatus>().Select(s => new Bar(Labels.Of(s), projects.Count(p => p.Status == s), $"{Labels.Of(s)}: {projects.Count(p => p.Status == s)}"))], "Projekte");

        var open = await db.Todo.AsNoTracking().Where(t => t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded).GroupBy(t => t.OwnerUser!.DisplayName).Select(g => new { Name = g.Key, N = g.Count() }).OrderByDescending(x => x.N).Take(12).ToListAsync();
        OwnerChart = BarChart.Render("owner-chart", "Offene Todos je Verantwortlichem", [.. open.Select(x => new Bar(Fmt.Initials(x.Name), x.N, $"{x.Name}: {x.N} offen"))], "Todos");
    }

    // ------------------------------------------------------------------------------------------------ export

    public async Task<IActionResult> OnGetCsvAsync(string kind)
    {
        var rows = new List<string[]>();
        switch (kind)
        {
            case "todos":
                rows.Add(["Titel", "Verantwortlich", "Status", "Priorität", "Start", "Fällig", "Projekt", "Ziel", "Schlüsselergebnis", "Erledigt am", "Ergebnis"]);
                foreach (var t in await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.Project).Include(t => t.Goal).Include(t => t.KeyResult).ThenInclude(k => k!.Goal).OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ToListAsync())
                {
                    rows.Add([t.Title, t.OwnerUser?.DisplayName ?? "", Labels.Of(t.Status), Labels.Of(t.Priority), Fmt.Date(t.StartDate), Fmt.Date(t.DueDate), t.Project?.Name ?? "", t.Goal?.Title ?? t.KeyResult?.Goal?.Title ?? "", t.KeyResult?.Title ?? "", Fmt.Moment(t.CompletedDate), t.Result ?? ""]);
                }

                break;

            case "projects":
                rows.Add(["Projekt", "Projektowner", "Status", "Priorität", "Abteilung", "Start", "Ende", "Todos gesamt", "Todos erledigt", "Anteil erledigt", "Ziel erreicht", "Kostenaufwand", "Ressourcen"]);
                var projects = await db.Project.AsNoTracking().Include(p => p.OwnerUser).Include(p => p.Department).OrderBy(p => p.Name).ToListAsync();
                var facts = await planning.ProjectFactsAsync([.. projects.Select(p => p.Id)]);
                foreach (var p in projects)
                {
                    var f = facts[p.Id];
                    rows.Add([p.Name, p.OwnerUser?.DisplayName ?? "", Labels.Of(p.Status), Labels.Of(p.Priority), p.Department?.Name ?? "", Fmt.Date(p.StartDate), Fmt.Date(p.EndDate), f.Todos.Total.ToString(), f.Todos.Done.ToString(), Fmt.Percent(f.Todos.Completion), f.Attainment is { } a ? Fmt.Percent(a) : "", p.Budget is { } b ? Fmt.Money(b) : "", p.Resources ?? ""]);
                }

                break;

            case "goals":
                rows.Add(["Ziel", "Jahr", "Bereich", "Verantwortlich", "Status", "Jahresziel", "Q1", "Q2", "Q3", "Q4", "Schlüsselergebnis", "Startwert", "Aktuell", "Zielwert", "Einheit", "Erreicht"]);
                foreach (var g in await db.Goal.AsNoTracking().Include(g => g.Department).Include(g => g.OwnerUser).Include(g => g.KeyResults).ThenInclude(k => k.CheckIns).OrderByDescending(g => g.Year).ThenBy(g => g.Title).ToListAsync())
                {
                    if (g.KeyResults.Count == 0)
                    {
                        rows.Add([g.Title, g.Year.ToString(), g.Department?.Name ?? "", g.OwnerUser?.DisplayName ?? "", Labels.Of(g.Status), g.YearTarget ?? "", g.Q1Target ?? "", g.Q2Target ?? "", g.Q3Target ?? "", g.Q4Target ?? "", "", "", "", "", "", ""]);
                    }

                    foreach (var k in g.KeyResults.OrderBy(k => k.Title))
                    {
                        rows.Add([g.Title, g.Year.ToString(), g.Department?.Name ?? "", g.OwnerUser?.DisplayName ?? "", Labels.Of(g.Status), g.YearTarget ?? "", g.Q1Target ?? "", g.Q2Target ?? "", g.Q3Target ?? "", g.Q4Target ?? "", k.Title, Fmt.Number(k.StartValue, 2), Fmt.Number(k.CurrentValue, 2), Fmt.Number(k.TargetValue, 2), k.Unit ?? "", Fmt.Percent(k.Attainment)]);
                    }
                }

                break;

            default:
                return NotFound();
        }

        // A spreadsheet in Germany wants a semicolon and a byte order mark; cells that start with = + - @ are made harmless (formula injection).
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendJoin(';', row.Select(Cell)).Append("\r\n");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"nextproject-{kind}-{planning.Today:yyyy-MM-dd}.csv");
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
