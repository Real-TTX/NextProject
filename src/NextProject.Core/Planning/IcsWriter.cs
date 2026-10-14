using System.Text;

namespace NextProject.Core.Planning;

/// <summary>One entry of a calendar file: a day or several days (the last day is <see cref="End"/>, inclusive).</summary>
public sealed record IcsEvent(string Uid, string Summary, DateOnly Start, DateOnly? End = null, string? Description = null, string? Url = null, string? Location = null, bool Done = false);

/// <summary>
/// Writes an iCalendar file (RFC 5545) that Outlook, phones and calendar programs can subscribe to. All entries are all-day entries; lines end with CRLF and are folded
/// at 75 bytes, and text is escaped, so that a title with a comma, a semicolon or a line break does not break the file.
/// </summary>
public static class IcsWriter
{
    public static string Write(string calendarName, IEnumerable<IcsEvent> events, DateTime stampUtc)
    {
        var sb = new StringBuilder();
        Line(sb, "BEGIN:VCALENDAR");
        Line(sb, "VERSION:2.0");
        Line(sb, "PRODID:-//NextProject//Kalender//DE");
        Line(sb, "CALSCALE:GREGORIAN");
        Line(sb, "METHOD:PUBLISH");
        Line(sb, "X-WR-CALNAME:" + Escape(calendarName));
        Line(sb, "REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        Line(sb, "X-PUBLISHED-TTL:PT1H");
        var stamp = stampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        foreach (var e in events)
        {
            var last = e.End is { } end && end > e.Start ? end : e.Start;
            Line(sb, "BEGIN:VEVENT");
            Line(sb, "UID:" + Escape(e.Uid));
            Line(sb, "DTSTAMP:" + stamp);
            Line(sb, "DTSTART;VALUE=DATE:" + e.Start.ToString("yyyyMMdd"));
            Line(sb, "DTEND;VALUE=DATE:" + last.AddDays(1).ToString("yyyyMMdd"));
            Line(sb, "SUMMARY:" + Escape(e.Summary));
            if (!string.IsNullOrWhiteSpace(e.Description))
            {
                Line(sb, "DESCRIPTION:" + Escape(e.Description));
            }

            if (!string.IsNullOrWhiteSpace(e.Location))
            {
                Line(sb, "LOCATION:" + Escape(e.Location));
            }

            if (!string.IsNullOrWhiteSpace(e.Url))
            {
                Line(sb, "URL:" + e.Url);
            }

            Line(sb, "TRANSP:TRANSPARENT");
            Line(sb, "STATUS:" + (e.Done ? "CONFIRMED" : "TENTATIVE"));
            Line(sb, "END:VEVENT");
        }

        Line(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>Text of a property: backslash, semicolon, comma and line breaks are written the way the standard says.</summary>
    public static string Escape(string text)
        => text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    /// <summary>One content line, folded: no line is longer than 75 bytes, a continuation starts with one space, and a letter is never cut in the middle.</summary>
    private static void Line(StringBuilder sb, string line)
    {
        const int limit = 75;
        var bytes = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > limit)
            {
                sb.Append("\r\n ");
                bytes = 1;
            }

            sb.Append(rune.ToString());
            bytes += size;
        }

        sb.Append("\r\n");
    }
}
