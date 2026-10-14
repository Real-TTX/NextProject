using System.Net;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace NextProject.UiKit;

/// <summary>An entry of the user's context menu. A <see cref="Post"/> item changes something (sign out): a button of a small form, not a link.</summary>
public sealed record UserMenuItem(string Title, string Href, string Icon, bool Danger = false, bool SeparatorBefore = false, bool Post = false);

/// <summary>Everything the layout needs to draw the shell around a page: the menus, the user, the version.</summary>
public sealed class ShellModel
{
    public IReadOnlyList<NavSet> Menus { get; init; } = [];

    public bool HasSwitcher => Menus.Count > 1;

    public NavSet CurrentMenu(string? active) => Menus.FirstOrDefault(m => m.Contains(active)) ?? Menus[0];

    public string UserName { get; init; } = "";

    public string UserRole { get; init; } = "";

    public string UserLine { get; init; } = "";

    public IReadOnlyList<UserMenuItem> UserMenu { get; init; } = [];

    public string Version { get; init; } = "";

    public string Tagline { get; init; } = "Projekte, Ziele, Todos";

    /// <summary>"light" or "dark" when the user chose one; null follows the system.</summary>
    public string? Theme { get; init; }

    public string HomeHref { get; init; } = "/";

    /// <summary>The optional attributes of &lt;html&gt;, written only when they have a value (an empty data-theme would count as a choice).</summary>
    public string RootAttributes => Theme is "light" or "dark" ? $"data-theme=\"{WebUtility.HtmlEncode(Theme)}\"" : string.Empty;
}

/// <summary>Counts shown as badges in the menu. Cached for a minute per person: a badge may be a little late, a page must not wait for queries.</summary>
public sealed class NavCounts(IMemoryCache cache, NpDbContext db, ICurrentUser user, TimeProvider clock)
{
    public sealed record Counts(int MyOverdue, int ProjectsAtRisk);

    public async Task<Counts> GetAsync()
    {
        var me = user.UserId ?? 0;
        return (await cache.GetOrCreateAsync($"nav-counts-{me}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var today = Fmt.Today(clock);
            return new Counts(
                await db.Todo.CountAsync(t => t.OwnerUserId == me && t.DueDate != null && t.DueDate < today && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded),
                await db.Project.CountAsync(p => p.Status == ProjectStatus.AtRisk));
        }))!;
    }
}

/// <summary>Builds the shell for the person who is signed in: the administration menu only for administrators.</summary>
public sealed class ShellFactory(ICurrentUser user, NavCounts counts, IHttpContextAccessor accessor)
{
    public async Task<ShellModel> BuildAsync()
    {
        var principal = accessor.HttpContext?.User;
        var theme = principal?.FindFirst(AppClaims.Theme)?.Value;

        var menus = new List<NavSet> { await WorkMenuAsync() };
        if (user.IsAdmin)
        {
            menus.Add(AdminMenu());
        }

        return new ShellModel
        {
            Menus = menus,
            UserName = user.Name ?? "Gast",
            UserRole = user.IsAdmin ? "Administrator" : "Benutzer",
            UserLine = principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
            UserMenu = UserMenu(),
            Version = $"NextProject {AppVersion.Current}",
            Theme = theme,
        };
    }

    /// <summary>The shell of a page without a menu (sign-in, setup, a shared link).</summary>
    public static ShellModel Bare(string? theme = null) => new()
    {
        Menus = [new NavSet("none", "NextProject", "home", [])],
        Version = $"NextProject {AppVersion.Current}",
        Theme = theme,
    };

    private async Task<NavSet> WorkMenuAsync()
    {
        var c = await counts.GetAsync();
        return new NavSet("work", "Arbeit", "dashboard",
        [
            new(null, [new("Dashboard", "/", "dashboard")]),
            new("Arbeit",
            [
                new("Meine Todos", "/Todos", "check-square", c.MyOverdue > 0 ? NavBadge.Count(c.MyOverdue, "warn", "überfällige Todos") : null),
                new("Projekte", "/Projects", "folder", c.ProjectsAtRisk > 0 ? NavBadge.Count(c.ProjectsAtRisk, "warn", "gefährdete Projekte") : null),
                new("Ziele & OKRs", "/Goals", "target"),
            ]),
            new("Überblick",
            [
                new("Team", "/Team", "users"),
                new("Kalender", "/Calendar", "calendar"),
                new("Reviews", "/Reviews", "history"),
                new("Reports", "/Reports", "bar-chart"),
            ]),
        ]);
    }

    private static NavSet AdminMenu() => new("admin", "Einstellungen", "settings",
    [
        new("Organisation",
        [
            new("Benutzer", "/Admin/Users", "user"),
            new("Abteilungen", "/Admin/Departments", "briefcase"),
            new("Freigaben", "/Admin/Shares", "share"),
            new("Allgemein", "/Admin/Organization", "sliders"),
        ]),
        new("System",
        [
            new("Speicher (NAS)", "/Admin/Storage", "hard-drive"),
            new("Backup", null, "archive", Children:
            [
                new("Übersicht", "/Admin/Backup", null),
                new("Pläne", "/Admin/Backup/Plans", null),
                new("Ziele", "/Admin/Backup/Targets", null),
                new("Wiederherstellung", "/Admin/Backup/Restore", null),
            ]),
            new("Sicherheit", "/Admin/Security", "shield"),
        ]),
        new("Protokolle",
        [
            new("Aktivität", "/Admin/Activity", "activity"),
            new("System", "/Admin/System", "server"),
        ]),
    ]);

    private static List<UserMenuItem> UserMenu() =>
    [
        new("Mein Konto", "/Account/Profile", "user"),
        new("Sicherheit", "/Account/Security", "shield"),
        new("Abmelden", "/Account/Logout", "logout", Danger: true, SeparatorBefore: true, Post: true),
    ];
}
