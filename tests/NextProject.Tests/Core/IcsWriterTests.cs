using System.Text;
using NextProject.Core.Planning;

namespace NextProject.Tests.Core;

public sealed class IcsWriterTests
{
    private static readonly DateTime Stamp = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_all_day_entry_ends_the_day_after_its_last_day()
    {
        var text = IcsWriter.Write("Meine Termine", [new IcsEvent("t1@np", "Angebot", new DateOnly(2026, 10, 20))], Stamp);
        Assert.Contains("DTSTART;VALUE=DATE:20261020\r\n", text);
        Assert.Contains("DTEND;VALUE=DATE:20261021\r\n", text);

        var range = IcsWriter.Write("x", [new IcsEvent("t2@np", "Phase", new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 2))], Stamp);
        Assert.Contains("DTEND;VALUE=DATE:20261103\r\n", range);
    }

    [Fact]
    public void The_file_has_the_frame_crlf_line_ends_and_a_stamp()
    {
        var text = IcsWriter.Write("Kalender", [new IcsEvent("a", "b", new DateOnly(2026, 1, 1))], Stamp);
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", text);
        Assert.EndsWith("END:VEVENT\r\nEND:VCALENDAR\r\n", text);
        Assert.Contains("DTSTAMP:20261014T120000Z\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }

    [Fact]
    public void Special_characters_are_escaped_so_a_title_cannot_break_the_file()
    {
        var text = IcsWriter.Write("Name; mit, Zeichen", [new IcsEvent("u", "Rechnung, Mahnung; Zweite\nZeile \\ Ende", new DateOnly(2026, 1, 1), Description: "Zeile 1\r\nZeile 2")], Stamp);
        Assert.Contains("X-WR-CALNAME:Name\\; mit\\, Zeichen\r\n", text);
        Assert.Contains("SUMMARY:Rechnung\\, Mahnung\\; Zweite\\nZeile \\\\ Ende\r\n", text);
        Assert.Contains("DESCRIPTION:Zeile 1\\nZeile 2\r\n", text);
        // an injected line stays part of the text
        var injected = IcsWriter.Write("x", [new IcsEvent("u", "Termin\r\nEND:VCALENDAR", new DateOnly(2026, 1, 1))], Stamp);
        Assert.Single(injected.Split("\r\n"), l => l == "END:VCALENDAR");
    }

    [Fact]
    public void Long_lines_are_folded_at_75_bytes_and_no_letter_is_cut()
    {
        var title = string.Concat(Enumerable.Repeat("Übergabe der Schnittstelle an die Lieferanten – ", 6));
        var text = IcsWriter.Write("x", [new IcsEvent("u", title, new DateOnly(2026, 1, 1))], Stamp);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 75, l));
        Assert.Contains(lines, l => l.StartsWith(' '));
        // unfolded it is the title again
        var unfolded = text.Replace("\r\n ", "");
        Assert.Contains("SUMMARY:" + title.Replace("\\", "\\\\"), unfolded);
    }

    [Fact]
    public void A_finished_entry_is_confirmed_and_an_open_one_is_tentative()
    {
        var text = IcsWriter.Write("x", [new IcsEvent("1", "offen", new DateOnly(2026, 1, 1)), new IcsEvent("2", "fertig", new DateOnly(2026, 1, 1), Done: true)], Stamp);
        Assert.Contains("STATUS:TENTATIVE", text);
        Assert.Contains("STATUS:CONFIRMED", text);
    }
}
