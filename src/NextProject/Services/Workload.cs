namespace NextProject.Services;

/// <summary>What one person has open: overdue, due in each of the next weeks, and without a date.</summary>
public sealed record WorkloadRow(long UserId, string Name, string? Job, string? Department, int Overdue, int[] Weeks, int NoDate)
{
    public int Total => Overdue + Weeks.Sum() + NoDate;
}

/// <summary>
/// The workload table: open todos per person and week. A todo counts in the week of its due date; one that is overdue counts in the column "overdue" and in no week, so that
/// nothing is counted twice. Todos that are done or discarded are not passed in. A week is Monday to Sunday.
/// </summary>
public static class Workload
{
    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public static List<WorkloadRow> Build(IEnumerable<(long OwnerId, DateOnly? Due)> openTodos, IEnumerable<(long Id, string Name, string? Job, string? Department)> people, DateOnly firstWeek, int weeks, DateOnly today)
    {
        var byOwner = openTodos.ToLookup(t => t.OwnerId, t => t.Due);
        return [.. people.Select(p =>
        {
            var mine = byOwner[p.Id].ToList();
            var counts = new int[weeks];
            for (var i = 0; i < weeks; i++)
            {
                var from = firstWeek.AddDays(7 * i);
                var to = from.AddDays(6);
                counts[i] = mine.Count(d => d is { } due && due >= today && due >= from && due <= to);
            }

            return new WorkloadRow(p.Id, p.Name, p.Job, p.Department, mine.Count(d => d is { } due && due < today), counts, mine.Count(d => d is null));
        })];
    }

    /// <summary>0 nothing, 1 a normal week, 2 a full one, 3 more than a person can do well (the table marks it; the number is always written as well).</summary>
    public static int Level(int count) => count switch
    {
        0 => 0,
        <= 3 => 1,
        <= 6 => 2,
        _ => 3,
    };
}
