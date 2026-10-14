using NextProject.Data.Entities;

namespace NextProject.UiKit;

/// <summary>One column of the board: a status, the cards shown and how many there are in all (the done column shows only the newest).</summary>
public sealed record BoardColumn(TodoStatus Status, IReadOnlyList<Todo> Cards, int Total);

/// <summary>
/// The kanban board of todos: a column per status (discarded ones have none), a card per todo. Several boards can share a page: every id is derived from
/// <see cref="Id"/>. Dragging a card to another column changes its status (the script of the app posts it); every card also has a select for the same, which is
/// what a phone and the keyboard use.
/// </summary>
public sealed record BoardView(string Id, IReadOnlyList<Todo> Todos, DateOnly Today, bool ShowProject = true, int DoneLimit = 30)
{
    public static readonly TodoStatus[] Statuses = [TodoStatus.Open, TodoStatus.InProgress, TodoStatus.Blocked, TodoStatus.InReview, TodoStatus.Done];

    public IReadOnlyList<BoardColumn> Columns()
        => [.. Statuses.Select(s =>
        {
            var all = Todos.Where(t => t.Status == s);
            var ordered = s == TodoStatus.Done
                ? all.OrderByDescending(t => t.CompletedDate)
                : all.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority == Level.High);
            var cards = ordered.Take(s == TodoStatus.Done ? DoneLimit : int.MaxValue).ToList();
            return new BoardColumn(s, cards, all.Count());
        })];
}
