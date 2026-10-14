using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Goals;

/// <summary>A measurement of a key result: the value on a day. The share of the linked todos that were done at that time is written down with it, so the two can be compared later.</summary>
public sealed class CheckInModel(NpDbContext db, PlanningService planning, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib das Datum der Messung an.")]
        [Display(Name = "Datum")]
        public DateOnly? CheckInDate { get; set; }

        [Display(Name = "Gemessener Wert")]
        public decimal Value { get; set; }

        [Display(Name = "Kommentar")]
        public string? Comment { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long KeyResultId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public KeyResult Result { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
        {
            return NotFound();
        }

        if (Id is { } id)
        {
            var c = await db.KeyResultCheckIn.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.KeyResultId == KeyResultId);
            if (c is null)
            {
                return NotFound();
            }

            Input = new InputModel { CheckInDate = c.CheckInDate, Value = c.Value, Comment = c.Comment };
        }
        else
        {
            Input.CheckInDate = planning.Today;
            Input.Value = Result.CurrentValue;
        }

        Prepare();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync())
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            Prepare();
            return Page();
        }

        var checkIn = Id is { } id ? await db.KeyResultCheckIn.FirstOrDefaultAsync(c => c.Id == id && c.KeyResultId == KeyResultId) : new KeyResultCheckIn { KeyResultId = KeyResultId };
        if (checkIn is null)
        {
            return NotFound();
        }

        checkIn.CheckInDate = Input.CheckInDate!.Value;
        checkIn.Value = Input.Value;
        checkIn.Comment = string.IsNullOrWhiteSpace(Input.Comment) ? null : Input.Comment.Trim();
        checkIn.DonePercent = await planning.DonePercentAsync(KeyResultId);
        if (Id is null)
        {
            db.KeyResultCheckIn.Add(checkIn);
        }

        await db.SaveChangesAsync();
        await log.LogAsync("Messung eingetragen", "KeyResult", KeyResultId, $"{Result.Title}: {Fmt.Number(checkIn.Value, 1)} {Result.Unit}");
        Flash("Die Messung ist gespeichert.");
        return RedirectToPage("/Goals/KeyResult", new { id = KeyResultId });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var checkIn = await db.KeyResultCheckIn.FirstOrDefaultAsync(c => c.Id == Id && c.KeyResultId == KeyResultId);
        if (checkIn is not null)
        {
            db.KeyResultCheckIn.Remove(checkIn);
            await db.SaveChangesAsync();
            Flash("Die Messung ist gelöscht.");
        }

        return RedirectToPage("/Goals/KeyResult", new { id = KeyResultId });
    }

    private async Task<bool> LoadAsync()
    {
        var result = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).Include(k => k.Goal).FirstOrDefaultAsync(k => k.Id == KeyResultId);
        if (result is null)
        {
            return false;
        }

        Result = result;
        return true;
    }

    private void Prepare()
    {
        var title = Id is null ? "Neue Messung" : "Messung bearbeiten";
        Head(title, null, ("Ziele & OKRs", "/Goals"), (Result.Goal?.Title ?? "Ziel", $"/Goals/Details?id={Result.GoalId}&tab=results"), (Result.Title, $"/Goals/KeyResult?id={KeyResultId}"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Goals";
    }
}
