using NextProject.Data.Entities;
using NextProject.Services;

namespace NextProject.UiKit;

/// <summary>A project as a card: the project and what is worked out about it (null when nothing is known yet).</summary>
public sealed record ProjectCard(Project Project, ProjectFacts? Facts)
{
    /// <summary>The colour of the card: the status of the project (an endangered running project is red).</summary>
    public string Tone => Project.Status switch
    {
        ProjectStatus.Completed => "ok",
        ProjectStatus.AtRisk => "error",
        ProjectStatus.InProgress => Facts?.IsEndangered == true ? "error" : "busy",
        ProjectStatus.Planning => "info",
        _ => "plain",
    };
}

public sealed record ProjectCards(IReadOnlyList<ProjectCard> Items, DateOnly Today);

/// <summary>A goal as a card, with how far it is reached.</summary>
public sealed record GoalCard(Goal Goal, GoalFacts? Facts)
{
    public string Tone => Goal.Status switch
    {
        GoalStatus.Achieved => "ok",
        GoalStatus.Planned => "info",
        GoalStatus.Dropped => "plain",
        _ => Facts?.Attainment switch { null => "busy", >= 0.66m => "ok", >= 0.33m => "busy", _ => "error" },
    };
}

public sealed record GoalCards(IReadOnlyList<GoalCard> Items);

/// <summary>The switch between the cards and the list that a list page can offer. The address of each view keeps what the toolbar holds.</summary>
public sealed record ViewToggle(string Current, string CardsHref, string ListHref)
{
    /// <summary>The two addresses of a list page: the cards are the standard (no parameter), the table is <c>ansicht=liste</c>. What else is in the address stays.</summary>
    public static ViewToggle For(string current, string url)
    {
        var clean = System.Text.RegularExpressions.Regex.Replace(url, "([?&])ansicht=[^&]*&?", "$1").TrimEnd('?', '&');
        return new ViewToggle(current, clean, clean + (clean.Contains('?') ? "&" : "?") + "ansicht=liste");
    }
}
