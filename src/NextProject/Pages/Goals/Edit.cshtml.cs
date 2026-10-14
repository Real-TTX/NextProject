using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Goals;

/// <summary>Create or change a goal: what it is, why it matters, who is responsible, and the targets for the year and its quarters.</summary>
public sealed class EditModel(NpDbContext db, HistoryService history, Lookups lookups, ICurrentUser me, ActivityLogger log, TimeProvider clock) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib dem Ziel einen Titel.")]
        [Display(Name = "Titel")]
        public string Title { get; set; } = "";

        [Display(Name = "Bedeutung für das Unternehmen")]
        public string? Description { get; set; }

        [Display(Name = "Verantwortlich")]
        public long? OwnerUserId { get; set; }

        [Display(Name = "Betrifft Bereich")]
        public long? DepartmentId { get; set; }

        [Range(2000, 2100, ErrorMessage = "Bitte gib ein Jahr ein.")]
        [Display(Name = "Jahr")]
        public int Year { get; set; }

        [Display(Name = "Status")]
        public GoalStatus Status { get; set; } = GoalStatus.Active;

        [Display(Name = "Jahresziel")]
        public string? YearTarget { get; set; }

        [Display(Name = "Quartalsziel 1")]
        public string? Q1Target { get; set; }

        [Display(Name = "Quartalsziel 2")]
        public string? Q2Target { get; set; }

        [Display(Name = "Quartalsziel 3")]
        public string? Q3Target { get; set; }

        [Display(Name = "Quartalsziel 4")]
        public string? Q4Target { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Users { get; private set; } = [];

    public List<SelectListItem> Departments { get; private set; } = [];

    public int Year => Input.Year;

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is { } id)
        {
            var g = await db.Goal.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (g is null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Title = g.Title, Description = g.Description, OwnerUserId = g.OwnerUserId, DepartmentId = g.DepartmentId, Year = g.Year, Status = g.Status,
                YearTarget = g.YearTarget, Q1Target = g.Q1Target, Q2Target = g.Q2Target, Q3Target = g.Q3Target, Q4Target = g.Q4Target,
            };
        }
        else
        {
            Input.OwnerUserId = me.UserId;
            Input.Year = Fmt.Today(clock).Year;
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Goal? goal = null;
        if (Id is { } id)
        {
            goal = await db.Goal.FirstOrDefaultAsync(x => x.Id == id);
            if (goal is null)
            {
                return NotFound();
            }
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var isNew = goal is null;
        var oldStatus = goal?.Status;
        goal ??= new Goal();
        goal.Title = Input.Title.Trim();
        goal.Description = Clean(Input.Description);
        goal.OwnerUserId = Input.OwnerUserId;
        goal.DepartmentId = Input.DepartmentId;
        goal.Year = Input.Year;
        goal.Status = Input.Status;
        goal.YearTarget = Clean(Input.YearTarget);
        goal.Q1Target = Clean(Input.Q1Target);
        goal.Q2Target = Clean(Input.Q2Target);
        goal.Q3Target = Clean(Input.Q3Target);
        goal.Q4Target = Clean(Input.Q4Target);
        if (isNew)
        {
            db.Goal.Add(goal);
        }

        await db.SaveChangesAsync();
        if (isNew)
        {
            history.Add(HistoryEntity.Goal, goal.Id, HistoryKind.Created, note: goal.Title);
        }
        else if (oldStatus != goal.Status)
        {
            history.Add(HistoryEntity.Goal, goal.Id, HistoryKind.Status, Labels.Of(oldStatus!.Value), Labels.Of(goal.Status));
        }

        await db.SaveChangesAsync();
        await log.LogAsync(isNew ? "Ziel angelegt" : "Ziel geändert", "Goal", goal.Id, goal.Title);
        Flash(isNew ? "Das Ziel ist angelegt. Als Nächstes: woran misst du es?" : "Das Ziel ist gespeichert.");
        return RedirectToPage("/Goals/Details", new { id = goal.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var goal = await db.Goal.FirstOrDefaultAsync(g => g.Id == Id);
        if (goal is not null)
        {
            await db.Todo.Where(t => t.GoalId == goal.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.GoalId, (long?)null));
            var results = await db.KeyResult.Where(k => k.GoalId == goal.Id).ToListAsync();
            var resultIds = results.Select(k => k.Id).ToList();
            await db.Todo.Where(t => t.KeyResultId != null && resultIds.Contains(t.KeyResultId.Value)).ExecuteUpdateAsync(s => s.SetProperty(t => t.KeyResultId, (long?)null));
            db.KeyResult.RemoveRange(results);
            history.Add(HistoryEntity.Goal, goal.Id, HistoryKind.Deleted, note: goal.Title);
            db.Goal.Remove(goal);
            await db.SaveChangesAsync();
            await log.LogAsync("Ziel gelöscht", "Goal", goal.Id, goal.Title);
            Flash("Das Ziel ist gelöscht, mit seinen Schlüsselergebnissen. Projekte und Todos bleiben erhalten.");
        }

        return RedirectToPage("/Goals/Index");
    }

    private async Task PrepareAsync()
    {
        Users = await lookups.UsersAsync(Input.OwnerUserId);
        Departments = await lookups.DepartmentsAsync();
        var title = Id is null ? "Neues Ziel" : "Ziel bearbeiten";
        Head(title, null, ("Ziele & OKRs", "/Goals"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Goals";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
