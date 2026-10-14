using System.Net;
using NextProject.Data.Entities;
using Microsoft.AspNetCore.Html;

namespace NextProject.UiKit;

/// <summary>
/// The one table of todos, used by every page that lists them (my todos, a project, a goal, the team). The first column is a box that finishes the todo
/// (<c>data-np-done</c>, the script of the app posts it); the title links to the todo. <paramref name="withProject"/> adds the project column.
/// </summary>
public static class TodoTable
{
    public static TableModel Build(string id, string caption, IEnumerable<Todo> todos, DateOnly today, bool withProject = true, bool withOwner = true)
    {
        var columns = new List<(TableColumn Column, Func<Todo, IHtmlContent> Cell)>
        {
            (new TableColumn("Erledigt"), t => Check(t)),
            (new TableColumn("Todo"), t => Ui.NameWithSub(t.Title, $"/Todos/Details?id={t.Id}", Sub(t, withProject))),
        };
        if (withOwner)
        {
            columns.Add((new TableColumn("Verantwortlich", HideOnPhone: true), t => Ui.Person(t.OwnerUser?.DisplayName)));
        }

        columns.Add((new TableColumn("Status"), t => Labels.Pill(t.Status)));
        columns.Add((new TableColumn("Priorität", HideOnPhone: true), t => Labels.Pill(t.Priority)));
        columns.Add((new TableColumn("Fällig"), t => Ui.DueText(t.DueDate, today, t.IsOverdue(today))));
        if (withProject)
        {
            columns.Add((new TableColumn("Projekt", HideOnPhone: true), t => t.Project is null ? Ui.Text("") : Ui.Link(t.Project.Name, $"/Projects/Details?id={t.Project.Id}")));
        }

        return TableModel.Of(id, caption, todos, columns, t => [new RowAction("edit", "Bearbeiten", $"/Todos/Edit?id={t.Id}")])
            with { EmptyTitle = "Keine Todos", EmptyText = "Hier ist nichts zu tun, oder der Filter ist zu eng.", EmptyIcon = "check-square" };
    }

    private static string? Sub(Todo t, bool withProject)
    {
        var parts = new List<string>();
        if (t.KeyResult is not null)
        {
            parts.Add("Schlüsselergebnis: " + t.KeyResult.Title);
        }
        else if (t.Goal is not null)
        {
            parts.Add("Ziel: " + t.Goal.Title);
        }

        if (!withProject && t.Milestone is not null)
        {
            parts.Add("Meilenstein: " + t.Milestone.Title);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>The box in front of a todo: ticked when it is done, and a click finishes or reopens it. Without the script it is a disabled box (the todo page has the buttons).</summary>
    public static IHtmlContent Check(Todo t)
    {
        var label = WebUtility.HtmlEncode($"„{t.Title}“ erledigen");
        var disabled = t.Status == TodoStatus.Discarded ? " disabled" : "";
        return new HtmlString(
            $"<label class=\"mat-check np-donecheck\"><input type=\"checkbox\" data-np-done=\"{t.Id}\" aria-label=\"{label}\"{(t.Status == TodoStatus.Done ? " checked" : "")}{disabled} />" +
            "<span class=\"mat-check__text mat-sr-only\">erledigt</span></label>");
    }
}
