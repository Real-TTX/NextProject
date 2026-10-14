using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Reviews;

/// <summary>A review of a project or a goal, in the four questions of the review sheet: what deviates and why, what we learn, where we adjust, how we steer.</summary>
public sealed class EditModel(NpDbContext db, Lookups lookups, PlanningService planning, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Display(Name = "Bezug")]
        public string Kind { get; set; } = "projekt";

        [Display(Name = "Projekt")]
        public long? ProjectId { get; set; }

        [Display(Name = "Ziel")]
        public long? GoalId { get; set; }

        [Required(ErrorMessage = "Bitte gib das Datum des Reviews an.")]
        [Display(Name = "Datum")]
        public DateOnly? ReviewDate { get; set; }

        [Display(Name = "Einschätzung")]
        public ReviewHealth Health { get; set; } = ReviewHealth.OnTrack;

        [Display(Name = "Umsetzung gegen Strategie: Wo liegt die Ursache von Abweichungen?")]
        public string? Deviation { get; set; }

        [Display(Name = "Lernschleifen: Was lernen wir aus der Umsetzung?")]
        public string? Learnings { get; set; }

        [Display(Name = "Nachschärfung: Wo müssen wir justieren?")]
        public string? Adjustments { get; set; }

        [Display(Name = "Strategische Steuerung: Wie bleibt die Strategie lebendig?")]
        public string? Steering { get; set; }

        [Display(Name = "Nächstes Review")]
        public DateOnly? NextReviewDate { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Projects { get; private set; } = [];

    public List<SelectListItem> Goals { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long? projectId, long? goalId)
    {
        if (Id is { } id)
        {
            var r = await db.Review.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (r is null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Kind = r.GoalId is not null ? "ziel" : "projekt", ProjectId = r.ProjectId, GoalId = r.GoalId, ReviewDate = r.ReviewDate, Health = r.Health,
                Deviation = r.Deviation, Learnings = r.Learnings, Adjustments = r.Adjustments, Steering = r.Steering, NextReviewDate = r.NextReviewDate,
            };
        }
        else
        {
            Input.ReviewDate = planning.Today;
            Input.ProjectId = projectId;
            Input.GoalId = goalId;
            Input.Kind = goalId is not null ? "ziel" : "projekt";
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Review? review = null;
        if (Id is { } id)
        {
            review = await db.Review.FirstOrDefaultAsync(x => x.Id == id);
            if (review is null)
            {
                return NotFound();
            }
        }

        var forGoal = Input.Kind == "ziel";
        if (forGoal ? Input.GoalId is null : Input.ProjectId is null)
        {
            ModelState.AddModelError(forGoal ? "Input.GoalId" : "Input.ProjectId", forGoal ? "Bitte wähle das Ziel." : "Bitte wähle das Projekt.");
        }

        if (Input.NextReviewDate is { } next && Input.ReviewDate is { } date && next <= date)
        {
            ModelState.AddModelError("Input.NextReviewDate", "Das nächste Review liegt nach diesem.");
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var isNew = review is null;
        review ??= new Review();
        review.ProjectId = forGoal ? null : Input.ProjectId;
        review.GoalId = forGoal ? Input.GoalId : null;
        review.ReviewDate = Input.ReviewDate!.Value;
        review.Health = Input.Health;
        review.Deviation = Clean(Input.Deviation);
        review.Learnings = Clean(Input.Learnings);
        review.Adjustments = Clean(Input.Adjustments);
        review.Steering = Clean(Input.Steering);
        review.NextReviewDate = Input.NextReviewDate;
        if (isNew)
        {
            db.Review.Add(review);
        }

        await db.SaveChangesAsync();
        await log.LogAsync(isNew ? "Review angelegt" : "Review geändert", "Review", review.Id);
        Flash("Das Review ist gespeichert.");
        return RedirectToPage("/Reviews/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var review = await db.Review.FirstOrDefaultAsync(r => r.Id == Id);
        if (review is not null)
        {
            db.Review.Remove(review);
            await db.SaveChangesAsync();
            Flash("Das Review ist gelöscht.");
        }

        return RedirectToPage("/Reviews/Index");
    }

    private async Task PrepareAsync()
    {
        Projects = await lookups.ProjectsAsync(alsoInclude: Input.ProjectId);
        Goals = await lookups.GoalsAsync();
        var title = Id is null ? "Neues Review" : "Review bearbeiten";
        Head(title, null, ("Reviews", "/Reviews"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Reviews";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
