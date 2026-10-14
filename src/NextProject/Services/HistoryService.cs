using NextProject.Data;
using NextProject.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>Writes the history of todos, projects and goals (who changed what, when) and reads it back. The caller saves: an entry belongs to the same save as the change.</summary>
public sealed class HistoryService(NpDbContext db, ICurrentUser user, TimeProvider clock)
{
    public void Add(HistoryEntity type, long id, HistoryKind kind, string? oldValue = null, string? newValue = null, string? note = null)
        => db.HistoryEntry.Add(new HistoryEntry
        {
            EntityType = type,
            EntityId = id,
            OccurredDate = clock.GetUtcNow().UtcDateTime,
            UserId = user.UserId,
            UserName = user.Name,
            Kind = kind,
            OldValue = oldValue,
            NewValue = newValue,
            Note = note is { Length: > 1000 } ? note[..1000] : note,
        });

    public Task<List<HistoryEntry>> ForAsync(HistoryEntity type, long id, int take = 100, CancellationToken ct = default)
        => db.HistoryEntry.AsNoTracking().Where(h => h.EntityType == type && h.EntityId == id).OrderByDescending(h => h.OccurredDate).ThenByDescending(h => h.Id).Take(take).ToListAsync(ct);

    /// <summary>The history entry as a sentence ("Status: Offen → In Arbeit").</summary>
    public static string Describe(HistoryEntry h) => h.Kind switch
    {
        HistoryKind.Created => "angelegt",
        HistoryKind.Status => $"Status: {h.OldValue} → {h.NewValue}",
        HistoryKind.Owner => $"Verantwortlich: {h.OldValue} → {h.NewValue}",
        HistoryKind.DueDate => $"Fälligkeit: {Or(h.OldValue)} → {Or(h.NewValue)}",
        HistoryKind.Priority => $"Priorität: {h.OldValue} → {h.NewValue}",
        HistoryKind.Comment => "kommentiert",
        HistoryKind.Deleted => "gelöscht",
        _ => h.Note ?? "geändert",
    };

    private static string Or(string? value) => string.IsNullOrEmpty(value) ? "keine" : value;
}
