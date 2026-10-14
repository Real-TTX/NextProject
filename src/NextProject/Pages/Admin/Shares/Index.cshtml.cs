using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Admin.Shares;

/// <summary>Every link that shows a project or a goal to somebody without an account: who has it, how long it holds, how often it was opened.</summary>
public sealed class IndexModel(NpDbContext db, TimeProvider clock) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Head("Freigaben", "Links für alle ohne Konto: lesen, für begrenzte Zeit.", ("Einstellungen", null), ("Freigaben", null));
        var now = clock.GetUtcNow().UtcDateTime;

        var spec = new ListSpec<ShareLink>
        {
            Id = "shares",
            Action = "/Admin/Shares",
            Label = "Freigaben",
            Noun = "Freigaben",
            SearchPlaceholder = "Titel, Empfänger, Projekt oder Ziel ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(l => EF.Functions.ILike(l.Title, like) || EF.Functions.ILike(l.RecipientName ?? "", like) || (l.Project != null && EF.Functions.ILike(l.Project.Name, like)) || (l.Goal != null && EF.Functions.ILike(l.Goal.Title, like)));
            },
            Filters =
            [
                new FilterSpec<ShareLink>("art", "Art", ListBuilder.EnumOptions<ShareKind>(k => k == ShareKind.Project ? "Projekt" : "Ziel"), (q, v) => q.Where(l => l.Kind == Enum.Parse<ShareKind>(v))),
                new FilterSpec<ShareLink>("status", "Status", [new("aktiv", "Gültig"), new("abgelaufen", "Abgelaufen oder widerrufen")], (q, v) => v == "aktiv" ? q.Where(l => l.RevokedDate == null && l.ExpiresDate > now) : q.Where(l => l.RevokedDate != null || l.ExpiresDate <= now)),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<ShareLink>, bool, IOrderedQueryable<ShareLink>>)>
            {
                ["created"] = ("Angelegt", (q, d) => d ? q.OrderBy(l => l.CreateDate) : q.OrderByDescending(l => l.CreateDate)),
                ["expires"] = ("Gültig bis", (q, d) => d ? q.OrderByDescending(l => l.ExpiresDate) : q.OrderBy(l => l.ExpiresDate)),
                ["views"] = ("Aufrufe", (q, d) => d ? q.OrderBy(l => l.ViewCount) : q.OrderByDescending(l => l.ViewCount)),
            },
            DefaultSort = "created",
        };

        var result = await ListBuilder.RunAsync(db.ShareLink.AsNoTracking().Include(l => l.Project).Include(l => l.Goal), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        Table = TableModel.Of("share-table", "Freigaben", result.Items,
        [
            (new TableColumn("Link"), l => Ui.NameWithSub(l.Title, $"/Admin/Shares/Edit?id={l.Id}", l.RecipientName is { Length: > 0 } ? "für " + l.RecipientName : null)),
            (new TableColumn("Zeigt", HideOnPhone: true), l => Ui.Text((l.Kind == ShareKind.Project ? "Projekt: " + l.Project?.Name : "Ziel: " + l.Goal?.Title))),
            (new TableColumn("Gültig bis"), l => Ui.Text(Fmt.Date(l.ExpiresDate))),
            (new TableColumn("Aufrufe", Numeric: true, HideOnPhone: true), l => Ui.Text(l.ViewCount.ToString())),
            (new TableColumn("Status"), l => l.RevokedDate is not null ? Ui.Pill("widerrufen", "plain") : l.ExpiresDate <= now ? Ui.Pill("abgelaufen", "plain") : Ui.Pill("gültig", "ok")),
        ],
        l => [new RowAction("edit", "Öffnen", $"/Admin/Shares/Edit?id={l.Id}")])
        with { EmptyTitle = Toolbar.Total == 0 ? "Noch keine Freigabe" : "Nichts gefunden", EmptyText = Toolbar.Total == 0 ? "Links entstehen auf der Seite eines Projekts oder Ziels, mit „Freigeben“." : "Ändere die Suche oder den Filter.", EmptyIcon = "share" };
    }
}
