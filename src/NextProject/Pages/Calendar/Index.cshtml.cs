using System.Text.Json;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Calendar;

/// <summary>The dates of the work in one calendar: todos that are due, milestones, the end of projects, key results that are due and the next reviews.</summary>
public sealed class IndexModel(NpDbContext db, ICurrentUser me, PlanningService planning) : PageModelBase
{
    private sealed record CalendarEvent(string Id, string Title, string Start, string? End, string Tone, string? Status, string? Url, string? Note);

    public LinkTabsModel Tabs { get; private set; } = null!;

    public string EventsJson { get; private set; } = "[]";

    public int Count { get; private set; }

    public string Today => planning.Today.ToString("yyyy-MM-dd");

    public async Task OnGetAsync([FromQuery(Name = "scope")] string? scope)
    {
        var mine = scope != "alle";
        Head("Kalender", "Termine von Todos, Meilensteinen, Projekten, Schlüsselergebnissen und Reviews.", ("Kalender", null));
        Tabs = new LinkTabsModel("Wessen Termine", [new("meine", "Meine", "/Calendar", "user"), new("alle", "Alle", "/Calendar?scope=alle", "users")], mine ? "meine" : "alle");

        var today = planning.Today;
        var from = today.AddMonths(-6);
        var to = today.AddMonths(12);
        var meId = me.UserId ?? 0;
        var events = new List<CalendarEvent>();

        var todos = db.Todo.AsNoTracking().Include(t => t.Project).Where(t => t.DueDate != null && t.DueDate >= from && t.DueDate <= to && t.Status != TodoStatus.Discarded);
        if (mine)
        {
            todos = todos.Where(t => t.OwnerUserId == meId || t.Contributors.Any(c => c.UserId == meId));
        }

        foreach (var t in await todos.OrderBy(t => t.DueDate).Take(1500).ToListAsync())
        {
            var tone = t.Status == TodoStatus.Done ? "ok" : t.Status == TodoStatus.Blocked || t.IsOverdue(today) ? "error" : t.Status == TodoStatus.InProgress ? "info" : "";
            var start = t.StartDate is { } s && s < t.DueDate ? s : t.DueDate!.Value;
            events.Add(new($"t{t.Id}", t.Title, Iso(start), start < t.DueDate ? Iso(t.DueDate!.Value) : null, tone, Labels.Of(t.Status) + (t.IsOverdue(today) ? ", überfällig" : ""), $"/Todos/Details?id={t.Id}", t.Project?.Name));
        }

        var milestones = await db.Milestone.AsNoTracking().Include(m => m.Project).Where(m => m.DueDate >= from && m.DueDate <= to).ToListAsync();
        events.AddRange(milestones.Select(m => new CalendarEvent($"m{m.Id}", "Meilenstein: " + m.Title, Iso(m.DueDate), null, m.ReachedDate is not null ? "ok" : m.DueDate < today ? "error" : "accent", m.ReachedDate is not null ? "erreicht" : "Meilenstein", $"/Projects/Milestone?projectId={m.ProjectId}&id={m.Id}", m.Project?.Name)));

        var projects = await db.Project.AsNoTracking().Where(p => p.EndDate != null && p.EndDate >= from && p.EndDate <= to && p.Status != ProjectStatus.Cancelled).ToListAsync();
        events.AddRange(projects.Select(p => new CalendarEvent($"p{p.Id}", "Projektende: " + p.Name, Iso(p.EndDate!.Value), null, p.Status == ProjectStatus.Completed ? "ok" : "accent", Labels.Of(p.Status), $"/Projects/Details?id={p.Id}", null)));

        var results = await db.KeyResult.AsNoTracking().Where(k => k.DueDate != null && k.DueDate >= from && k.DueDate <= to).ToListAsync();
        events.AddRange(results.Select(k => new CalendarEvent($"k{k.Id}", "Schlüsselergebnis: " + k.Title, Iso(k.DueDate!.Value), null, k.Attainment >= 1m ? "ok" : "info", Fmt.Percent(k.Attainment) + " erreicht", $"/Goals/KeyResult?id={k.Id}", null)));

        var reviews = await db.Review.AsNoTracking().Include(r => r.Project).Include(r => r.Goal).Where(r => r.NextReviewDate != null && r.NextReviewDate >= from && r.NextReviewDate <= to).ToListAsync();
        events.AddRange(reviews.Select(r => new CalendarEvent($"r{r.Id}", "Nächstes Review: " + (r.Project?.Name ?? r.Goal?.Title), Iso(r.NextReviewDate!.Value), null, "warn", "Review", $"/Reviews/Edit?id={r.Id}", null)));

        Count = events.Count;
        EventsJson = JsonSerializer.Serialize(events.Select(e => new
        {
            id = e.Id, title = e.Title, start = e.Start, end = e.End, tone = e.Tone, status = e.Status, url = e.Url, note = e.Note,
        }), new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");
}
