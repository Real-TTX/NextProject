using System.Net;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NextProject.Tests.Integration;

/// <summary>The role "Anonym": a link shows a project read only, for a limited time, behind a password when it has one, and never more than it says.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class ShareTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _app;

    public ShareTests(AppFixture app) => _app = app;

    private async Task<(Project Project, ShareLink Link)> ProjectWithLinkAsync(string name, string? password = null, bool showTodos = true)
    {
        await _app.EnsureAdminAsync();
        var (project, _, _) = await _app.CreateProjectWithAttachmentAsync(name);
        var link = await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            db.TodoComment.Add(new TodoComment { TodoId = await db.Todo.Where(t => t.ProjectId == project.Id).Select(t => t.Id).FirstAsync(), Text = "GEHEIMER-KOMMENTAR-" + name });
            project.Description = "GEHEIME-BESCHREIBUNG-" + name;
            db.Project.Update(project);
            await db.SaveChangesAsync();
            return await sp.GetRequiredService<ShareService>().CreateAsync(ShareKind.Project, project.Id, "Stand " + name, "Hallo Besucher", 7, password, showTodos, null);
        });
        return (project, link);
    }

    [DatabaseFact]
    public async Task Anybody_with_the_link_sees_the_project_but_not_the_comments_or_the_attachments()
    {
        var (project, link) = await ProjectWithLinkAsync("Offen");
        using var visitor = _app.NewClient();

        var response = await visitor.GetAsync($"/s/{link.Token}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await AppFixture.TextAsync(response);
        Assert.Contains("Stand Offen", text);
        Assert.Contains("Hallo Besucher", text);
        Assert.Contains("Todo Offen", text);
        Assert.DoesNotContain("GEHEIMER-KOMMENTAR", text);
        Assert.DoesNotContain("Offen.txt", text);
        Assert.DoesNotContain("/Todos/Details", text);

        var counted = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().ShareLink.AsNoTracking().FirstAsync(l => l.Id == link.Id));
        Assert.Equal(1, counted.ViewCount);
        _ = project;
    }

    [DatabaseFact]
    public async Task A_link_can_leave_out_the_single_todos()
    {
        var (_, link) = await ProjectWithLinkAsync("OhneTodos", showTodos: false);
        using var visitor = _app.NewClient();
        var text = await AppFixture.TextAsync(visitor, $"/s/{link.Token}");
        Assert.Contains("Stand OhneTodos", text);
        Assert.DoesNotContain("Todo OhneTodos", text);
    }

    [DatabaseFact]
    public async Task A_password_is_asked_for_checked_and_remembered()
    {
        var (_, link) = await ProjectWithLinkAsync("Geschützt", "Passwort-123");
        using var visitor = _app.NewClient();

        var gate = await AppFixture.TextAsync(visitor, $"/s/{link.Token}");
        Assert.Contains("Für diese Freigabe brauchst du ein Passwort.", gate);
        Assert.DoesNotContain("Todo Geschützt", gate);

        var wrong = await AppFixture.PostFormAsync(visitor, $"/s/{link.Token}", new Dictionary<string, string> { ["Password"] = "falsch" }, $"/s/{link.Token}?handler=Password");
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("Das Passwort stimmt nicht.", await AppFixture.TextAsync(wrong));

        var right = await AppFixture.PostFormAsync(visitor, $"/s/{link.Token}", new Dictionary<string, string> { ["Password"] = "Passwort-123" }, $"/s/{link.Token}?handler=Password");
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
        Assert.Contains("Stand Geschützt", await AppFixture.TextAsync(visitor, $"/s/{link.Token}"));

        // somebody else still sees the gate
        using var other = _app.NewClient();
        Assert.Contains("Passwort", await AppFixture.TextAsync(other, $"/s/{link.Token}"));
    }

    [DatabaseFact]
    public async Task Too_many_wrong_passwords_shut_the_link_for_a_while()
    {
        var (_, link) = await ProjectWithLinkAsync("Gesperrt", "Passwort-123");
        using var visitor = _app.NewClient();
        for (var i = 0; i < ShareService.MaxWrongPasswords; i++)
        {
            await AppFixture.PostFormAsync(visitor, $"/s/{link.Token}", new Dictionary<string, string> { ["Password"] = "falsch" + i }, $"/s/{link.Token}?handler=Password");
        }

        var right = await AppFixture.PostFormAsync(visitor, $"/s/{link.Token}", new Dictionary<string, string> { ["Password"] = "Passwort-123" }, $"/s/{link.Token}?handler=Password");
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Contains("Zu viele falsche Versuche", await AppFixture.TextAsync(right));
    }

    [DatabaseFact]
    public async Task A_revoked_or_expired_link_says_so_and_an_unknown_one_is_not_found()
    {
        var (_, revoked) = await ProjectWithLinkAsync("Widerrufen");
        var (_, expired) = await ProjectWithLinkAsync("Abgelaufen");
        await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            (await db.ShareLink.FirstAsync(l => l.Id == revoked.Id)).RevokedDate = DateTime.UtcNow;
            (await db.ShareLink.FirstAsync(l => l.Id == expired.Id)).ExpiresDate = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        using var visitor = _app.NewClient();
        var r = await visitor.GetAsync($"/s/{revoked.Token}");
        Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
        var text = await AppFixture.TextAsync(r);
        Assert.Contains("widerrufen", text);

        var e = await visitor.GetAsync($"/s/{expired.Token}");
        Assert.Equal(HttpStatusCode.Gone, e.StatusCode);
        Assert.Contains("abgelaufen", await AppFixture.TextAsync(e));

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/s/{Guid.NewGuid()}")).StatusCode);
    }
}
