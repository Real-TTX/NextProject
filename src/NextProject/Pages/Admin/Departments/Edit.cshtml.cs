using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Admin.Departments;

public sealed class EditModel(NpDbContext db, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib der Abteilung einen Namen.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = "";

        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is { } id)
        {
            var d = await db.Department.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (d is null)
            {
                return NotFound();
            }

            Input = new InputModel { Name = d.Name, Description = d.Description };
        }

        Prepare();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var department = Id is { } id ? await db.Department.FirstOrDefaultAsync(d => d.Id == id) : new Department();
        if (department is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid && await db.Department.AnyAsync(d => d.Name == Input.Name.Trim() && d.Id != Id))
        {
            ModelState.AddModelError("Input.Name", "Eine Abteilung mit diesem Namen gibt es schon.");
        }

        if (!ModelState.IsValid)
        {
            Prepare();
            return Page();
        }

        department.Name = Input.Name.Trim();
        department.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        if (Id is null)
        {
            db.Department.Add(department);
        }

        await db.SaveChangesAsync();
        await log.LogAsync(Id is null ? "Abteilung angelegt" : "Abteilung geändert", "Department", department.Id, department.Name);
        Flash("Die Abteilung ist gespeichert.");
        return RedirectToPage("/Admin/Departments/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var department = await db.Department.FirstOrDefaultAsync(d => d.Id == Id);
        if (department is not null)
        {
            await db.User.Where(u => u.DepartmentId == department.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, (long?)null));
            await db.Project.Where(p => p.DepartmentId == department.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.DepartmentId, (long?)null));
            await db.Goal.Where(g => g.DepartmentId == department.Id).ExecuteUpdateAsync(s => s.SetProperty(g => g.DepartmentId, (long?)null));
            db.Department.Remove(department);
            await db.SaveChangesAsync();
            await log.LogAsync("Abteilung gelöscht", "Department", department.Id, department.Name);
            Flash("Die Abteilung ist gelöscht. Wer ihr zugeordnet war, ist jetzt keiner Abteilung zugeordnet.");
        }

        return RedirectToPage("/Admin/Departments/Index");
    }

    private void Prepare()
    {
        var title = Id is null ? "Neue Abteilung" : "Abteilung bearbeiten";
        Head(title, null, ("Einstellungen", null), ("Abteilungen", "/Admin/Departments"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Admin/Departments";
    }
}
