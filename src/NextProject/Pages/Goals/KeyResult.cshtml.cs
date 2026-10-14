using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Goals;

/// <summary>A key result: a measurable step towards a goal, with the value it started at, the target, and the measurements (check-ins) in between.</summary>
public sealed class KeyResultModel(NpDbContext db, Lookups lookups, HistoryService history, ICurrentUser me, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Range(1, long.MaxValue, ErrorMessage = "Bitte wähle das Ziel, zu dem das Schlüsselergebnis gehört.")]
        [Display(Name = "Ziel")]
        public long GoalId { get; set; }

        [Display(Name = "Projekt")]
        public long? ProjectId { get; set; }

        [Required(ErrorMessage = "Bitte beschreibe das Schlüsselergebnis.")]
        [Display(Name = "Schlüsselergebnis")]
        public string Title { get; set; } = "";

        [Display(Name = "Verantwortlich")]
        public long? OwnerUserId { get; set; }

        [Display(Name = "Messgröße")]
        public string? Metric { get; set; }

        [Display(Name = "Erhebungsart")]
        public string? Collection { get; set; }

        [Display(Name = "Einheit")]
        public string? Unit { get; set; }

        [Display(Name = "Startwert")]
        public decimal StartValue { get; set; }

        [Display(Name = "Zielwert")]
        public decimal TargetValue { get; set; }

        [Display(Name = "Bis wann")]
        public DateOnly? DueDate { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public KeyResult? Result { get; private set; }

    public List<SelectListItem> Goals { get; private set; } = [];

    public List<SelectListItem> Projects { get; private set; } = [];

    public List<SelectListItem> Users { get; private set; } = [];

    public IHtmlContent? Chart { get; private set; }

    public async Task<IActionResult> OnGetAsync(long? goalId, long? projectId)
    {
        if (Id is { } id)
        {
            Result = await db.KeyResult.AsNoTracking().Include(k => k.CheckIns).FirstOrDefaultAsync(k => k.Id == id);
            if (Result is null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                GoalId = Result.GoalId, ProjectId = Result.ProjectId, Title = Result.Title, OwnerUserId = Result.OwnerUserId, Metric = Result.Metric, Collection = Result.Collection,
                Unit = Result.Unit, StartValue = Result.StartValue, TargetValue = Result.TargetValue, DueDate = Result.DueDate,
            };
        }
        else
        {
            Input.GoalId = goalId ?? 0;
            Input.ProjectId = projectId;
            Input.OwnerUserId = me.UserId;
            if (goalId is null && projectId is { } pid)
            {
                var goals = await db.ProjectGoal.Where(g => g.ProjectId == pid).Select(g => g.GoalId).ToListAsync();
                if (goals.Count == 1)
                {
                    Input.GoalId = goals[0];
                }
            }
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        KeyResult? result = null;
        if (Id is { } id)
        {
            result = await db.KeyResult.FirstOrDefaultAsync(k => k.Id == id);
            if (result is null)
            {
                return NotFound();
            }
        }

        if (Input.StartValue == Input.TargetValue)
        {
            ModelState.AddModelError("Input.TargetValue", "Start- und Zielwert sind gleich: dann gibt es nichts zu erreichen.");
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var isNew = result is null;
        result ??= new KeyResult();
        result.GoalId = Input.GoalId;
        result.ProjectId = Input.ProjectId;
        result.Title = Input.Title.Trim();
        result.OwnerUserId = Input.OwnerUserId;
        result.Metric = Clean(Input.Metric);
        result.Collection = Clean(Input.Collection);
        result.Unit = Clean(Input.Unit);
        result.StartValue = Input.StartValue;
        result.TargetValue = Input.TargetValue;
        result.DueDate = Input.DueDate;
        if (isNew)
        {
            db.KeyResult.Add(result);
        }

        await db.SaveChangesAsync();
        if (isNew)
        {
            history.Add(HistoryEntity.KeyResult, result.Id, HistoryKind.Created, note: result.Title);
            await db.SaveChangesAsync();
        }

        await log.LogAsync(isNew ? "Schlüsselergebnis angelegt" : "Schlüsselergebnis geändert", "KeyResult", result.Id, result.Title);
        Flash("Das Schlüsselergebnis ist gespeichert.");
        return RedirectToPage("/Goals/Details", new { id = result.GoalId, tab = "results" });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await db.KeyResult.FirstOrDefaultAsync(k => k.Id == Id);
        if (result is not null)
        {
            await db.Todo.Where(t => t.KeyResultId == result.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.KeyResultId, (long?)null).SetProperty(t => t.GoalId, result.GoalId));
            history.Add(HistoryEntity.KeyResult, result.Id, HistoryKind.Deleted, note: result.Title);
            db.KeyResult.Remove(result);
            await db.SaveChangesAsync();
            Flash("Das Schlüsselergebnis ist gelöscht. Seine Todos gehören jetzt direkt zum Ziel.");
            return RedirectToPage("/Goals/Details", new { id = result.GoalId, tab = "results" });
        }

        return RedirectToPage("/Goals/Index");
    }

    private async Task PrepareAsync()
    {
        Goals = await lookups.GoalsAsync();
        Projects = await lookups.ProjectsAsync();
        Users = await lookups.UsersAsync(Input.OwnerUserId);
        if (Result is { CheckIns.Count: > 0 })
        {
            Chart = BarChart.Render("keyresult-chart", "Gemessene Werte",
                [.. Result.CheckIns.OrderBy(c => c.CheckInDate).Select(c => new Bar(c.CheckInDate.ToString("dd.MM.", Fmt.Culture), (double)c.Value, $"{Fmt.Date(c.CheckInDate)}: {Fmt.Number(c.Value, 1)} {Result.Unit}"))], Result.Unit ?? "");
        }

        var title = Id is null ? "Neues Schlüsselergebnis" : "Schlüsselergebnis bearbeiten";
        Head(title, null, ("Ziele & OKRs", "/Goals"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Goals";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
