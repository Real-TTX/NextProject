using System.Net;
using System.Text.RegularExpressions;
using NextProject.Data;
using NextProject.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NextProject.Tests.Integration;

/// <summary>The life of a todo through the real pages: create, block with a reason, finish, reopen, depend on another, and what is written to the history.</summary>
[Collection(IntegrationCollection.Name)]
public sealed partial class WorkflowTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _app;

    public WorkflowTests(AppFixture app) => _app = app;

    private async Task<(HttpClient Client, long AdminId)> SignedInAsync()
    {
        await _app.EnsureAdminAsync();
        var client = _app.NewClient();
        await AppFixture.LoginAsync(client, "admin@example.test", "Admin-Passwort-2026");
        var id = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Where(u => u.Email == "admin@example.test").Select(u => u.Id).FirstAsync());
        return (client, id);
    }

    private static async Task<HttpResponseMessage> PostStatusAsync(HttpClient client, long id, string to, string? reason = null)
    {
        var html = await client.GetStringAsync("/Todos");
        var token = Token().Match(html).Groups[1].Value;
        var fields = new Dictionary<string, string> { ["to"] = to };
        if (reason is not null)
        {
            fields["reason"] = reason;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/todos/{id}/status") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("RequestVerificationToken", System.Net.WebUtility.HtmlDecode(token));
        return await client.SendAsync(request);
    }

    private async Task<long> CreateTodoAsync(HttpClient client, long ownerId, string title, DateOnly? due = null)
    {
        var response = await AppFixture.PostFormAsync(client, "/Todos/Edit", new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.OwnerUserId"] = ownerId.ToString(),
            ["Input.Status"] = "Open",
            ["Input.Priority"] = "Medium",
            ["Input.DueDate"] = due?.ToString("yyyy-MM-dd") ?? "",
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Todos/Details", AppFixture.LocationPath(response));
        return await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.Where(t => t.Title == title).Select(t => t.Id).FirstAsync());
    }

    [DatabaseFact]
    public async Task A_todo_is_created_with_one_owner_and_its_creation_is_in_the_history()
    {
        var (client, adminId) = await SignedInAsync();
        var id = await CreateTodoAsync(client, adminId, "Angebot für Kunde X versenden", DateOnly.FromDateTime(DateTime.Today).AddDays(3));

        await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            var todo = await db.Todo.AsNoTracking().FirstAsync(t => t.Id == id);
            Assert.Equal(adminId, todo.OwnerUserId);
            Assert.Equal(TodoStatus.Open, todo.Status);
            Assert.Equal(adminId, todo.CreateUserId);
            Assert.Equal(UpdateState.Created, todo.UpdateState);
            Assert.Contains(await db.HistoryEntry.AsNoTracking().Where(h => h.EntityType == HistoryEntity.Todo && h.EntityId == id).ToListAsync(), h => h.Kind == HistoryKind.Created);
        });

        var page = await AppFixture.TextAsync(client, $"/Todos/Details?id={id}");
        Assert.Contains("Angebot für Kunde X versenden", page);
        Assert.Contains("Starten", page);
    }

    [DatabaseFact]
    public async Task A_todo_needs_a_title_and_an_owner()
    {
        var (client, _) = await SignedInAsync();
        var response = await AppFixture.PostFormAsync(client, "/Todos/Edit", new Dictionary<string, string> { ["Input.Title"] = "", ["Input.OwnerUserId"] = "0", ["Input.Status"] = "Open", ["Input.Priority"] = "Low" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await AppFixture.TextAsync(response);
        Assert.Contains("Bitte gib der Aufgabe einen Titel.", text);
        Assert.Contains("Bitte lege fest, wer verantwortlich ist.", text);
    }

    [DatabaseFact]
    public async Task A_block_needs_a_reason_finishing_sets_the_time_and_reopening_clears_it()
    {
        var (client, adminId) = await SignedInAsync();
        var id = await CreateTodoAsync(client, adminId, "Schnittstelle bauen");

        var noReason = await PostStatusAsync(client, id, "Blocked");
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Contains("blockiert", await noReason.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, id, "Blocked", "Zugang fehlt")).StatusCode);
        var blocked = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().FirstAsync(t => t.Id == id));
        Assert.Equal(TodoStatus.Blocked, blocked.Status);
        Assert.Equal("Zugang fehlt", blocked.BlockReason);
        Assert.Contains("Zugang fehlt", await AppFixture.TextAsync(client, $"/Todos/Details?id={id}"));

        // the block is lifted: the reason is gone from the todo (it stays in the history)
        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, id, "InProgress")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, id, "Done")).StatusCode);
        var done = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().FirstAsync(t => t.Id == id));
        Assert.Equal(TodoStatus.Done, done.Status);
        Assert.NotNull(done.CompletedDate);
        Assert.Null(done.BlockReason);

        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, id, "Open")).StatusCode);
        Assert.Null((await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.AsNoTracking().FirstAsync(t => t.Id == id))).CompletedDate);

        var history = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().HistoryEntry.AsNoTracking().Where(h => h.EntityId == id && h.EntityType == HistoryEntity.Todo && h.Kind == HistoryKind.Status).OrderBy(h => h.Id).ToListAsync());
        Assert.Equal(["Offen → Blockiert", "Blockiert → In Arbeit", "In Arbeit → Erledigt", "Erledigt → Offen"], history.Select(h => $"{h.OldValue} → {h.NewValue}"));
        Assert.Equal("Zugang fehlt", history[0].Note);
    }

    [DatabaseFact]
    public async Task A_todo_cannot_be_finished_while_it_waits_for_another_and_a_circle_is_refused()
    {
        var (client, adminId) = await SignedInAsync();
        var first = await CreateTodoAsync(client, adminId, "Erst das");
        var second = await CreateTodoAsync(client, adminId, "Dann das");
        await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            db.TodoDependency.Add(new TodoDependency { TodoId = second, DependsOnTodoId = first });
            await db.SaveChangesAsync();
        });

        var refused = await PostStatusAsync(client, second, "Done");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Erst das", await refused.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, first, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostStatusAsync(client, second, "Done")).StatusCode);

        // first waiting for second would close a circle
        Assert.True(await _app.WithScopeAsync(sp => sp.GetRequiredService<Services.TodoService>().WouldCycleAsync(first, second)));
        Assert.False(await _app.WithScopeAsync(sp => sp.GetRequiredService<Services.TodoService>().WouldCycleAsync(second, first + 1000)));
    }

    [DatabaseFact]
    public async Task A_change_of_owner_and_due_date_is_written_to_the_history()
    {
        var (client, adminId) = await SignedInAsync();
        var other = await _app.CreateUserAsync("owner2@example.test", "Zweiter-Besitzer-2026", UserRole.User);
        var id = await CreateTodoAsync(client, adminId, "Übergeben", DateOnly.FromDateTime(DateTime.Today).AddDays(2));

        var response = await AppFixture.PostFormAsync(client, $"/Todos/Edit?id={id}", new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["Input.Title"] = "Übergeben",
            ["Input.OwnerUserId"] = other.Id.ToString(),
            ["Input.Status"] = "Open",
            ["Input.Priority"] = "High",
            ["Input.DueDate"] = DateOnly.FromDateTime(DateTime.Today).AddDays(9).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var kinds = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().HistoryEntry.AsNoTracking().Where(h => h.EntityId == id && h.EntityType == HistoryEntity.Todo).Select(h => h.Kind).ToListAsync());
        Assert.Contains(HistoryKind.Owner, kinds);
        Assert.Contains(HistoryKind.DueDate, kinds);
        Assert.Contains(HistoryKind.Priority, kinds);
        var text = await AppFixture.TextAsync(client, $"/Todos/Details?id={id}&tab=history");
        Assert.Contains("Verantwortlich:", text);
        Assert.Contains("Fälligkeit:", text);
    }

    [DatabaseFact]
    public async Task Deleting_a_project_keeps_its_todos_as_stand_alone_todos()
    {
        var (client, adminId) = await SignedInAsync();
        var (project, todo, _) = await _app.CreateProjectWithAttachmentAsync("Löschprobe");

        var response = await AppFixture.PostFormAsync(client, $"/Projects/Edit?id={project.Id}", new Dictionary<string, string> { ["Id"] = project.Id.ToString() }, $"/Projects/Edit?id={project.Id}&handler=Delete");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            Assert.False(await db.Project.AnyAsync(p => p.Id == project.Id));
            Assert.Equal(UpdateState.Deleted, (await db.Project.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id)).UpdateState);
            var kept = await db.Todo.AsNoTracking().FirstAsync(t => t.Id == todo.Id);
            Assert.Null(kept.ProjectId);
        });
        _ = adminId;
    }

    [DatabaseFact]
    public async Task The_csv_export_makes_formulas_harmless()
    {
        var (client, adminId) = await SignedInAsync();
        await CreateTodoAsync(client, adminId, "=HYPERLINK(\"http://böse\")");
        var response = await client.GetAsync("/Reports?handler=Csv&kind=todos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.ToString());
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("Titel;Verantwortlich;Status", csv);
        Assert.DoesNotContain(";=HYPERLINK", csv.Replace("\r\n", "\n").Replace("\n=", "\n").Replace("\"'=", "\"'="));
        Assert.Contains("'=HYPERLINK", csv);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Reports?handler=Csv&kind=unknown")).StatusCode);
    }

    [GeneratedRegex("name=\"np-csrf\" content=\"([^\"]+)\"")]
    private static partial Regex Token();
}
