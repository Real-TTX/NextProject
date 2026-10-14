using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Reviews;

/// <summary>The reviews of projects and goals: where does the implementation differ from the strategy, what did we learn, where do we adjust.</summary>
public sealed class IndexModel(NpDbContext db) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public TableModel Table { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Head("Reviews", "Wirken die Projekte wie geplant auf die Strategie? Was lernen wir, wo justieren wir?", ("Reviews", null));
        ViewData["ActiveHref"] = "/Reviews";

        var spec = new ListSpec<Review>
        {
            Id = "reviews",
            Action = "/Reviews",
            Label = "Reviews",
            Noun = "Reviews",
            SearchPlaceholder = "Texte, Projekt oder Ziel ...",
            Search = (q, text) =>
            {
                var like = Like.Contains(text);
                return q.Where(r => EF.Functions.ILike(r.Deviation ?? "", like) || EF.Functions.ILike(r.Learnings ?? "", like) || EF.Functions.ILike(r.Adjustments ?? "", like)
                    || EF.Functions.ILike(r.Steering ?? "", like) || (r.Project != null && EF.Functions.ILike(r.Project.Name, like)) || (r.Goal != null && EF.Functions.ILike(r.Goal.Title, like)));
            },
            Filters =
            [
                new FilterSpec<Review>("bezug", "Bezug", [new("projekt", "Projekte"), new("ziel", "Ziele")], (q, v) => v == "projekt" ? q.Where(r => r.ProjectId != null) : q.Where(r => r.GoalId != null)),
                new FilterSpec<Review>("stand", "Einschätzung", ListBuilder.EnumOptions<ReviewHealth>(Labels.Of), (q, v) => q.Where(r => r.Health == Enum.Parse<ReviewHealth>(v))),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Review>, bool, IOrderedQueryable<Review>>)>
            {
                ["date"] = ("Datum", (q, d) => d ? q.OrderBy(r => r.ReviewDate) : q.OrderByDescending(r => r.ReviewDate)),
                ["next"] = ("Nächstes Review", (q, d) => d ? q.OrderByDescending(r => r.NextReviewDate) : q.OrderBy(r => r.NextReviewDate == null).ThenBy(r => r.NextReviewDate)),
            },
            DefaultSort = "date",
        };

        var result = await ListBuilder.RunAsync(db.Review.AsNoTracking().Include(r => r.Project).Include(r => r.Goal).Include(r => r.CreateUser), Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Pager = result.Pager;
        Table = TableModel.Of("review-table", "Reviews", result.Items,
        [
            (new TableColumn("Review"), r => Ui.NameWithSub(Fmt.Date(r.ReviewDate) + ": " + (r.Project?.Name ?? r.Goal?.Title ?? "?"), $"/Reviews/Edit?id={r.Id}", r.Project is not null ? "Projekt" : "Ziel")),
            (new TableColumn("Einschätzung"), r => Labels.Pill(r.Health)),
            (new TableColumn("Abweichung und Ursache", HideOnPhone: true), r => Ui.Text(Short(r.Deviation))),
            (new TableColumn("Nachschärfung", HideOnPhone: true), r => Ui.Text(Short(r.Adjustments))),
            (new TableColumn("Von", HideOnPhone: true), r => Ui.Person(r.CreateUser?.DisplayName)),
            (new TableColumn("Nächstes Review", HideOnPhone: true), r => Ui.Text(Fmt.Date(r.NextReviewDate))),
        ],
        r => [new RowAction("edit", "Bearbeiten", $"/Reviews/Edit?id={r.Id}")])
        with
        {
            EmptyTitle = Toolbar.Total == 0 ? "Noch kein Review" : "Nichts gefunden",
            EmptyText = Toolbar.Total == 0 ? "Ein Review hält fest, wo die Umsetzung von der Strategie abweicht und was ihr daraus macht." : "Ändere die Suche oder den Filter.",
            EmptyIcon = "history",
        };
    }

    private static string? Short(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Length <= 90 ? text : text[..90] + "…";
}
