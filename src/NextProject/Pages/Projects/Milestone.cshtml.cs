using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Projects;

/// <summary>A milestone of a project. It is reached when somebody says so (and when): the todos that belong to it are a hint, not the proof.</summary>
public sealed class MilestoneModel(NpDbContext db, ActivityLogger log, PlanningService planning) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib dem Meilenstein einen Titel.")]
        [Display(Name = "Titel")]
        public string Title { get; set; } = "";

        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Bitte lege fest, wann der Meilenstein erreicht sein soll.")]
        [Display(Name = "Fällig am")]
        public DateOnly? DueDate { get; set; }

        [Display(Name = "Erreicht am")]
        public DateOnly? ReachedDate { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ProjectName { get; private set; } = "";

    public int OpenTodos { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadProjectAsync())
        {
            return NotFound();
        }

        if (Id is { } id)
        {
            var m = await db.Milestone.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == ProjectId);
            if (m is null)
            {
                return NotFound();
            }

            Input = new InputModel { Title = m.Title, Description = m.Description, DueDate = m.DueDate, ReachedDate = m.ReachedDate };
            OpenTodos = await db.Todo.CountAsync(t => t.MilestoneId == id && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded);
        }

        Prepare();
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
            Prepare();
            return Page();
        }

        var milestone = Id is { } id ? await db.Milestone.FirstOrDefaultAsync(m => m.Id == id && m.ProjectId == ProjectId) : new Milestone { ProjectId = ProjectId };
        if (milestone is null)
        {
            return NotFound();
        }

        milestone.Title = Input.Title.Trim();
        milestone.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        milestone.DueDate = Input.DueDate!.Value;
        milestone.ReachedDate = Input.ReachedDate;
        if (Id is null)
        {
            db.Milestone.Add(milestone);
        }

        await db.SaveChangesAsync();
        await log.LogAsync(Id is null ? "Meilenstein angelegt" : "Meilenstein geändert", "Project", ProjectId, milestone.Title);
        Flash("Der Meilenstein ist gespeichert.");
        return RedirectToPage("/Projects/Details", new { id = ProjectId, tab = "milestones" });
    }

    /// <summary>The check in the list of milestones: reached today, or taken back.</summary>
    public async Task<IActionResult> OnPostReachAsync(bool reached)
    {
        var milestone = await db.Milestone.FirstOrDefaultAsync(m => m.Id == Id && m.ProjectId == ProjectId);
        if (milestone is not null)
        {
            milestone.ReachedDate = reached ? planning.Today : null;
            await db.SaveChangesAsync();
            Flash(reached ? "Der Meilenstein ist erreicht." : "Der Meilenstein ist wieder offen.");
        }

        return RedirectToPage("/Projects/Details", new { id = ProjectId, tab = "milestones" });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var milestone = await db.Milestone.FirstOrDefaultAsync(m => m.Id == Id && m.ProjectId == ProjectId);
        if (milestone is not null)
        {
            db.Milestone.Remove(milestone);
            await db.SaveChangesAsync();
            Flash("Der Meilenstein ist gelöscht. Seine Todos bleiben im Projekt.");
        }

        return RedirectToPage("/Projects/Details", new { id = ProjectId, tab = "milestones" });
    }

    private async Task<bool> LoadProjectAsync()
    {
        var name = await db.Project.AsNoTracking().Where(p => p.Id == ProjectId).Select(p => p.Name).FirstOrDefaultAsync();
        ProjectName = name ?? "";
        return name is not null;
    }

    private void Prepare()
    {
        var title = Id is null ? "Neuer Meilenstein" : "Meilenstein bearbeiten";
        Head(title, null, ("Projekte", "/Projects"), (ProjectName, $"/Projects/Details?id={ProjectId}&tab=milestones"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Projects";
    }
}
