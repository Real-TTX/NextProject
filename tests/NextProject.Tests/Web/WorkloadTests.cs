using NextProject.Services;

namespace NextProject.Tests.Web;

public sealed class WorkloadTests
{
    // Wednesday, 14 October 2026: the week starts on Monday the 12th.
    private static readonly DateOnly Today = new(2026, 10, 14);

    [Theory]
    [InlineData(2026, 10, 12, 2026, 10, 12)]
    [InlineData(2026, 10, 14, 2026, 10, 12)]
    [InlineData(2026, 10, 18, 2026, 10, 12)]
    [InlineData(2026, 10, 19, 2026, 10, 19)]
    public void A_week_starts_on_monday(int y, int m, int d, int ey, int em, int ed)
        => Assert.Equal(new DateOnly(ey, em, ed), Workload.MondayOf(new DateOnly(y, m, d)));

    [Fact]
    public void Todos_count_in_the_week_of_their_due_date_and_an_overdue_one_in_no_week()
    {
        var first = Workload.MondayOf(Today);
        var rows = Workload.Build(
        [
            (1, Today), (1, Today.AddDays(4)), (1, Today.AddDays(5)), (1, Today.AddDays(8)), (1, Today.AddDays(-1)), (1, null), (1, Today.AddDays(70)),
            (2, Today.AddDays(-30)),
        ],
        [(1, "Anna", "Leitung", "Vertrieb"), (2, "Tom", null, null), (3, "Lisa", null, null)], first, 8, Today);

        var anna = rows[0];
        Assert.Equal(1, anna.Overdue);
        Assert.Equal([2, 2, 0, 0, 0, 0, 0, 0], anna.Weeks);
        Assert.Equal(2, anna.Weeks[0]);
        Assert.Equal(2, anna.Weeks[1]);
        Assert.Equal(0, anna.Weeks[2]);
        Assert.Equal(1, anna.NoDate);
        Assert.Equal(1 + 2 + 2 + 1, anna.Total);

        Assert.Equal(1, rows[1].Overdue);
        Assert.Equal(1, rows[1].Total);
        Assert.Equal(0, rows[2].Total);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(40, 3)]
    public void The_level_of_a_week_grows_with_the_number_of_todos(int count, int level) => Assert.Equal(level, Workload.Level(count));
}
