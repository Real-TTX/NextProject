using System.Globalization;
using NextProject.Data.Entities;
using NextProject.Services;

namespace NextProject.UiKit;

/// <summary>A division of the time axis: a month, or a week. Positions are percent of the visible time.</summary>
public sealed record GanttTick(string Label, double Left, double Width);

/// <summary>A bar: from its start to its end, cut where it goes on beyond the visible time. <see cref="Progress"/> (0 to 100) fills it with the share of the work that is done.</summary>
public sealed record GanttBar(double Left, double Width, string Tone, bool CutLeft, bool CutRight, string Label, string Dates, double? Progress);

public sealed record GanttMark(double Left, string Tone, string Label);

/// <summary>One row: a project (level 0, with its milestones) or a todo of a project (level 1, hidden until the project is opened).</summary>
public sealed record GanttRow(string Key, string? ParentKey, string Label, string? Sub, string Href, GanttBar? Bar, IReadOnlyList<GanttMark> Marks, int ChildCount)
{
    public int Level => ParentKey is null ? 0 : 1;
}

/// <summary>
/// The schedule of the projects: one row per project, the time from left to right. A window of months (3, 6 or 12), months and, when it is short, weeks along the top,
/// a line for today. The positions are set by the script from <c>data-np-bar</c> (no inline style).
/// </summary>
public sealed record GanttView(DateOnly From, DateOnly To, IReadOnlyList<GanttTick> Months, IReadOnlyList<GanttTick> Weeks, double? TodayLeft, IReadOnlyList<GanttRow> Rows, int Hidden)
{
    public const int MaxTodosPerProject = 30;

    /// <summary>The months of a zoom: 3, 6 or 12.</summary>
    public static int MonthsOf(string zoom) => zoom switch { "quartal" => 3, "jahr" => 12, _ => 6 };

    public static GanttView Build(DateOnly today, int offsetMonths, int months, IReadOnlyCollection<Project> projects, IReadOnlyCollection<Milestone> milestones, IReadOnlyCollection<Todo> todos, IReadOnlyDictionary<long, ProjectFacts> facts)
    {
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-1 + offsetMonths);
        var to = from.AddMonths(months).AddDays(-1);
        double total = to.DayNumber - from.DayNumber + 1;
        double Pos(DateOnly d) => (d.DayNumber - from.DayNumber) / total * 100;

        var monthTicks = new List<GanttTick>();
        for (var m = from; m <= to; m = m.AddMonths(1))
        {
            var end = m.AddMonths(1).AddDays(-1);
            monthTicks.Add(new GanttTick(months > 6 ? m.ToString("MMM", Fmt.Culture) : m.ToString("MMMM yyyy", Fmt.Culture), Pos(m), (end.DayNumber - m.DayNumber + 1) / total * 100));
        }

        var weekTicks = new List<GanttTick>();
        if (months <= 3)
        {
            for (var w = Workload.MondayOf(from); w <= to; w = w.AddDays(7))
            {
                var left = Math.Max(0, Pos(w));
                var right = Math.Min(100, Pos(w.AddDays(7)));
                if (right > left)
                {
                    weekTicks.Add(new GanttTick(ISOWeek.GetWeekOfYear(w.ToDateTime(TimeOnly.MinValue)).ToString(Fmt.Culture), left, right - left));
                }
            }
        }

        var rows = new List<GanttRow>();
        var hidden = 0;
        foreach (var p in projects.OrderBy(p => p.StartDate ?? p.EndDate ?? DateOnly.MaxValue).ThenBy(p => p.Name))
        {
            var start = p.StartDate ?? p.EndDate;
            var end = p.EndDate ?? p.StartDate;
            if (start is null || end is null || end < from || start > to)
            {
                hidden++;
                continue;
            }

            var f = facts.GetValueOrDefault(p.Id);
            var endangered = f?.IsEndangered == true || p.Status == ProjectStatus.AtRisk;
            var tone = p.Status == ProjectStatus.Completed ? "ok" : endangered ? "error" : p.Status == ProjectStatus.InProgress ? "busy" : p.Status == ProjectStatus.Planning ? "info" : "plain";
            var progress = f is { } facts1 && facts1.Todos.Total - facts1.Todos.Discarded > 0 ? (double)facts1.Todos.Completion * 100 : (double?)null;
            var bar = Bar(start.Value, end.Value, from, to, Pos, tone, p.Name, Fmt.Period(p.StartDate, p.EndDate), progress);

            var marks = milestones.Where(m => m.ProjectId == p.Id && m.DueDate >= from && m.DueDate <= to).OrderBy(m => m.DueDate)
                .Select(m => new GanttMark(Pos(m.DueDate), m.ReachedDate is not null ? "ok" : m.DueDate < today ? "error" : "accent", $"{m.Title}, {Fmt.Date(m.DueDate)}")).ToList();

            var children = todos.Where(t => t.ProjectId == p.Id && t.Status != TodoStatus.Discarded && (t.StartDate ?? t.DueDate) is not null)
                .Select(t => (Todo: t, Start: t.StartDate ?? t.DueDate!.Value, End: t.DueDate ?? t.StartDate!.Value))
                .Where(x => x.End >= from && x.Start <= to)
                .OrderBy(x => x.Start).ThenBy(x => x.Todo.Title).Take(MaxTodosPerProject).ToList();

            rows.Add(new GanttRow("p" + p.Id, null, p.Name, string.Join(" · ", new[] { p.OwnerUser?.DisplayName, progress is { } pr ? Math.Round(pr) + " % erledigt" : null }.Where(s => s is not null)),
                $"/Projects/Details?id={p.Id}", bar, marks, children.Count));
            foreach (var (t, s, e) in children)
            {
                var ttone = t.Status == TodoStatus.Done ? "ok" : t.Status == TodoStatus.Blocked || t.IsOverdue(today) ? "error" : t.Status == TodoStatus.InProgress ? "busy" : "plain";
                rows.Add(new GanttRow("t" + t.Id, "p" + p.Id, t.Title, t.OwnerUser?.DisplayName, $"/Todos/Details?id={t.Id}",
                    Bar(s, e < s ? s : e, from, to, Pos, ttone, t.Title, Fmt.Period(t.StartDate, t.DueDate), null), [], 0));
            }
        }

        double? todayLeft = today >= from && today <= to ? Pos(today) : null;
        return new GanttView(from, to, monthTicks, weekTicks, todayLeft, rows, hidden);
    }

    private static GanttBar Bar(DateOnly start, DateOnly end, DateOnly from, DateOnly to, Func<DateOnly, double> pos, string tone, string label, string dates, double? progress)
    {
        var left = Math.Max(0, pos(start));
        var right = Math.Min(100, pos(end.AddDays(1)));
        return new GanttBar(left, Math.Max(0.8, right - left), tone, start < from, end > to, label, dates, progress);
    }

    public static string Css(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
