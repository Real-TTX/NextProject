using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Todos;

/// <summary>The todos as a board: a column per status. The same toolbar as the list (search, filter, sort) sits above it.</summary>
public sealed class BoardModel(NpDbContext db, TodoService todos, ICurrentUser me, Lookups lookups, PlanningService planning) : PageModelBase
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public BoardView Board { get; private set; } = null!;

    public async Task OnGetAsync([FromQuery(Name = "mine")] bool mine)
    {
        Head("Board", "Die Todos nach Status. Ziehe eine Karte in eine andere Spalte, um den Status zu ändern.", ("Meine Todos", "/Todos"), ("Board", null));
        ViewData["ActiveHref"] = "/Todos";
        var today = planning.Today;
        var meId = me.UserId ?? 0;
        var all = db.Todo.AsNoTracking().Include(t => t.OwnerUser).Include(t => t.Project).Where(t => t.Status != TodoStatus.Discarded);
        if (mine)
        {
            all = all.Where(t => t.OwnerUserId == meId || t.Contributors.Any(c => c.UserId == meId));
        }

        var users = await lookups.UsersAsync();
        var projects = await lookups.ProjectsAsync();
        var spec = new ListSpec<Todo>
        {
            Id = "board",
            Action = "/Todos/Board",
            Label = "Board",
            Noun = "Todos",
            SearchPlaceholder = "Titel ...",
            Search = (q, text) => q.Where(t => EF.Functions.ILike(t.Title, Like.Contains(text))),
            Filters =
            [
                new FilterSpec<Todo>("prio", "Priorität", ListBuilder.EnumOptions<Level>(Labels.Of), (q, v) => q.Where(t => t.Priority == Enum.Parse<Level>(v))),
                new FilterSpec<Todo>("projekt", "Projekt", [.. projects.Select(p => new FilterOption(p.Value!, p.Text))], (q, v) => q.Where(t => t.ProjectId == long.Parse(v))),
                new FilterSpec<Todo>("owner", "Verantwortlich", [.. users.Select(u => new FilterOption(u.Value!, u.Text))], (q, v) => q.Where(t => t.OwnerUserId == long.Parse(v))),
            ],
            Sorts = new Dictionary<string, (string, Func<IQueryable<Todo>, bool, IOrderedQueryable<Todo>>)>
            {
                ["due"] = ("Fälligkeit", (q, d) => d ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)),
                ["title"] = ("Titel", (q, d) => d ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title)),
            },
            DefaultSort = "due",
            PageSize = 500,
            Keep = new Dictionary<string, string?> { ["mine"] = mine ? "true" : null },
        };

        var result = await ListBuilder.RunAsync(all, Request, spec, HttpContext.RequestAborted);
        Toolbar = result.Toolbar;
        Board = new BoardView("todo-board", result.Items, today);
    }

    /// <summary>The select of a card (a phone, the keyboard): changes the status and comes back to the board.</summary>
    public async Task<IActionResult> OnPostMoveAsync(long id, TodoStatus to)
    {
        var back = SafeReturn(Request.Headers.Referer.ToString() is { Length: > 0 } referer && Uri.TryCreate(referer, UriKind.Absolute, out var uri) ? uri.PathAndQuery : null, "/Todos/Board");
        if (to == TodoStatus.Blocked)
        {
            FlashError("Zum Blockieren brauchst du einen Grund. Öffne das Todo und halte das Hindernis fest.");
            return RedirectToPage("/Todos/Details", new { id });
        }

        if (await todos.ChangeStatusAsync(id, to) is { } problem)
        {
            FlashError(problem);
        }

        return LocalRedirect(back);
    }
}
