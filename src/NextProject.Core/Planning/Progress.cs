namespace NextProject.Core.Planning;

/// <summary>The rules by which progress is worked out, in one place and with tests: what a page shows must be what these functions say.</summary>
public static class Progress
{
    /// <summary>0 to 1: the share of the way from <paramref name="start"/> to <paramref name="target"/> that <paramref name="current"/> has covered.</summary>
    public static decimal Attainment(decimal start, decimal target, decimal current)
    {
        if (target == start)
        {
            return current == target ? 1m : 0m;
        }

        return Math.Clamp((current - start) / (target - start), 0m, 1m);
    }

    /// <summary>0 to 1: finished todos of all todos that still count (discarded ones do not). No todo: 0.</summary>
    public static decimal Completion(int done, int discarded, int total)
    {
        var counted = total - discarded;
        return counted <= 0 ? 0m : Math.Clamp(done / (decimal)counted, 0m, 1m);
    }

    /// <summary>The average of several attainments; null when there is nothing to average (a goal without a key result is not "0 percent reached", it is not measured).</summary>
    public static decimal? Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}
