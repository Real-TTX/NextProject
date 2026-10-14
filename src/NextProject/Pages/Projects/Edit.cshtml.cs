using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.UiKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Projects;

/// <summary>Create or change a project: what it is for, who owns it, who works on it, when, and which goals it serves.</summary>
public sealed class EditModel(NpDbContext db, HistoryService history, Lookups lookups, ICurrentUser me, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib dem Projekt einen Namen.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = "";

        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Display(Name = "Zielsetzung")]
        public string? Objective { get; set; }

        [Display(Name = "Erwartetes Ergebnis")]
        public string? ExpectedResult { get; set; }

        [Display(Name = "Mehrwert")]
        public string? AddedValue { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Bitte bestimme einen verantwortlichen Projektowner.")]
        [Display(Name = "Projektowner")]
        public long OwnerUserId { get; set; }

        [Display(Name = "Beteiligte")]
        public string? MemberIds { get; set; }

        [Display(Name = "Abteilung")]
        public long? DepartmentId { get; set; }

        [Display(Name = "Startdatum")]
        public DateOnly? StartDate { get; set; }

        [Display(Name = "Enddatum")]
        public DateOnly? EndDate { get; set; }

        [Display(Name = "Status")]
        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        [Display(Name = "Priorität")]
        public Level Priority { get; set; } = Level.Medium;

        [Display(Name = "Ziele")]
        public string? GoalIds { get; set; }

        [Display(Name = "Hängt ab von")]
        public string? DependsOnIds { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Bitte gib ein Budget ab 0 ein.")]
        [Display(Name = "Kostenaufwand (€)")]
        public decimal? Budget { get; set; }

        [Display(Name = "Ressourcenaufwand")]
        public string? Resources { get; set; }

        [Display(Name = "Einflussfaktoren (+/-)")]
        public string? InfluenceFactors { get; set; }

        [Display(Name = "Stand")]
        public string? StatusNote { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Users { get; private set; } = [];

    public List<SelectListItem> UserChoices { get; private set; } = [];

    public List<SelectListItem> Departments { get; private set; } = [];

    public List<SelectListItem> Goals { get; private set; } = [];

    public List<SelectListItem> ProjectChoices { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long? goalId)
    {
        if (Id is { } id)
        {
            var p = await db.Project.AsNoTracking().Include(x => x.Members).Include(x => x.Goals).Include(x => x.Dependencies).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Name = p.Name,
                Description = p.Description,
                Objective = p.Objective,
                ExpectedResult = p.ExpectedResult,
                AddedValue = p.AddedValue,
                OwnerUserId = p.OwnerUserId,
                MemberIds = IdList.Join(p.Members.Select(m => m.UserId)),
                DepartmentId = p.DepartmentId,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status,
                Priority = p.Priority,
                GoalIds = IdList.Join(p.Goals.Select(g => g.GoalId)),
                DependsOnIds = IdList.Join(p.Dependencies.Select(d => d.DependsOnProjectId)),
                Budget = p.Budget,
                Resources = p.Resources,
                InfluenceFactors = p.InfluenceFactors,
                StatusNote = p.StatusNote,
            };
        }
        else
        {
            Input.OwnerUserId = me.UserId ?? 0;
            Input.GoalIds = goalId?.ToString();
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Project? project = null;
        if (Id is { } id)
        {
            project = await db.Project.Include(x => x.Members).Include(x => x.Goals).Include(x => x.Dependencies).FirstOrDefaultAsync(x => x.Id == id);
            if (project is null)
            {
                return NotFound();
            }
        }

        if (Input.StartDate is { } start && Input.EndDate is { } end && end < start)
        {
            ModelState.AddModelError("Input.EndDate", "Das Projekt kann nicht enden, bevor es beginnt.");
        }

        var dependsOn = IdList.Parse(Input.DependsOnIds);
        if (project is not null && dependsOn.Contains(project.Id))
        {
            ModelState.AddModelError("Input.DependsOnIds", "Ein Projekt kann nicht von sich selbst abhängen.");
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var isNew = project is null;
        var oldStatus = project?.Status;
        var oldOwner = project?.OwnerUserId;
        project ??= new Project();
        project.Name = Input.Name.Trim();
        project.Description = Clean(Input.Description);
        project.Objective = Clean(Input.Objective);
        project.ExpectedResult = Clean(Input.ExpectedResult);
        project.AddedValue = Clean(Input.AddedValue);
        project.OwnerUserId = Input.OwnerUserId;
        project.DepartmentId = Input.DepartmentId;
        project.StartDate = Input.StartDate;
        project.EndDate = Input.EndDate;
        project.Status = Input.Status;
        project.Priority = Input.Priority;
        project.Budget = Input.Budget;
        project.Resources = Clean(Input.Resources);
        project.InfluenceFactors = Clean(Input.InfluenceFactors);
        project.StatusNote = Clean(Input.StatusNote);

        Sync(project.Members, IdList.Parse(Input.MemberIds).Where(m => m != Input.OwnerUserId).ToList(), m => m.UserId, userId => new ProjectMember { UserId = userId }, m => db.ProjectMember.Remove(m));
        Sync(project.Goals, IdList.Parse(Input.GoalIds), g => g.GoalId, goalId => new ProjectGoal { GoalId = goalId }, g => db.ProjectGoal.Remove(g));
        Sync(project.Dependencies, dependsOn.Where(d => d != project.Id).ToList(), d => d.DependsOnProjectId, projectId => new ProjectDependency { DependsOnProjectId = projectId }, d => db.ProjectDependency.Remove(d));

        if (isNew)
        {
            db.Project.Add(project);
        }

        await db.SaveChangesAsync();
        if (isNew)
        {
            history.Add(HistoryEntity.Project, project.Id, HistoryKind.Created, note: project.Name);
        }
        else
        {
            if (oldStatus != project.Status)
            {
                history.Add(HistoryEntity.Project, project.Id, HistoryKind.Status, Labels.Of(oldStatus!.Value), Labels.Of(project.Status));
            }

            if (oldOwner != project.OwnerUserId)
            {
                var names = await db.User.IgnoreQueryFilters().Where(u => u.Id == oldOwner || u.Id == project.OwnerUserId).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
                history.Add(HistoryEntity.Project, project.Id, HistoryKind.Owner, names.GetValueOrDefault(oldOwner!.Value, "?"), names.GetValueOrDefault(project.OwnerUserId, "?"));
            }
        }

        await db.SaveChangesAsync();
        await log.LogAsync(isNew ? "Projekt angelegt" : "Projekt geändert", "Project", project.Id, project.Name);
        Flash(isNew ? "Das Projekt ist angelegt." : "Das Projekt ist gespeichert.");
        return RedirectToPage("/Projects/Details", new { id = project.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var project = await db.Project.FirstOrDefaultAsync(p => p.Id == Id);
        if (project is not null)
        {
            // The todos stay, as stand-alone ones; the key results stay with their goal.
            await db.Todo.Where(t => t.ProjectId == project.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.ProjectId, (long?)null).SetProperty(t => t.MilestoneId, (long?)null));
            await db.KeyResult.Where(k => k.ProjectId == project.Id).ExecuteUpdateAsync(s => s.SetProperty(k => k.ProjectId, (long?)null));
            history.Add(HistoryEntity.Project, project.Id, HistoryKind.Deleted, note: project.Name);
            db.Project.Remove(project);
            await db.SaveChangesAsync();
            await log.LogAsync("Projekt gelöscht", "Project", project.Id, project.Name);
            Flash("Das Projekt ist gelöscht. Seine Todos bleiben als eigenständige Todos erhalten.");
        }

        return RedirectToPage("/Projects/Index");
    }

    /// <summary>Makes a child collection hold exactly the wanted keys: removes what is not wanted, adds what is missing.</summary>
    private static void Sync<T>(List<T> current, List<long> wanted, Func<T, long> key, Func<long, T> create, Action<T> remove)
    {
        foreach (var gone in current.Where(c => !wanted.Contains(key(c))).ToList())
        {
            current.Remove(gone);
            remove(gone);
        }

        foreach (var id in wanted.Where(w => !current.Any(c => key(c) == w)))
        {
            current.Add(create(id));
        }
    }

    private async Task PrepareAsync()
    {
        Users = await lookups.UsersAsync(Input.OwnerUserId);
        UserChoices = await lookups.UserChoicesAsync();
        Departments = await lookups.DepartmentsAsync();
        Goals = await lookups.GoalsAsync();
        ProjectChoices = await lookups.ProjectChoicesAsync(Id);
        var title = Id is null ? "Neues Projekt" : "Projekt bearbeiten";
        Head(title, null, ("Projekte", "/Projects"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Projects";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
