using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;

namespace NextProject.Tests.Web;

public sealed class GanttTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static Project P(long id, string name, DateOnly? start, DateOnly? end, ProjectStatus status = ProjectStatus.InProgress)
        => new() { Id = id, Name = name, StartDate = start, EndDate = end, Status = status };

    [Fact]
    public void The_window_starts_a_month_before_today_and_a_project_inside_it_gets_a_bar_with_its_progress()
    {
        var project = P(1, "Eins", new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30));
        var facts = new Dictionary<long, ProjectFacts> { [1] = new(new TodoCounts(10, 5, 0, 0, 0, 5, 0, 0), null, 0, 0, 0, 0, 0, 0, []) };
        var view = GanttView.Build(Today, 0, 6, [project], [], [], facts);

        Assert.Equal(new DateOnly(2026, 9, 1), view.From);
        Assert.Equal(new DateOnly(2027, 2, 28), view.To);
        Assert.Equal(6, view.Months.Count);
        var row = Assert.Single(view.Rows);
        Assert.Equal("busy", row.Bar!.Tone);
        Assert.Equal(50, row.Bar.Progress);
        Assert.False(row.Bar.CutLeft);
        Assert.True(row.Bar.Left > 0 && row.Bar.Width > 0 && row.Bar.Left + row.Bar.Width <= 100);
        Assert.NotNull(view.TodayLeft);
    }

    [Fact]
    public void A_bar_that_goes_beyond_the_window_is_cut_and_a_project_outside_or_without_dates_is_counted_as_hidden()
    {
        var long1 = P(1, "Lang", new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));
        var past = P(2, "Vorbei", new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1));
        var later = P(3, "Spät", new DateOnly(2028, 1, 1), new DateOnly(2028, 3, 1));
        var none = P(4, "Ohne", null, null);
        var view = GanttView.Build(Today, 0, 3, [long1, past, later, none], [], [], new Dictionary<long, ProjectFacts>());

        var row = Assert.Single(view.Rows);
        Assert.True(row.Bar!.CutLeft);
        Assert.True(row.Bar.CutRight);
        Assert.Equal(0, row.Bar.Left);
        Assert.Equal(100, Math.Round(row.Bar.Width));
        Assert.Equal(3, view.Hidden);
        Assert.NotEmpty(view.Weeks);
    }

    [Fact]
    public void Milestones_sit_on_the_row_of_their_project_and_todos_are_hidden_children()
    {
        var project = P(1, "Eins", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));
        var milestones = new[]
        {
            new Milestone { ProjectId = 1, Title = "A", DueDate = new DateOnly(2026, 10, 5), ReachedDate = new DateOnly(2026, 10, 5) },
            new Milestone { ProjectId = 1, Title = "B", DueDate = new DateOnly(2026, 10, 10) },
            new Milestone { ProjectId = 1, Title = "C", DueDate = new DateOnly(2030, 1, 1) },
        };
        var todos = new[]
        {
            new Todo { Id = 5, ProjectId = 1, Title = "Spät", Status = TodoStatus.Open, DueDate = new DateOnly(2026, 10, 12) },
            new Todo { Id = 6, ProjectId = 1, Title = "Fertig", Status = TodoStatus.Done, StartDate = new DateOnly(2026, 10, 2), DueDate = new DateOnly(2026, 10, 3) },
            new Todo { Id = 7, ProjectId = 1, Title = "Verworfen", Status = TodoStatus.Discarded, DueDate = new DateOnly(2026, 10, 3) },
            new Todo { Id = 8, ProjectId = 1, Title = "Ohne Datum", Status = TodoStatus.Open },
            new Todo { Id = 9, ProjectId = 2, Title = "Anderes Projekt", Status = TodoStatus.Open, DueDate = new DateOnly(2026, 10, 3) },
        };
        var view = GanttView.Build(Today, 0, 6, [project], milestones, todos, new Dictionary<long, ProjectFacts>());

        var head = view.Rows[0];
        Assert.Equal(2, head.Marks.Count);
        Assert.Contains(head.Marks, m => m.Tone == "ok");
        Assert.Contains(head.Marks, m => m.Tone == "error");
        Assert.Equal(2, head.ChildCount);
        var children = view.Rows.Skip(1).ToList();
        Assert.Equal(["Fertig", "Spät"], children.Select(c => c.Label));
        Assert.All(children, c => Assert.Equal("p1", c.ParentKey));
        Assert.Equal("ok", children[0].Bar!.Tone);
        Assert.Equal("error", children[1].Bar!.Tone);
    }

    [Theory]
    [InlineData("quartal", 3)]
    [InlineData("halbjahr", 6)]
    [InlineData("jahr", 12)]
    [InlineData("unsinn", 6)]
    public void The_zoom_names_the_number_of_months(string zoom, int months) => Assert.Equal(months, GanttView.MonthsOf(zoom));
}
