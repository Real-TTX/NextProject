using System.Net;
using System.Text.RegularExpressions;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NextProject.Tests.Integration;

/// <summary>The calendar: what it shows for a filter, moving a todo to another day, and the subscription (iCal) with its secret address.</summary>
[Collection(IntegrationCollection.Name)]
public sealed partial class CalendarTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _app;

    public CalendarTests(AppFixture app) => _app = app;

    private async Task<(HttpClient Client, long AdminId)> SignedInWithDemoAsync()
    {
        await _app.EnsureAdminAsync();
        var adminId = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Where(u => u.Email == "admin@example.test").Select(u => u.Id).FirstAsync());
        await _app.WithScopeAsync(sp => sp.GetRequiredService<DemoData>().CreateAsync(adminId));
        var client = _app.NewClient();
        await AppFixture.LoginAsync(client, "admin@example.test", "Admin-Passwort-2026");
        return (client, adminId);
    }

    private Task<List<CalendarItem>> ItemsAsync(CalendarFilter filter)
        => _app.WithScopeAsync(sp =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return sp.GetRequiredService<CalendarService>().GetAsync(filter, today.AddMonths(-6), today.AddMonths(12));
        });

    [DatabaseFact]
    public async Task The_calendar_filters_by_person_project_kind_and_state()
    {
        var (_, adminId) = await SignedInWithDemoAsync();
        var all = await ItemsAsync(new CalendarFilter(adminId, Mine: false));
        Assert.Contains(all, i => i.Kind == "todo");
        Assert.Contains(all, i => i.Kind == "milestone");
        Assert.Contains(all, i => i.Kind == "project");
        Assert.Contains(all, i => i.Kind == "keyresult");
        Assert.Contains(all, i => i.Kind == "review");

        var mine = await ItemsAsync(new CalendarFilter(adminId));
        Assert.True(mine.Count < all.Count);
        Assert.All(mine.Where(i => i.Kind == "todo"), i => Assert.Contains("Verantwortlich", i.Description!));

        var onlyTodos = await ItemsAsync(new CalendarFilter(adminId, Mine: false, Kind: "todo"));
        Assert.NotEmpty(onlyTodos);
        Assert.All(onlyTodos, i => Assert.Equal("todo", i.Kind));

        var projectId = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Project.Where(p => p.Name.StartsWith("Bestellverfolgung")).Select(p => p.Id).FirstAsync());
        var ofProject = await ItemsAsync(new CalendarFilter(adminId, Mine: false, ProjectId: projectId));
        Assert.All(ofProject.Where(i => i.Kind == "todo"), i => Assert.Equal("Bestellverfolgung verbessern", i.Note));
        Assert.True(ofProject.Count < all.Count);

        var overdue = await ItemsAsync(new CalendarFilter(adminId, Mine: false, State: "ueberfaellig"));
        Assert.NotEmpty(overdue);
        Assert.All(overdue, i => Assert.Equal("error", i.Tone));

        var done = await ItemsAsync(new CalendarFilter(adminId, Mine: false, Kind: "todo", State: "erledigt"));
        Assert.All(done, i => Assert.True(i.Done));

        var search = await ItemsAsync(new CalendarFilter(adminId, Mine: false, Text: "Schnittstelle"));
        Assert.NotEmpty(search);
        Assert.All(search, i => Assert.Contains("schnittstelle", i.Title, StringComparison.OrdinalIgnoreCase));
    }

    [DatabaseFact]
    public async Task A_todo_with_a_start_and_a_due_date_is_one_bar_and_a_finished_one_cannot_be_moved()
    {
        var (_, adminId) = await SignedInWithDemoAsync();
        var all = await ItemsAsync(new CalendarFilter(adminId, Mine: false, Kind: "todo"));
        Assert.Contains(all, i => i.End is not null && i.End > i.Start);
        Assert.All(all.Where(i => i.Done), i => Assert.False(i.Movable));
        Assert.All(all.Where(i => !i.Done), i => Assert.True(i.Movable));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync("/Calendar");
        var token = System.Net.WebUtility.HtmlDecode(Token().Match(html).Groups[1].Value);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    [DatabaseFact]
    public async Task Moving_a_todo_shifts_its_start_too_writes_the_history_and_refuses_finished_ones()
    {
        var (client, _) = await SignedInWithDemoAsync();
        var (id, start, due) = await _app.WithScopeAsync(async sp =>
        {
            var t = await sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().Where(x => x.StartDate != null && x.DueDate != null && x.Status == TodoStatus.InProgress).FirstAsync();
            return (t.Id, t.StartDate!.Value, t.DueDate!.Value);
        });

        var moved = await PostAsync(client, $"/api/todos/{id}/due", new() { ["due"] = due.AddDays(5).ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var after = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().FirstAsync(t => t.Id == id));
        Assert.Equal(due.AddDays(5), after.DueDate);
        Assert.Equal(start.AddDays(5), after.StartDate);
        Assert.Contains(await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().HistoryEntry.AsNoTracking().Where(h => h.EntityId == id && h.EntityType == HistoryEntity.Todo && h.Kind == HistoryKind.DueDate).ToListAsync()),
            h => h.NewValue == Fmt.Date(due.AddDays(5)));

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, $"/api/todos/{id}/due", new() { ["due"] = "kein Datum" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, $"/api/todos/{id}/due", new() { ["due"] = "1850-01-01" })).StatusCode);

        var finished = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().Where(t => t.Status == TodoStatus.Done).Select(t => t.Id).FirstAsync());
        var refused = await PostAsync(client, $"/api/todos/{finished}/due", new() { ["due"] = "2026-12-01" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("nicht verschoben", await refused.Content.ReadAsStringAsync());
    }

    [DatabaseFact]
    public async Task The_subscription_is_a_calendar_file_behind_a_secret_and_a_new_secret_ends_the_old_address()
    {
        var (client, adminId) = await SignedInWithDemoAsync();
        var token = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Where(u => u.Id == adminId).Select(u => u.CalendarToken).FirstAsync());

        // no sign-in: a calendar program cannot; the address is the key
        using var program = _app.NewClient();
        var response = await program.GetAsync($"/cal/{token}.ics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/calendar", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var text = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", text);
        Assert.Contains("BEGIN:VEVENT", text);
        Assert.Contains("DTSTART;VALUE=DATE:", text);
        Assert.Contains("URL:http", text);
        Assert.All(text.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, line));

        Assert.Equal(HttpStatusCode.NotFound, (await program.GetAsync($"/cal/{Guid.NewGuid()}.ics")).StatusCode);

        // somebody who is switched off has no calendar
        var other = await _app.CreateUserAsync("ics-off@example.test", "Aus-Passwort-2026", UserRole.User);
        var otherToken = await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            var u = await db.User.FirstAsync(x => x.Id == other.Id);
            u.IsActive = false;
            await db.SaveChangesAsync();
            return u.CalendarToken;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await program.GetAsync($"/cal/{otherToken}.ics")).StatusCode);

        // a new address: the old one shows nothing any more
        var page = await AppFixture.PostFormAsync(client, "/Calendar", new Dictionary<string, string>(), "/Calendar?handler=Rotate");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await program.GetAsync($"/cal/{token}.ics")).StatusCode);
        var fresh = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Where(u => u.Id == adminId).Select(u => u.CalendarToken).FirstAsync());
        Assert.NotEqual(token, fresh);
        Assert.Equal(HttpStatusCode.OK, (await program.GetAsync($"/cal/{fresh}.ics")).StatusCode);
    }

    [DatabaseFact]
    public async Task The_file_of_the_shown_dates_follows_the_filter_and_needs_a_sign_in()
    {
        var (client, _) = await SignedInWithDemoAsync();
        var response = await client.GetAsync("/Calendar?handler=Ics&wessen=alle&art=milestone");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(".ics", response.Content.Headers.ContentDisposition!.FileName);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("SUMMARY:Meilenstein:", text);
        Assert.DoesNotContain("UID:t", text);

        using var anonymous = _app.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Calendar?handler=Ics")).StatusCode);
    }

    [DatabaseFact]
    public async Task The_schedule_and_the_workload_open_with_filters_and_the_calendar_opens_on_a_given_week()
    {
        var (client, _) = await SignedInWithDemoAsync();
        foreach (var url in new[]
        {
            "/Calendar/Plan", "/Calendar/Plan?status=offen&wessen=meine", "/Calendar/Plan?abteilung=1", "/Calendar/Workload", "/Calendar/Workload?ab=8", "/Calendar/Workload?abteilung=2&projekt=1",
            "/Calendar?person=1&art=todo&status=offen&ansicht=week&datum=2026-10-19", "/Calendar?wessen=alle&q=Schnittstelle", "/Calendar?datum=kaputt&ansicht=unsinn",
        })
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} answered {(int)response.StatusCode}");
        }

        var week = await AppFixture.TextAsync(client, "/Calendar?ansicht=week&datum=2026-10-19");
        Assert.Contains("data-view=\"week\"", week);
        Assert.Contains("data-date=\"2026-10-19\"", week);
        var junk = await AppFixture.TextAsync(client, "/Calendar?datum=kaputt&ansicht=unsinn");
        Assert.Contains("data-view=\"month\"", junk);

        var workload = await AppFixture.TextAsync(client, "/Calendar/Workload");
        Assert.Contains("Überfällig", workload);
        Assert.Contains("KW ", workload);
        var plan = await AppFixture.TextAsync(client, "/Calendar/Plan");
        Assert.Contains("Bestellverfolgung verbessern", plan);
        Assert.Contains("np-gantt__bar", plan);
    }

    [GeneratedRegex("name=\"np-csrf\" content=\"([^\"]+)\"")]
    private static partial Regex Token();
}
