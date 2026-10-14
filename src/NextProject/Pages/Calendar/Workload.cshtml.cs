using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Calendar;

/// <summary>Who has how much to do in the next weeks: open todos per person and week. Not a ranking: it shows where one person is full and another has room, before it becomes a delay.</summary>
public sealed class WorkloadModel(NpDbContext db, Lookups lookups, PlanningService planning) : PageModelBase
{
    private static readonly string[] FilterNames = ["abteilung", "projekt"];

    public const int Weeks = 8;

    public LinkTabsModel Tabs { get; private set; } = null!;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public List<WorkloadRow> Rows { get; private set; } = [];

    public List<DateOnly> WeekStarts { get; private set; } = [];

    public int Offset { get; private set; }

    public string FilterQuery { get; private set; } = "";

    public DateOnly Today => planning.Today;

    public async Task OnGetAsync([FromQuery(Name = "ab")] int? offset)
    {
        Head("Auslastung", "Wer hat in den nächsten Wochen wie viel zu tun?", ("Kalender", "/Calendar"), ("Auslastung", null));
        ViewData["ActiveHref"] = "/Calendar";
        FilterQuery = CalendarToolbar.Query(Request, FilterNames.Append("q").ToArray());
        Tabs = CalendarToolbar.Tabs("auslastung", FilterQuery);

        Offset = Math.Clamp(offset ?? 0, 0, 52);
        var today = planning.Today;
        var first = Workload.MondayOf(today).AddDays(7 * Offset);
        WeekStarts = [.. Enumerable.Range(0, Weeks).Select(i => first.AddDays(7 * i))];

        var people = db.User.AsNoTracking().Include(u => u.Department).Where(u => u.IsActive);
        if (long.TryParse(Request.Query["abteilung"], out var department))
        {
            people = people.Where(u => u.DepartmentId == department);
        }

        if (Request.Query["q"].ToString() is { Length: > 0 } text)
        {
            people = people.Where(u => EF.Functions.ILike(u.DisplayName, Like.Contains(text.Trim())));
        }

        var users = await people.OrderBy(u => u.DisplayName).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var todos = db.Todo.AsNoTracking().Where(t => ids.Contains(t.OwnerUserId) && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        if (long.TryParse(Request.Query["projekt"], out var project))
        {
            todos = todos.Where(t => t.ProjectId == project);
        }

        var open = await todos.Select(t => new { t.OwnerUserId, t.DueDate }).ToListAsync();
        Rows = Workload.Build(open.Select(t => (t.OwnerUserId, t.DueDate)), users.Select(u => (u.Id, u.DisplayName, u.JobTitle, u.Department?.Name)), first, Weeks, today);
        Toolbar = CalendarToolbar.Model("workload", "/Calendar/Workload", "Auslastung", "Mitarbeiter", Request, await CalendarToolbar.FieldsAsync(Request, lookups, FilterNames), Rows.Count, "Name ...");
    }

    public static string Week(DateOnly monday) => $"{System.Globalization.ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue))}";

    public string CalendarLink(long userId, DateOnly monday)
        => $"/Calendar?person={userId}&art=todo&status=offen&ansicht=week&datum={monday:yyyy-MM-dd}";
}
