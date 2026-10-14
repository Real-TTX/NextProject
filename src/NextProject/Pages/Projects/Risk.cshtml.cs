using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Projects;

/// <summary>A risk of a project: what could go wrong, how bad it would be, what is done about it and who looks after it.</summary>
public sealed class RiskModel(NpDbContext db, Lookups lookups, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte benenne das Risiko.")]
        [Display(Name = "Risiko")]
        public string Title { get; set; } = "";

        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Display(Name = "Schwere")]
        public Level Severity { get; set; } = Level.Medium;

        [Display(Name = "Status")]
        public RiskStatus Status { get; set; } = RiskStatus.Open;

        [Display(Name = "Gegenmaßnahme")]
        public string? Mitigation { get; set; }

        [Display(Name = "Verantwortlich")]
        public long? OwnerUserId { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ProjectName { get; private set; } = "";

    public List<SelectListItem> Users { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadProjectAsync())
        {
            return NotFound();
        }

        if (Id is { } id)
        {
            var r = await db.Risk.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == ProjectId);
            if (r is null)
            {
                return NotFound();
            }

            Input = new InputModel { Title = r.Title, Description = r.Description, Severity = r.Severity, Status = r.Status, Mitigation = r.Mitigation, OwnerUserId = r.OwnerUserId };
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadProjectAsync())
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var risk = Id is { } id ? await db.Risk.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == ProjectId) : new Risk { ProjectId = ProjectId };
        if (risk is null)
        {
            return NotFound();
        }

        risk.Title = Input.Title.Trim();
        risk.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        risk.Severity = Input.Severity;
        risk.Status = Input.Status;
        risk.Mitigation = string.IsNullOrWhiteSpace(Input.Mitigation) ? null : Input.Mitigation.Trim();
        risk.OwnerUserId = Input.OwnerUserId;
        if (Id is null)
        {
            db.Risk.Add(risk);
        }

        await db.SaveChangesAsync();
        await log.LogAsync(Id is null ? "Risiko angelegt" : "Risiko geändert", "Project", ProjectId, risk.Title);
        Flash("Das Risiko ist gespeichert.");
        return RedirectToPage("/Projects/Details", new { id = ProjectId, tab = "risks" });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var risk = await db.Risk.FirstOrDefaultAsync(r => r.Id == Id && r.ProjectId == ProjectId);
        if (risk is not null)
        {
            db.Risk.Remove(risk);
            await db.SaveChangesAsync();
            Flash("Das Risiko ist gelöscht.");
        }

        return RedirectToPage("/Projects/Details", new { id = ProjectId, tab = "risks" });
    }

    private async Task<bool> LoadProjectAsync()
    {
        var name = await db.Project.AsNoTracking().Where(p => p.Id == ProjectId).Select(p => p.Name).FirstOrDefaultAsync();
        ProjectName = name ?? "";
        return name is not null;
    }

    private async Task PrepareAsync()
    {
        Users = await lookups.UsersAsync(Input.OwnerUserId);
        var title = Id is null ? "Neues Risiko" : "Risiko bearbeiten";
        Head(title, null, ("Projekte", "/Projects"), (ProjectName, $"/Projects/Details?id={ProjectId}&tab=risks"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Projects";
    }
}
