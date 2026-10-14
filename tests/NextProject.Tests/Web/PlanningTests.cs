using NextProject.Configuration;
using NextProject.Data.Entities;
using NextProject.Services;

namespace NextProject.Tests.Web;

public sealed class PlanningTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static PlanningService Service()
    {
        var dir = Path.Combine(Path.GetTempPath(), "np-plan-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        // Signals() needs the configuration and nothing else; no database is touched.
        return new PlanningService(null!, new ConfigStore(dir), TimeProvider.System);
    }

    [Fact]
    public void Overdue_is_worked_out_from_the_due_date_and_the_status()
    {
        var late = new Todo { DueDate = Today.AddDays(-1), Status = TodoStatus.InProgress };
        Assert.True(late.IsOverdue(Today));
        Assert.False(new Todo { DueDate = Today, Status = TodoStatus.Open }.IsOverdue(Today));
        Assert.False(new Todo { DueDate = Today.AddDays(-9), Status = TodoStatus.Done }.IsOverdue(Today));
        Assert.False(new Todo { DueDate = Today.AddDays(-9), Status = TodoStatus.Discarded }.IsOverdue(Today));
        Assert.False(new Todo { Status = TodoStatus.Open }.IsOverdue(Today));
    }

    [Fact]
    public void Todos_are_counted_by_status_and_overdue_ones_are_not_the_closed_ones()
    {
        var counts = PlanningService.Count(
        [
            (TodoStatus.Open, Today.AddDays(-2)), (TodoStatus.InProgress, null), (TodoStatus.Blocked, Today.AddDays(-1)), (TodoStatus.InReview, Today),
            (TodoStatus.Done, Today.AddDays(-30)), (TodoStatus.Done, null), (TodoStatus.Discarded, Today.AddDays(-5)),
        ], Today);
        Assert.Equal(7, counts.Total);
        Assert.Equal(2, counts.Overdue);
        Assert.Equal(1, counts.Blocked);
        Assert.Equal(4, counts.Pending);
        Assert.Equal(2m / 6m, counts.Completion);
    }

    [Fact]
    public void A_project_with_a_blocked_and_an_overdue_todo_gets_signals_and_a_closed_one_gets_none()
    {
        var planning = Service();
        var project = new Project { Status = ProjectStatus.InProgress };
        var signals = planning.Signals(project, new TodoCounts(4, 1, 1, 1, 0, 1, 0, 1), 0, 0, Today);
        Assert.Equal(2, signals.Count);
        Assert.All(signals, s => Assert.Equal("warn", s.Tone));

        Assert.Empty(planning.Signals(new Project { Status = ProjectStatus.Completed }, new TodoCounts(4, 1, 1, 1, 0, 1, 0, 3), 2, 1, Today));
        Assert.Empty(planning.Signals(new Project { Status = ProjectStatus.Paused }, new TodoCounts(4, 1, 1, 1, 0, 1, 0, 3), 2, 1, Today));
    }

    [Fact]
    public void Many_overdue_todos_a_passed_end_date_and_a_late_pace_make_it_an_error_or_a_warning()
    {
        var planning = Service();
        var running = new Project { Status = ProjectStatus.InProgress };
        Assert.Contains(planning.Signals(running, new TodoCounts(10, 5, 0, 0, 0, 2, 0, 3), 0, 0, Today), s => s.Tone == "error");

        var ended = new Project { Status = ProjectStatus.InProgress, EndDate = Today.AddDays(-1) };
        Assert.Contains(planning.Signals(ended, TodoCounts.None, 0, 0, Today), s => s.Tone == "error" && s.Text.Contains("Enddatum"));

        var slow = new Project { Status = ProjectStatus.InProgress, StartDate = Today.AddDays(-80), EndDate = Today.AddDays(20) };
        var pace = planning.Signals(slow, new TodoCounts(10, 8, 0, 0, 0, 1, 0, 0), 0, 0, Today);
        Assert.Contains(pace, s => s.Text.Contains("hinter der Zeit"));

        var fine = planning.Signals(slow, new TodoCounts(10, 1, 0, 0, 0, 9, 0, 0), 0, 0, Today);
        Assert.Empty(fine);
    }

    [Fact]
    public void A_key_result_follows_its_latest_measurement()
    {
        var result = new KeyResult { StartValue = 50, TargetValue = 25 };
        Assert.Equal(0m, result.Attainment);
        result.CheckIns.Add(new KeyResultCheckIn { CheckInDate = Today.AddDays(-30), Value = 41 });
        result.CheckIns.Add(new KeyResultCheckIn { CheckInDate = Today, Value = 36 });
        Assert.Equal(36m, result.CurrentValue);
        Assert.Equal(0.56m, result.Attainment);
    }
}
