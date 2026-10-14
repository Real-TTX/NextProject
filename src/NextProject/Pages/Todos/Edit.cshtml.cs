using System.ComponentModel.DataAnnotations;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Todos;

/// <summary>Create or change a todo: what, who, by when, and what it belongs to. Fields that only matter in a status (the reason of a block) appear with it.</summary>
public sealed class EditModel(NpDbContext db, TodoService todos, HistoryService history, Lookups lookups, ICurrentUser me, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib der Aufgabe einen Titel.")]
        [Display(Name = "Titel")]
        public string Title { get; set; } = "";

        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Bitte lege fest, wer verantwortlich ist.")]
        [Display(Name = "Verantwortlich")]
        public long OwnerUserId { get; set; }

        [Display(Name = "Mitwirkende")]
        public string? ContributorIds { get; set; }

        [Display(Name = "Status")]
        public TodoStatus Status { get; set; } = TodoStatus.Open;

        [Display(Name = "Priorität")]
        public Level Priority { get; set; } = Level.Medium;

        [Display(Name = "Startdatum")]
        public DateOnly? StartDate { get; set; }

        [Display(Name = "Fälligkeitsdatum")]
        public DateOnly? DueDate { get; set; }

        [Display(Name = "Projekt")]
        public long? ProjectId { get; set; }

        [Display(Name = "Meilenstein")]
        public long? MilestoneId { get; set; }

        [Display(Name = "Schlüsselergebnis")]
        public long? KeyResultId { get; set; }

        [Display(Name = "Ziel")]
        public long? GoalId { get; set; }

        [Display(Name = "Prüfer")]
        public long? ReviewerUserId { get; set; }

        [Display(Name = "Was blockiert die Aufgabe?")]
        public string? BlockReason { get; set; }

        [Display(Name = "Was wird zum Weitermachen gebraucht?")]
        public string? BlockNeeds { get; set; }

        [Display(Name = "Ergebnis")]
        public string? Result { get; set; }

        [Display(Name = "Checkliste")]
        public string? Checklist { get; set; }

        [Display(Name = "Wartet auf")]
        public string? DependsOnIds { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Where to go after saving: the page the person came from (a project, the board), if it is one of ours.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public List<SelectListItem> Users { get; private set; } = [];

    public List<SelectListItem> UserChoices { get; private set; } = [];

    public List<SelectListItem> Projects { get; private set; } = [];

    public List<SelectListItem> Goals { get; private set; } = [];

    public List<SelectListItem> KeyResults { get; private set; } = [];

    public List<SelectListItem> Milestones { get; private set; } = [];

    public List<SelectListItem> TodoChoices { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long? projectId, long? keyResultId, long? goalId, DateOnly? due)
    {
        if (Id is { } id)
        {
            var t = await db.Todo.AsNoTracking().Include(x => x.Contributors).Include(x => x.Checklist).Include(x => x.Dependencies).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Title = t.Title,
                Description = t.Description,
                OwnerUserId = t.OwnerUserId,
                ContributorIds = IdList.Join(t.Contributors.Select(c => c.UserId)),
                Status = t.Status,
                Priority = t.Priority,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                ProjectId = t.ProjectId,
                MilestoneId = t.MilestoneId,
                KeyResultId = t.KeyResultId,
                GoalId = t.GoalId,
                ReviewerUserId = t.ReviewerUserId,
                BlockReason = t.BlockReason,
                BlockNeeds = t.BlockNeeds,
                Result = t.Result,
                Checklist = string.Join("\n", t.Checklist.OrderBy(c => c.SortOrder).Select(c => c.Text)),
                DependsOnIds = IdList.Join(t.Dependencies.Select(d => d.DependsOnTodoId)),
            };
        }
        else
        {
            Input.OwnerUserId = me.UserId ?? 0;
            Input.ProjectId = projectId;
            Input.KeyResultId = keyResultId;
            Input.GoalId = keyResultId is null ? goalId : null;
            Input.DueDate = due;
            if (keyResultId is { } kr && projectId is null)
            {
                Input.ProjectId = await db.KeyResult.Where(k => k.Id == kr).Select(k => k.ProjectId).FirstOrDefaultAsync();
            }
        }

        await PrepareAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Todo? todo = null;
        if (Id is { } id)
        {
            todo = await db.Todo.Include(x => x.Contributors).Include(x => x.Checklist).Include(x => x.Dependencies).FirstOrDefaultAsync(x => x.Id == id);
            if (todo is null)
            {
                return NotFound();
            }
        }

        if (Input.StartDate is { } start && Input.DueDate is { } due && due < start)
        {
            ModelState.AddModelError("Input.DueDate", "Die Aufgabe kann nicht vor ihrem Start fällig sein.");
        }

        var dependsOn = IdList.Parse(Input.DependsOnIds);
        if (todo is not null)
        {
            foreach (var dep in dependsOn.Where(d => !todo.Dependencies.Any(x => x.DependsOnTodoId == d)))
            {
                if (await todos.WouldCycleAsync(todo.Id, dep))
                {
                    ModelState.AddModelError("Input.DependsOnIds", "Das ergäbe einen Kreis: eine Aufgabe, die auf sich selbst wartet.");
                    break;
                }
            }
        }

        var status = todo is null ? new Todo { Status = Input.Status } : todo;
        if (ModelState.IsValid && (todo is null || todo.Status != Input.Status) && await todos.CheckStatusAsync(status, Input.Status, Input.BlockReason) is { } problem)
        {
            ModelState.AddModelError(Input.Status == TodoStatus.Blocked ? "Input.BlockReason" : "Input.Status", problem);
        }

        if (!ModelState.IsValid)
        {
            await PrepareAsync();
            return Page();
        }

        var isNew = todo is null;
        var before = todo is null ? null : TodoSnapshot.Of(todo);
        todo ??= new Todo();
        todo.Title = Input.Title.Trim();
        todo.Description = Clean(Input.Description);
        todo.OwnerUserId = Input.OwnerUserId;
        todo.Priority = Input.Priority;
        todo.StartDate = Input.StartDate;
        todo.DueDate = Input.DueDate;
        todo.ProjectId = Input.ProjectId;
        todo.MilestoneId = Input.ProjectId is null ? null : Input.MilestoneId;
        todo.KeyResultId = Input.KeyResultId;
        // The goal follows from the key result; it is set directly only for an action that has no key result.
        todo.GoalId = Input.KeyResultId is null ? Input.GoalId : null;
        todo.ReviewerUserId = Input.Status == TodoStatus.InReview ? Input.ReviewerUserId : todo.ReviewerUserId;
        if (Request.Form.ContainsKey("Input.Result"))
        {
            todo.Result = Clean(Input.Result);
        }

        var blockNote = Input.Status == TodoStatus.Blocked && (isNew || todo.Status != TodoStatus.Blocked) ? Join(Input.BlockReason, Input.BlockNeeds) : null;
        todos.Apply(todo, Input.Status, Input.BlockReason, Input.BlockNeeds);

        SyncContributors(todo, IdList.Parse(Input.ContributorIds).Where(c => c != Input.OwnerUserId).ToList());
        SyncChecklist(todo, Input.Checklist);
        SyncDependencies(todo, dependsOn);

        if (isNew)
        {
            db.Todo.Add(todo);
        }

        await db.SaveChangesAsync();
        if (isNew)
        {
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.Created, note: todo.Title);
        }
        else
        {
            await todos.RecordAsync(todo, before!, blockNote);
        }

        await db.SaveChangesAsync();
        if (isNew)
        {
            await log.LogAsync("Todo angelegt", "Todo", todo.Id, todo.Title);
        }

        Flash(isNew ? "Das Todo ist angelegt." : "Das Todo ist gespeichert.");
        return LocalRedirect(SafeReturn(ReturnUrl, $"/Todos/Details?id={todo.Id}"));
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var todo = await db.Todo.FirstOrDefaultAsync(t => t.Id == Id);
        if (todo is not null)
        {
            history.Add(HistoryEntity.Todo, todo.Id, HistoryKind.Deleted, note: todo.Title);
            db.Todo.Remove(todo);
            await db.SaveChangesAsync();
            await log.LogAsync("Todo gelöscht", "Todo", todo.Id, todo.Title);
            Flash("Das Todo ist gelöscht.");
        }

        return LocalRedirect(SafeReturn(ReturnUrl, "/Todos"));
    }

    private void SyncContributors(Todo todo, List<long> wanted)
    {
        foreach (var gone in todo.Contributors.Where(c => !wanted.Contains(c.UserId)).ToList())
        {
            todo.Contributors.Remove(gone);
            db.TodoContributor.Remove(gone);
        }

        foreach (var userId in wanted.Where(w => !todo.Contributors.Any(c => c.UserId == w)))
        {
            todo.Contributors.Add(new TodoContributor { UserId = userId });
        }
    }

    private void SyncChecklist(Todo todo, string? text)
    {
        var lines = (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var gone in todo.Checklist.Where(c => !lines.Contains(c.Text, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            todo.Checklist.Remove(gone);
            db.TodoChecklistItem.Remove(gone);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var existing = todo.Checklist.FirstOrDefault(c => string.Equals(c.Text, lines[i], StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                todo.Checklist.Add(new TodoChecklistItem { Text = lines[i], SortOrder = i + 1 });
            }
            else
            {
                existing.SortOrder = i + 1;
            }
        }
    }

    private void SyncDependencies(Todo todo, List<long> wanted)
    {
        foreach (var gone in todo.Dependencies.Where(d => !wanted.Contains(d.DependsOnTodoId)).ToList())
        {
            todo.Dependencies.Remove(gone);
            db.TodoDependency.Remove(gone);
        }

        foreach (var dep in wanted.Where(w => w != todo.Id && !todo.Dependencies.Any(d => d.DependsOnTodoId == w)))
        {
            todo.Dependencies.Add(new TodoDependency { DependsOnTodoId = dep });
        }
    }

    private async Task PrepareAsync()
    {
        Users = await lookups.UsersAsync(Input.OwnerUserId);
        UserChoices = await lookups.UserChoicesAsync();
        Projects = await lookups.ProjectsAsync(openOnly: true, alsoInclude: Input.ProjectId);
        Goals = await lookups.GoalsAsync();
        KeyResults = await lookups.KeyResultsAsync();
        Milestones = await lookups.MilestonesAsync();
        TodoChoices = await lookups.TodoChoicesAsync(Id);
        var title = Id is null ? "Neues Todo" : "Todo bearbeiten";
        Head(title, null, ("Meine Todos", "/Todos"), (title, null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = "/Todos";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Join(string? reason, string? needs)
    {
        var parts = new[] { reason?.Trim(), string.IsNullOrWhiteSpace(needs) ? null : "Benötigt: " + needs.Trim() }.Where(p => !string.IsNullOrEmpty(p));
        var text = string.Join(" · ", parts);
        return text.Length == 0 ? null : text;
    }
}
