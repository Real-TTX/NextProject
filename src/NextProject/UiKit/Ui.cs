using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace NextProject.UiKit;

/// <summary>The version of this build: "1.0.42-20261007", "nightly-42-20261007" or "local-20261007" (see Directory.Build.props).</summary>
public static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? "local" : informational.Split('+')[0];
    }
}

/// <summary>Small helpers for the markup of the toolkit: icons, avatars, progress bars, status pills.</summary>
public static partial class Ui
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex NameRegex();

    /// <summary>An icon from the page's inline sprite. The name must exist in <c>matui-icons.svg</c> or in the app's own sprite.</summary>
    public static IHtmlContent Icon(string name, string? extraClass = null)
    {
        if (!NameRegex().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not an icon name.", nameof(name));
        }

        var cls = string.IsNullOrEmpty(extraClass) ? "mat-icon" : $"mat-icon {extraClass}";
        return new HtmlString($"<svg class=\"{cls}\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>");
    }

    /// <summary>The logo tile: the two cherries on the accent colour of the app (cherry red).</summary>
    public static IHtmlContent Logo(string size = "md")
        => new HtmlString(
            $"<span class=\"mat-logo mat-logo--{size} np-logo\" role=\"img\" aria-label=\"NextProject\">" +
            "<svg class=\"mat-icon\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-np-logo\"></use></svg></span>");

    /// <summary>The first letters of a name, for an avatar.</summary>
    public static string Initials(string name) => Services.Fmt.Initials(name);

    /// <summary>A status as a filled chip; <paramref name="tone"/> is ok, warn, error, info, busy, accent, plain or empty (a quiet grey one).</summary>
    public static IHtmlContent Pill(string text, string tone = "")
        => new HtmlString($"<span class=\"np-chip np-chip--{(string.IsNullOrEmpty(tone) ? "soft" : tone)}\">{WebUtility.HtmlEncode(text)}</span>");

    /// <summary>
    /// A tile with a round picture and a big number (the numbers of the dashboard, of a project, of a goal). <paramref name="note"/> says what the number means; with
    /// <paramref name="bad"/> it is written in the colour of a warning. A tile with an address is a link.
    /// </summary>
    public static IHtmlContent Kpi(string label, string value, string? note, string icon, string tone = "accent", string? href = null, bool bad = false)
        => KpiOf(label, WebUtility.HtmlEncode(value), "np-kpi__value", note, icon, tone, href, bad);

    /// <summary>The same tile with markup as its value (a status chip, a person).</summary>
    public static IHtmlContent Kpi(string label, IHtmlContent value, string? note, string icon, string tone = "accent", string? href = null, bool bad = false)
        => KpiOf(label, value.ToHtml(), "np-kpi__value np-kpi__value--small", note, icon, tone, href, bad);

    private static IHtmlContent KpiOf(string label, string value, string valueClass, string? note, string icon, string tone, string? href, bool bad)
    {
        var tag = href is null ? "div" : "a";
        var link = href is null ? "" : $" href=\"{WebUtility.HtmlEncode(href)}\"";
        var noteHtml = string.IsNullOrWhiteSpace(note) ? "" : $"<span class=\"np-kpi__note{(bad ? " is-bad" : "")}\">{WebUtility.HtmlEncode(note)}</span>";
        return new HtmlString(
            $"<{tag} class=\"np-kpi np-kpi--{tone}\"{link}><span class=\"np-kpi__icon\">{Icon(icon).ToHtml()}</span>" +
            $"<span class=\"np-kpi__text\"><span class=\"{valueClass}\">{value}</span><span class=\"np-kpi__label\">{WebUtility.HtmlEncode(label)}</span>{noteHtml}</span></{tag}>");
    }

    /// <summary>A share as a ring with its number in the middle; a null share is "not measured". <paramref name="tone"/> is the colour of the ring (accent, ok, busy, error ...).</summary>
    public static IHtmlContent Ring(decimal? share, string label, string tone = "accent", bool large = false)
    {
        if (share is not { } s)
        {
            return new HtmlString($"<span class=\"np-ring{(large ? " np-ring--lg" : "")}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(label)}: nicht gemessen\"><svg viewBox=\"0 0 36 36\" aria-hidden=\"true\"><circle class=\"np-ring__track\" cx=\"18\" cy=\"18\" r=\"15.9155\" stroke-width=\"3.6\"/></svg><span class=\"np-ring__text mat-muted\">–</span></span>");
        }

        var percent = (int)Math.Round(Math.Clamp(s, 0m, 1m) * 100, MidpointRounding.AwayFromZero);
        var color = tone switch
        {
            "ok" => "var(--np-c-ok)",
            "busy" => "var(--np-c-busy)",
            "error" => "var(--np-c-error)",
            "info" => "var(--np-c-info)",
            _ => "var(--mat-accent)",
        };
        return new HtmlString(
            $"<span class=\"np-ring{(large ? " np-ring--lg" : "")}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(label)}: {percent} Prozent\" data-np-tone=\"{WebUtility.HtmlEncode(tone)}\"><svg viewBox=\"0 0 36 36\" aria-hidden=\"true\">" +
            "<circle class=\"np-ring__track\" cx=\"18\" cy=\"18\" r=\"15.9155\" stroke-width=\"3.6\"/>" +
            $"<circle class=\"np-ring__value np-ring__value--{tone}\" cx=\"18\" cy=\"18\" r=\"15.9155\" stroke-width=\"3.6\" stroke-dasharray=\"{percent} 100\"/></svg><span class=\"np-ring__text\">{percent}%</span></span>");
    }

    /// <summary>Encoded text for a table cell; empty for null.</summary>
    public static IHtmlContent Text(string? text) => new HtmlString(WebUtility.HtmlEncode(text ?? string.Empty));

    /// <summary>A link in a table cell.</summary>
    public static IHtmlContent Link(string text, string href)
        => new HtmlString($"<a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(text)}</a>");

    /// <summary>A name with a second, quieter line below it.</summary>
    public static IHtmlContent NameWithSub(string text, string? href, string? sub)
    {
        var name = href is null ? WebUtility.HtmlEncode(text) : $"<a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(text)}</a>";
        var small = string.IsNullOrWhiteSpace(sub) ? "" : $"<small class=\"mat-muted np-sub\">{WebUtility.HtmlEncode(sub)}</small>";
        return new HtmlString($"<div class=\"mat-table__name np-name\"><span>{name}</span>{small}</div>");
    }

    public static IHtmlContent Mono(string? text) => new HtmlString($"<span class=\"mat-mono\">{WebUtility.HtmlEncode(text ?? string.Empty)}</span>");

    /// <summary>Several pieces of markup after each other (a status pill and a second line, for example).</summary>
    public static IHtmlContent Concat(params IHtmlContent[] parts) => new HtmlString(string.Concat(parts.Select(p => p.ToHtml())));

    /// <summary>A progress bar with the value written next to it (never only on it). <paramref name="share"/> is 0 to 1; a null share is "not measured", not 0 percent.</summary>
    public static IHtmlContent Meter(decimal? share, string label, string? tone = null)
    {
        if (share is not { } s)
        {
            return new HtmlString("<span class=\"mat-muted\">nicht gemessen</span>");
        }

        var percent = (int)Math.Round(s * 100, MidpointRounding.AwayFromZero);
        var toneAttr = string.IsNullOrEmpty(tone) ? "" : $" data-tone=\"{WebUtility.HtmlEncode(tone)}\"";
        return new HtmlString(
            $"<div class=\"np-meter\"><div class=\"mat-meter mat-meter--sm\" role=\"progressbar\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-valuenow=\"{percent}\" aria-label=\"{WebUtility.HtmlEncode(label)}\">" +
            $"<div class=\"mat-meter__fill\" data-value=\"{percent}\"{toneAttr}></div></div><span class=\"np-meter__text\">{percent} %</span></div>");
    }

    /// <summary>The round avatar with the initials of a person; the name is the tooltip and the label.</summary>
    public static IHtmlContent Avatar(string? name, string size = "")
    {
        var cls = string.IsNullOrEmpty(size) ? "mat-avatar" : $"mat-avatar mat-avatar--{size}";
        var shown = string.IsNullOrWhiteSpace(name) ? "Niemand" : name;
        return new HtmlString($"<span class=\"{cls}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(shown)}\" data-tip=\"{WebUtility.HtmlEncode(shown)}\">{WebUtility.HtmlEncode(Services.Fmt.Initials(name))}</span>");
    }

    /// <summary>A person with the avatar, for a table cell.</summary>
    public static IHtmlContent Person(string? name)
        => string.IsNullOrWhiteSpace(name)
            ? new HtmlString("<span class=\"mat-muted\">–</span>")
            : new HtmlString($"<span class=\"np-person\">{Avatar(name, "sm").ToHtml()}<span>{WebUtility.HtmlEncode(name)}</span></span>");

    /// <summary>Several people as overlapping avatars (the people of a project).</summary>
    public static IHtmlContent AvatarStack(IEnumerable<string> names, int max = 4)
    {
        var all = names.Distinct().ToList();
        if (all.Count == 0)
        {
            return new HtmlString("<span class=\"mat-muted\">–</span>");
        }

        var sb = new StringBuilder("<span class=\"mat-avatars\">");
        foreach (var name in all.Take(max))
        {
            sb.Append(Avatar(name, "sm").ToHtml());
        }

        if (all.Count > max)
        {
            sb.Append($"<span class=\"mat-avatar mat-avatar--sm\" role=\"img\" aria-label=\"{all.Count - max} weitere\">+{all.Count - max}</span>");
        }

        return new HtmlString(sb.Append("</span>").ToString());
    }

    /// <summary>A due date in a cell: the words ("Heute", "Morgen", the date), with a warning icon in the colour of a warning when it is overdue.</summary>
    public static IHtmlContent DueText(DateOnly? due, DateOnly today, bool overdue)
    {
        if (due is null)
        {
            return new HtmlString("<span class=\"mat-muted\">–</span>");
        }

        var text = WebUtility.HtmlEncode(Services.Fmt.Due(due, today));
        return new HtmlString(overdue ? $"<span class=\"np-overdue\">{Icon("alert").ToHtml()} {text}</span>" : $"<span>{text}</span>");
    }

    private static readonly Dictionary<string, string> Singulars = new(StringComparer.Ordinal)
    {
        ["Einträge"] = "Eintrag", ["Pläne"] = "Plan", ["Ziele"] = "Ziel", ["Todos"] = "Todo", ["Projekte"] = "Projekt", ["Abteilungen"] = "Abteilung",
        ["Reviews"] = "Review", ["Links"] = "Link", ["Meilensteine"] = "Meilenstein", ["Risiken"] = "Risiko", ["Schlüsselergebnisse"] = "Schlüsselergebnis",
        ["Freigaben"] = "Freigabe", ["Mitglieder"] = "Mitglied", ["Mitarbeiter"] = "Mitarbeiter",
    };

    /// <summary>The name of the things in a list for a count: "1 Projekt", "3 Projekte". Names that are not known keep their plural.</summary>
    public static string CountNoun(long count, string plural) => $"{Services.Fmt.Integer(count)} {(count == 1 && Singulars.TryGetValue(plural, out var one) ? one : plural)}";

    /// <summary>A phrase with a count in German: "1 Projekt", "3 Projekte".</summary>
    public static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    /// <summary>The markup of a content as text (a cell that is made of other cells).</summary>
    public static string ToHtml(this IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }
}
