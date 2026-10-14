using System.Globalization;
using NextProject.Data.Entities;
using NextProject.Services;

namespace NextProject.UiKit;

/// <summary>One row of the schedule: a todo as a bar from its start to its due date, or a milestone as a mark on its date. Positions are percent of the whole span.</summary>
public sealed record TimelineRow(string Label, string Href, bool IsMilestone, double Left, double Width, string Tone, string Dates, bool Done);

/// <summary>The schedule of a project: months along the top, a row per milestone and todo, a line for today. The positions are set by the script from <c>data-np-bar</c> (no inline style).</summary>
public sealed record TimelineView(IReadOnlyList<(string Label, double Left)> Months, double? TodayLeft, IReadOnlyList<TimelineRow> Rows, DateOnly From, DateOnly To)
{
    private const int MaxTodos = 60;

    /// <summary>Null when there is nothing with a date to draw.</summary>
    public static TimelineView? Build(Project project, IReadOnlyCollection<Todo> todos, IReadOnlyCollection<Milestone> milestones, DateOnly today)
    {
        var dated = todos.Where(t => t.Status != TodoStatus.Discarded && (t.StartDate is not null || t.DueDate is not null))
            .OrderBy(t => t.StartDate ?? t.DueDate).ThenBy(t => t.Title).Take(MaxTodos).ToList();
        var dates = new List<DateOnly>();
        dates.AddRange(dated.SelectMany(t => new[] { t.StartDate, t.DueDate }).Where(d => d is not null).Select(d => d!.Value));
        dates.AddRange(milestones.Select(m => m.DueDate));
        if (project.StartDate is { } ps) { dates.Add(ps); }
        if (project.EndDate is { } pe) { dates.Add(pe); }
        if (dates.Count == 0)
        {
            return null;
        }

        var from = new DateOnly(dates.Min().Year, dates.Min().Month, 1);
        var last = dates.Max();
        var to = new DateOnly(last.Year, last.Month, 1).AddMonths(1).AddDays(-1);
        double total = to.DayNumber - from.DayNumber + 1;
        double Pos(DateOnly d) => (d.DayNumber - from.DayNumber) / total * 100;

        var months = new List<(string, double)>();
        for (var m = from; m <= to; m = m.AddMonths(1))
        {
            months.Add((m.ToString("MMM yy", Fmt.Culture), Pos(m)));
        }

        var rows = new List<TimelineRow>();
        foreach (var m in milestones.OrderBy(m => m.DueDate))
        {
            var late = m.ReachedDate is null && m.DueDate < today;
            rows.Add(new TimelineRow(m.Title, $"/Projects/Milestone?projectId={project.Id}&id={m.Id}", true, Pos(m.DueDate), 0, m.ReachedDate is not null ? "ok" : late ? "error" : "info", Fmt.Date(m.DueDate), m.ReachedDate is not null));
        }

        foreach (var t in dated)
        {
            var start = t.StartDate ?? t.DueDate!.Value;
            var end = t.DueDate ?? t.StartDate!.Value;
            if (end < start)
            {
                end = start;
            }

            var width = Math.Max(1.2, (end.DayNumber - start.DayNumber + 1) / total * 100);
            var tone = t.Status == TodoStatus.Done ? "ok" : t.Status == TodoStatus.Blocked || t.IsOverdue(today) ? "error" : t.Status == TodoStatus.InProgress ? "info" : "plain";
            rows.Add(new TimelineRow(t.Title, $"/Todos/Details?id={t.Id}", false, Pos(start), width, tone, Fmt.Period(t.StartDate, t.DueDate), t.Status == TodoStatus.Done));
        }

        double? todayLeft = today >= from && today <= to ? Pos(today) : null;
        return new TimelineView(months, todayLeft, rows, from, to);
    }

    public static string Css(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
