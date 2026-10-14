using NextProject.Services;
using NextProject.UiKit;

namespace NextProject.Pages.Calendar;

/// <summary>
/// What the three views of the calendar (the calendar, the schedule of the projects, the workload) have in common: the tabs above them, the filters of the toolbar and how
/// they are read from the address. One toolbar above the view, like above every list; the filters are the same words everywhere.
/// </summary>
public static class CalendarToolbar
{
    public static LinkTabsModel Tabs(string selected, string? query = null)
    {
        var q = string.IsNullOrEmpty(query) ? "" : "?" + query;
        return new LinkTabsModel("Ansicht des Kalenders",
        [
            new("kalender", "Kalender", "/Calendar" + q, "calendar"),
            new("zeitplan", "Zeitplan der Projekte", "/Calendar/Plan" + q, "route"),
            new("auslastung", "Auslastung", "/Calendar/Workload" + q, "users"),
        ], selected);
    }

    private static long? Id(HttpRequest request, string name) => long.TryParse(request.Query[name], out var id) && id > 0 ? id : null;

    private static string? Text(HttpRequest request, string name) => request.Query[name].ToString() is { Length: > 0 } text ? text.Trim() : null;

    /// <summary>The filter of the address. Without "wessen" the calendar shows what concerns the person; with a person chosen it shows that person's dates.</summary>
    public static CalendarFilter Read(HttpRequest request, long userId)
        => new(userId, Mine: Text(request, "wessen") != "alle", ProjectId: Id(request, "projekt"), OwnerId: Id(request, "person"), DepartmentId: Id(request, "abteilung"), GoalId: Id(request, "ziel"),
            Kind: Text(request, "art") is { } kind && CalendarService.Kinds.Contains(kind) ? kind : null, State: Text(request, "status") is "offen" or "erledigt" or "ueberfaellig" ? Text(request, "status") : null, Text: Text(request, "q"));

    /// <summary>The filter fields that a view offers, in this order; <paramref name="names"/> picks them (wessen, projekt, person, abteilung, ziel, art, status).</summary>
    public static async Task<List<FilterField>> FieldsAsync(HttpRequest request, Lookups lookups, params string[] names)
    {
        static IReadOnlyList<FilterOption> Of(IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> items) => [.. items.Select(i => new FilterOption(i.Value!, i.Text))];
        var fields = new List<FilterField>();
        foreach (var name in names)
        {
            var selected = request.Query[name].ToString() is { Length: > 0 } value ? value : null;
            fields.Add(name switch
            {
                "wessen" => new FilterField(name, "Wessen Termine", [new("meine", "Meine"), new("alle", "Alle")], selected),
                "projekt" => new FilterField(name, "Projekt", Of(await lookups.ProjectsAsync()), selected),
                "person" => new FilterField(name, "Person", Of(await lookups.UsersAsync()), selected),
                "abteilung" => new FilterField(name, "Abteilung", Of(await lookups.DepartmentsAsync()), selected),
                "ziel" => new FilterField(name, "Ziel", Of(await lookups.GoalsAsync()), selected),
                "art" => new FilterField(name, "Art des Termins", [.. CalendarService.Kinds.Select(k => new FilterOption(k, CalendarService.KindLabel(k)))], selected),
                "status" => new FilterField(name, "Stand", [new("offen", "Offen"), new("erledigt", "Erledigt"), new("ueberfaellig", "Überfällig")], selected),
                _ => throw new ArgumentException($"Unknown filter {name}", nameof(names)),
            });
        }

        return fields;
    }

    public static ToolbarModel Model(string id, string action, string label, string noun, HttpRequest request, List<FilterField> fields, int total, string searchPlaceholder = "Titel ...")
        => new()
        {
            Id = id,
            Action = action,
            Label = label,
            Noun = noun,
            Search = request.Query["q"].ToString() is { Length: > 0 } q ? q.Trim() : null,
            SearchPlaceholder = searchPlaceholder,
            Filters = fields,
            Total = total,
            Matching = total,
        };

    /// <summary>The query string of the filters in effect, to keep them when the person goes to another view of the calendar.</summary>
    public static string Query(HttpRequest request, params string[] names)
        => string.Join("&", names.Select(n => (n, v: request.Query[n].ToString())).Where(p => p.v.Length > 0).Select(p => $"{System.Net.WebUtility.UrlEncode(p.n)}={System.Net.WebUtility.UrlEncode(p.v)}"));
}
