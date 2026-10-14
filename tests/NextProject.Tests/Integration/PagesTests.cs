using System.Net;
using NextProject.Data;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NextProject.Tests.Integration;

/// <summary>Every page of the application opens, with the sample data and as the people who may see it, and the roles keep to their side.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class PagesTests : IClassFixture<AppFixture>
{
    private readonly AppFixture _app;

    public PagesTests(AppFixture app) => _app = app;

    [DatabaseFact]
    public async Task The_sample_data_is_written_once_and_never_over_real_data()
    {
        await _app.EnsureAdminAsync();
        var adminId = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Select(u => u.Id).FirstAsync());
        Assert.True(await _app.WithScopeAsync(sp => sp.GetRequiredService<DemoData>().CreateAsync(adminId)));
        Assert.False(await _app.WithScopeAsync(sp => sp.GetRequiredService<DemoData>().CreateAsync(adminId)));
        Assert.True(await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().Todo.CountAsync()) > 10);
    }

    [DatabaseFact]
    public async Task Every_page_opens_for_an_administrator()
    {
        await _app.EnsureAdminAsync();
        var adminId = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().User.Select(u => u.Id).FirstAsync());
        await _app.WithScopeAsync(sp => sp.GetRequiredService<DemoData>().CreateAsync(adminId));
        using var client = _app.NewClient();
        await AppFixture.LoginAsync(client, "admin@example.test", "Admin-Passwort-2026");

        var (todo, project, goal, keyResult, milestone, risk, review, department, share) = await _app.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<NpDbContext>();
            var link = await sp.GetRequiredService<ShareService>().CreateAsync(Data.Entities.ShareKind.Project, await db.Project.Select(p => p.Id).FirstAsync(), "Probe", null, 7, null, true, null);
            return (await db.Todo.Select(t => t.Id).FirstAsync(), await db.Project.Select(p => p.Id).FirstAsync(), await db.Goal.Select(g => g.Id).FirstAsync(), await db.KeyResult.Select(k => k.Id).FirstAsync(),
                await db.Milestone.Select(m => m.Id).FirstAsync(), await db.Risk.Select(r => r.Id).FirstAsync(), await db.Review.Select(r => r.Id).FirstAsync(), await db.Department.Select(d => d.Id).FirstAsync(), link.Id);
        });
        var checkIn = await _app.WithScopeAsync(sp => sp.GetRequiredService<NpDbContext>().KeyResultCheckIn.Select(c => c.Id).FirstAsync());

        string[] urls =
        [
            "/", "/Todos", "/Todos?scope=heute", "/Todos?scope=woche", "/Todos?scope=team", "/Todos?scope=team&status=Done&sort=prio&dir=desc", "/Todos/Board", "/Todos/Board?mine=true", "/Todos/Edit",
            $"/Todos/Details?id={todo}", $"/Todos/Details?id={todo}&tab=check", $"/Todos/Edit?id={todo}", $"/Todos/Edit?projectId={project}", "/Projects", "/Projects?status=aktiv&sort=end",
            "/Projects/Edit", $"/Projects/Edit?id={project}", $"/Projects/Details?id={project}", $"/Projects/Details?id={project}&tab=todos", $"/Projects/Details?id={project}&tab=board",
            $"/Projects/Details?id={project}&tab=timeline", $"/Projects/Details?id={project}&tab=milestones", $"/Projects/Details?id={project}&tab=risks", $"/Projects/Details?id={project}&tab=goals",
            $"/Projects/Details?id={project}&tab=history", $"/Projects/Milestone?projectId={project}", $"/Projects/Milestone?projectId={project}&id={milestone}", $"/Projects/Risk?projectId={project}",
            $"/Projects/Risk?projectId={project}&id={risk}", $"/Shares/Create?kind=Project&id={project}", $"/Shares/Create?kind=Goal&id={goal}", "/Goals", "/Goals/Edit", $"/Goals/Edit?id={goal}",
            $"/Goals/Details?id={goal}", $"/Goals/Details?id={goal}&tab=results", $"/Goals/Details?id={goal}&tab=projects", $"/Goals/Details?id={goal}&tab=todos", $"/Goals/Details?id={goal}&tab=reviews",
            "/Goals/KeyResult", $"/Goals/KeyResult?goalId={goal}", $"/Goals/KeyResult?id={keyResult}", $"/Goals/CheckIn?keyResultId={keyResult}", $"/Goals/CheckIn?keyResultId={keyResult}&id={checkIn}",
            "/Team", "/Team?sort=department", "/Calendar", "/Calendar?scope=alle", "/Reviews", "/Reviews/Edit", $"/Reviews/Edit?id={review}", "/Reports",
            "/Admin/Users", "/Admin/Users/Edit", "/Admin/Departments", $"/Admin/Departments/Edit?id={department}", "/Admin/Shares", $"/Admin/Shares/Edit?id={share}", "/Admin/Organization",
            "/Admin/Storage", "/Admin/Backup", "/Admin/Backup/Plans", "/Admin/Backup/Plans/Edit", "/Admin/Backup/Targets", "/Admin/Backup/Targets/Edit", "/Admin/Backup/Restore", "/Admin/Security", "/Admin/Activity",
            "/Admin/System", "/Account/Profile", "/Account/Security",
        ];

        var failures = new List<string>();
        foreach (var url in urls)
        {
            var response = await client.GetAsync(url);
            var text = response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadAsStringAsync() : "";
            if (response.StatusCode != HttpStatusCode.OK || text.Contains("Exception", StringComparison.Ordinal) || !text.Contains("<h1", StringComparison.Ordinal))
            {
                failures.Add($"{(int)response.StatusCode} {url}");
            }
        }

        Assert.True(failures.Count == 0, "Pages that failed: " + string.Join(", ", failures));
    }

    [DatabaseFact]
    public async Task A_user_stays_out_of_the_settings_and_the_anonymous_visitor_is_sent_to_the_sign_in()
    {
        await _app.EnsureAdminAsync();
        await _app.CreateUserAsync("normal@example.test", "Normal-Passwort-2026", Data.Entities.UserRole.User);

        using var anonymous = _app.NewClient();
        foreach (var url in new[] { "/", "/Todos", "/Projects", "/Goals", "/Reports", "/Calendar", "/Team", "/Admin/Users", "/api/todos/1/status" })
        {
            var response = anonymous.GetAsync(url);
            var status = (await response).StatusCode;
            Assert.True(status is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, $"{url} answered {(int)status} to somebody who is not signed in");
        }

        using var user = _app.NewClient();
        await AppFixture.LoginAsync(user, "normal@example.test", "Normal-Passwort-2026");
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/Todos")).StatusCode);
        foreach (var url in new[] { "/Admin/Users", "/Admin/Backup", "/Admin/Shares", "/Admin/System", "/Admin/Departments", "/Admin/Security" })
        {
            var status = (await user.GetAsync(url)).StatusCode;
            Assert.NotEqual(HttpStatusCode.OK, status);
        }

        // the menu of a user has no settings
        Assert.DoesNotContain("/Admin/Users", await AppFixture.TextAsync(user, "/Todos"));
    }
}
