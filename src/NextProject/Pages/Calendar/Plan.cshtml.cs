using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Calendar;

/// <summary>
/// The schedule of all projects: one row per project, the time from left to right, the progress inside each bar, the milestones on it, the todos below when a project is
/// opened. The window (3, 6 or 12 months) can be moved. It shows what runs at the same time and what is late.
/// </summary>
public sealed class PlanModel(NpDbContext db, ICurrentUser me, Lookups lookups, PlanningService planning) : PageModelBase
{
    private static readonly string[] FilterNames = ["wessen", "person", "abteilung", "ziel", "status"];

    public LinkTabsModel Tabs { get; private set; } = null!;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public GanttView Gantt { get; private set; } = null!;

    public List<Project> Undated { get; private set; } = [];

    public string Zoom { get; private set; } = "halbjahr";

    public int Offset { get; private set; }

    public bool OpenAll { get; private set; }

    private string _query = "";

    public string ZoomHref(string zoom) => Href(zoom, Offset, OpenAll);

    public string MoveHref(int months) => Href(Zoom, Offset + months, OpenAll);

    public string TodayHref() => Href(Zoom, 0, OpenAll);

    public string OpenHref(bool open) => Href(Zoom, Offset, open);

    public int WindowMonths => GanttView.MonthsOf(Zoom);

    private string Href(string zoom, int offset, bool open)
    {
        var parts = new List<string>();
        if (_query.Length > 0) { parts.Add(_query); }
        if (zoom != "halbjahr") { parts.Add("zoom=" + zoom); }
        if (offset != 0) { parts.Add("ab=" + offset); }
        if (open) { parts.Add("todos=1"); }
        return "/Calendar/Plan" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }

    public async Task OnGetAsync([FromQuery(Name = "zoom")] string? zoom, [FromQuery(Name = "ab")] int? offset, [FromQuery(Name = "todos")] string? todos)
    {
        Head("Zeitplan der Projekte", "Alle Projekte auf einer Zeitachse.", ("Kalender", "/Calendar"), ("Zeitplan der Projekte", null));
        ViewData["ActiveHref"] = "/Calendar";
        _query = CalendarToolbar.Query(Request, FilterNames.Append("q").ToArray());
        Tabs = CalendarToolbar.Tabs("zeitplan", _query);
        Zoom = zoom is "quartal" or "jahr" ? zoom : "halbjahr";
        Offset = Math.Clamp(offset ?? 0, -36, 60);
        OpenAll = todos is "1" or "true";
        ViewData["OpenAll"] = OpenAll;

        var meId = me.UserId ?? 0;
        var today = planning.Today;
        var q = db.Project.AsNoTracking().Include(p => p.OwnerUser).Where(p => p.Status != ProjectStatus.Cancelled);
        if (Request.Query["wessen"] == "meine")
        {
            q = q.Where(p => p.OwnerUserId == meId || p.Members.Any(m => m.UserId == meId));
        }

        if (long.TryParse(Request.Query["person"], out var person))
        {
            q = q.Where(p => p.OwnerUserId == person || p.Members.Any(m => m.UserId == person));
        }

        if (long.TryParse(Request.Query["abteilung"], out var department))
        {
            q = q.Where(p => p.DepartmentId == department);
        }

        if (long.TryParse(Request.Query["ziel"], out var goal))
        {
            q = q.Where(p => p.Goals.Any(g => g.GoalId == goal));
        }

        q = Request.Query["status"].ToString() switch
        {
            "offen" => q.Where(p => p.Status != ProjectStatus.Completed),
            "erledigt" => q.Where(p => p.Status == ProjectStatus.Completed),
            "ueberfaellig" => q.Where(p => p.EndDate < today && p.Status != ProjectStatus.Completed),
            _ => q,
        };
        if (Request.Query["q"].ToString() is { Length: > 0 } text)
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, Like.Contains(text.Trim())));
        }

        var projects = await q.OrderBy(p => p.Name).ToListAsync();
        var ids = projects.Select(p => p.Id).ToList();
        var milestones = await db.Milestone.AsNoTracking().Where(m => ids.Contains(m.ProjectId)).ToListAsync();
        var projectTodos = await db.Todo.AsNoTracking().Include(t => t.OwnerUser).Where(t => t.ProjectId != null && ids.Contains(t.ProjectId.Value) && (t.StartDate != null || t.DueDate != null)).ToListAsync();
        var facts = await planning.ProjectFactsAsync(ids);
        Gantt = GanttView.Build(today, Offset, WindowMonths, projects, milestones, projectTodos, facts);
        Undated = [.. projects.Where(p => p.StartDate is null && p.EndDate is null)];

        var fields = await CalendarToolbar.FieldsAsync(Request, lookups, FilterNames);
        fields[0] = new FilterField("wessen", "Wessen Projekte", [new("meine", "Meine")], Request.Query["wessen"] == "meine" ? "meine" : null);
        Toolbar = CalendarToolbar.Model("plan", "/Calendar/Plan", "Zeitplan der Projekte", "Projekte", Request, fields, projects.Count, "Name des Projekts ...");
    }
}
