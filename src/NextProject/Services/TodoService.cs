using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.UiKit;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>What of a todo is written to its history when it changes.</summary>
public sealed record TodoSnapshot(TodoStatus Status, long OwnerUserId, DateOnly? DueDate, Level Priority)
{
    public static TodoSnapshot Of(Todo t) => new(t.Status, t.OwnerUserId, t.DueDate, t.Priority);
}

/// <summary>
/// The rules of a todo's life: what a status change needs (a reason for a block, finished dependencies for done), when the time of completion is set,
/// and what is written to the history. The pages ask this service instead of setting the fields themselves.
/// </summary>
public sealed class TodoService(NpDbContext db, HistoryService history, TimeProvider clock)
{
    /// <summary>Null when the todo may go to <paramref name="to"/>, else what is in the way (in words for the person).</summary>
    public async Task<string?> CheckStatusAsync(Todo todo, TodoStatus to, string? blockReason, CancellationToken ct = default)
    {
        if (to == TodoStatus.Blocked && string.IsNullOrWhiteSpace(blockReason))
        {
            return "Bitte beschreibe, was die Aufgabe blockiert.";
        }

        if (to == TodoStatus.Done && todo.Id != 0)
        {
            var open = await OpenDependenciesAsync(todo.Id, ct);
            if (open.Count > 0)
            {
                return "Diese Aufgabe wartet noch auf: " + string.Join(", ", open.Select(o => o.Title)) + ".";
            }
        }

        return null;
    }

    /// <summary>Puts the todo into the status: the time of completion is set when it becomes done and cleared when it is taken up again; the block is cleared when it is lifted.</summary>
    public void Apply(Todo todo, TodoStatus to, string? blockReason = null, string? blockNeeds = null)
    {
        todo.Status = to;
        if (to == TodoStatus.Done)
        {
            todo.CompletedDate ??= clock.GetUtcNow().UtcDateTime;
        }
        else
        {
            todo.CompletedDate = null;
        }

        if (to == TodoStatus.Blocked)
        {
            todo.BlockReason = blockReason?.Trim();
            todo.BlockNeeds = string.IsNullOrWhiteSpace(blockNeeds) ? null : blockNeeds.Trim();
        }
        else
        {
            todo.BlockReason = null;
            todo.BlockNeeds = null;
        }
    }

    /// <summary>A quick change of the status (the buttons of a todo, a card dragged on the board). Returns what is in the way, or null when it worked.</summary>
    public async Task<string?> ChangeStatusAsync(long id, TodoStatus to, string? blockReason = null, string? blockNeeds = null, CancellationToken ct = default)
    {
        var todo = await db.Todo.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (todo is null)
        {
            return "Die Aufgabe gibt es nicht mehr.";
        }

        if (todo.Status == to)
        {
            return null;
        }

        if (await CheckStatusAsync(todo, to, blockReason, ct) is { } problem)
        {
            return problem;
        }

        var before = TodoSnapshot.Of(todo);
        var blockNote = to == TodoStatus.Blocked ? Join(blockReason, blockNeeds) : null;
        Apply(todo, to, blockReason, blockNeeds);
        await RecordAsync(todo, before, blockNote, ct);
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>
    /// Moves the dates of a todo so that it is due on <paramref name="newDue"/> (the calendar: a card dropped on another day, a new date in the dialog). A start date moves with it,
    /// so the todo keeps its length. A finished or discarded todo is not moved. Returns the new dates, or what is in the way.
    /// </summary>
    public async Task<(DateOnly? Start, DateOnly? Due, string? Error)> MoveAsync(long id, DateOnly newDue, CancellationToken ct = default)
    {
        var todo = await db.Todo.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (todo is null)
        {
            return (null, null, "Die Aufgabe gibt es nicht mehr.");
        }

        if (todo.IsClosed)
        {
            return (todo.StartDate, todo.DueDate, "Erledigte und verworfene Todos werden nicht verschoben. Öffne es zuerst wieder.");
        }

        if (newDue.Year is < 2000 or > 2100)
        {
            return (todo.StartDate, todo.DueDate, "Dieses Datum ist nicht sinnvoll.");
        }

        var before = TodoSnapshot.Of(todo);
        if (todo.DueDate is { } old && todo.StartDate is { } start)
        {
            todo.StartDate = start.AddDays(newDue.DayNumber - old.DayNumber);
        }
        else if (todo.StartDate is { } lone && lone > newDue)
        {
            todo.StartDate = newDue;
        }

        todo.DueDate = newDue;
        await RecordAsync(todo, before, ct: ct);
        await db.SaveChangesAsync(ct);
        return (todo.StartDate, todo.DueDate, null);
    }

    /// <summary>Writes what differs from <paramref name="before"/> to the history (status, owner, due date, priority). The caller saves.</summary>
    public async Task RecordAsync(Todo todo, TodoSnapshot before, string? statusNote = null, CancellationToken ct = default)
    {
        if (before.Status != todo.Status)
        {
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.Status, Labels.Of(before.Status), Labels.Of(todo.Status), statusNote);
        }

        if (before.OwnerUserId != todo.OwnerUserId)
        {
            var names = await db.User.IgnoreQueryFilters().Where(u => u.Id == before.OwnerUserId || u.Id == todo.OwnerUserId).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.Owner, names.GetValueOrDefault(before.OwnerUserId, "?"), names.GetValueOrDefault(todo.OwnerUserId, "?"));
        }

        if (before.DueDate != todo.DueDate)
        {
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.DueDate, Fmt.Date(before.DueDate), Fmt.Date(todo.DueDate));
        }

        if (before.Priority != todo.Priority)
        {
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.Priority, Labels.Of(before.Priority), Labels.Of(todo.Priority));
        }
    }

    /// <summary>The todos this one waits for that are not finished yet.</summary>
    public Task<List<Todo>> OpenDependenciesAsync(long todoId, CancellationToken ct = default)
        => db.TodoDependency.AsNoTracking()
            .Where(d => d.TodoId == todoId && d.DependsOnTodo!.Status != TodoStatus.Done && d.DependsOnTodo.Status != TodoStatus.Discarded)
            .Select(d => d.DependsOnTodo!).ToListAsync(ct);

    /// <summary>Whether <paramref name="todoId"/> waiting for <paramref name="dependsOnId"/> would make a circle (A waits for B waits for A).</summary>
    public async Task<bool> WouldCycleAsync(long todoId, long dependsOnId, CancellationToken ct = default)
    {
        if (todoId == dependsOnId)
        {
            return true;
        }

        var edges = await db.TodoDependency.AsNoTracking().Select(d => new { d.TodoId, d.DependsOnTodoId }).ToListAsync(ct);
        var waitsFor = edges.ToLookup(e => e.TodoId, e => e.DependsOnTodoId);
        var seen = new HashSet<long>();
        var stack = new Stack<long>([dependsOnId]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == todoId)
            {
                return true;
            }

            if (seen.Add(current))
            {
                foreach (var next in waitsFor[current])
                {
                    stack.Push(next);
                }
            }
        }

        return false;
    }

    private static string? Join(string? reason, string? needs)
    {
        var parts = new[] { reason?.Trim(), string.IsNullOrWhiteSpace(needs) ? null : "Benötigt: " + needs.Trim() }.Where(p => !string.IsNullOrEmpty(p));
        var text = string.Join(" · ", parts);
        return text.Length == 0 ? null : text;
    }
}
