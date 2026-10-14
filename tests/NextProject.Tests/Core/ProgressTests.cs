using NextProject.Core.Planning;

namespace NextProject.Tests.Core;

public sealed class ProgressTests
{
    [Theory]
    [InlineData(0, 100, 0, 0.0)]
    [InlineData(0, 100, 50, 0.5)]
    [InlineData(0, 100, 100, 1.0)]
    [InlineData(0, 100, 130, 1.0)]
    [InlineData(0, 100, -20, 0.0)]
    [InlineData(40, 60, 50, 0.5)]
    public void The_attainment_is_the_share_of_the_way_from_start_to_target(int start, int target, int current, double expected)
        => Assert.Equal((decimal)expected, Progress.Attainment(start, target, current));

    [Theory]
    [InlineData(50, 25, 50, 0.0)]
    [InlineData(50, 25, 36, 0.56)]
    [InlineData(50, 25, 25, 1.0)]
    [InlineData(50, 25, 10, 1.0)]
    [InlineData(50, 25, 60, 0.0)]
    public void A_value_that_has_to_go_down_works_the_same_way(int start, int target, int current, double expected)
        => Assert.Equal((decimal)expected, Progress.Attainment(start, target, current));

    [Fact]
    public void Start_and_target_that_are_equal_are_reached_only_by_that_value()
    {
        Assert.Equal(1m, Progress.Attainment(5, 5, 5));
        Assert.Equal(0m, Progress.Attainment(5, 5, 4));
    }

    [Theory]
    [InlineData(0, 0, 0, 0.0)]
    [InlineData(3, 0, 6, 0.5)]
    [InlineData(3, 2, 8, 0.5)]
    [InlineData(6, 0, 6, 1.0)]
    [InlineData(0, 4, 4, 0.0)]
    public void The_completion_counts_finished_todos_and_leaves_out_discarded_ones(int done, int discarded, int total, double expected)
        => Assert.Equal((decimal)expected, Progress.Completion(done, discarded, total));

    [Fact]
    public void A_goal_without_measured_results_is_not_measured_rather_than_zero_percent()
    {
        Assert.Null(Progress.Average([]));
        Assert.Equal(0.5m, Progress.Average([0m, 1m]));
    }
}
