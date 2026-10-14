using System.Text.Json;
using NextProject.Core.Planning;
using NextProject.Data;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Calendar;

/// <summary>
/// The dates of the work in one calendar: todos (from the start to the due date), milestones, the end of projects, key results that are due and the next reviews. Filtered
/// like a list, changed in place (click a todo to move it or finish it, drag it to another day, click a day to make a todo), and taken to the calendar program of the phone
/// or the office as a file or as a subscription.
/// </summary>
public sealed class IndexModel(NpDbContext db, CalendarService calendar, ICurrentUser me, Lookups lookups, PlanningService planning, PublicUrl urls) : PageModelBase
{
    private static readonly string[] FilterNames = ["wessen", "projekt", "person", "abteilung", "ziel", "art", "status"];

    public LinkTabsModel Tabs { get; private set; } = null!;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public string EventsJson { get; private set; } = "[]";

    public int Count { get; private set; }

    public bool Mine { get; private set; }

    public string Today => planning.Today.ToString("yyyy-MM-dd");

    /// <summary>The day the calendar opens on (the address can name one: the workload page links to a week) and the view.</summary>
    public string StartDate { get; private set; } = "";

    public string StartView { get; private set; } = "month";

    public string SubscribeUrl { get; private set; } = "";

    public string SubscribeWebcal => SubscribeUrl.Replace("https://", "webcal://").Replace("http://", "webcal://");

    public string FilterQuery { get; private set; } = "";

    public async Task OnGetAsync([FromQuery(Name = "datum")] string? date, [FromQuery(Name = "ansicht")] string? view)
    {
        Head("Kalender", "Alle Termine der Arbeit in einem Kalender.", ("Kalender", null));
        ViewData["ActiveHref"] = "/Calendar";
        FilterQuery = CalendarToolbar.Query(Request, FilterNames.Append("q").ToArray());
        Tabs = CalendarToolbar.Tabs("kalender", FilterQuery);

        StartDate = DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start) ? start.ToString("yyyy-MM-dd") : Today;
        StartView = view is "month" or "week" or "agenda" or "year" ? view : "month";
        var filter = CalendarToolbar.Read(Request, me.UserId ?? 0);
        Mine = filter.Mine && filter.OwnerId is null;
        var today = planning.Today;
        var items = await calendar.GetAsync(filter, today.AddMonths(-6), today.AddMonths(12), HttpContext.RequestAborted);
        Count = items.Count;
        Toolbar = CalendarToolbar.Model("calendar", "/Calendar", "Kalender", "Termine", Request, await CalendarToolbar.FieldsAsync(Request, lookups, FilterNames), Count, "Titel ...");

        EventsJson = JsonSerializer.Serialize(items.Select(i => new
        {
            id = i.Id, title = i.Title, start = i.Start.ToString("yyyy-MM-dd"), end = i.End?.ToString("yyyy-MM-dd"), tone = i.Tone, status = i.Status, url = i.Url, note = i.Note,
            kind = i.Kind, entityId = i.EntityId, movable = i.Movable,
        }), new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        var token = await db.User.AsNoTracking().Where(u => u.Id == me.UserId).Select(u => u.CalendarToken).FirstAsync();
        SubscribeUrl = urls.Absolute($"/cal/{token}.ics");
    }

    /// <summary>The dates the calendar shows now as a file (the filter of the address applies), for a calendar program that cannot subscribe.</summary>
    public async Task<IActionResult> OnGetIcsAsync()
    {
        var today = planning.Today;
        var items = await calendar.GetAsync(CalendarToolbar.Read(Request, me.UserId ?? 0), today.AddMonths(-2), today.AddMonths(12), HttpContext.RequestAborted);
        var events = items.Select(i => new IcsEvent(i.Id + "@nextproject", i.Title, i.Start, i.End, i.Description, urls.Absolute(i.Url), i.Note, i.Done));
        var text = IcsWriter.Write("NextProject: " + me.Name, events, DateTime.UtcNow);
        return File(System.Text.Encoding.UTF8.GetBytes(text), "text/calendar; charset=utf-8", $"nextproject-kalender-{today:yyyy-MM-dd}.ics");
    }

    /// <summary>A new secret for the subscription address: the old address stops working, for a person whose address got out.</summary>
    public async Task<IActionResult> OnPostRotateAsync()
    {
        var user = await db.User.FirstAsync(u => u.Id == me.UserId);
        user.CalendarToken = Guid.NewGuid();
        await db.SaveChangesAsync();
        Flash("Die Abo-Adresse ist neu. Die alte Adresse zeigt nichts mehr: trage die neue in deinem Kalenderprogramm ein.");
        return RedirectToPage();
    }
}
