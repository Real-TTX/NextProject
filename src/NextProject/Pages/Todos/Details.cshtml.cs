using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using NextProject.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Todos;

/// <summary>
/// One todo as it is worked on: the status and what the next step is, the checklist, the comments, the attachments, the dependencies and the history. Every
/// change on this page is one small post, so that nothing is lost when two people have the page open.
/// </summary>
public sealed class DetailsModel(NpDbContext db, TodoService todos, HistoryService history, FileStoreService files, ICurrentUser me, PlanningService planning, ActivityLogger log) : PageModelBase
{
    public const long MaxUploadBytes = 25L * 1024 * 1024;

    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public Todo Todo { get; private set; } = null!;

    public List<HistoryEntry> History { get; private set; } = [];

    public List<Todo> WaitsFor { get; private set; } = [];

    public List<Todo> BlockedByThis { get; private set; } = [];

    public long? CurrentUserId => me.UserId;

    public bool IsAdmin => me.IsAdmin;

    public DateOnly Today => planning.Today;

    public bool IsOverdue => Todo.IsOverdue(Today);

    public string? Goal => Todo.Goal?.Title ?? Todo.KeyResult?.Goal?.Title;

    public int ChecklistDone => Todo.Checklist.Count(c => c.IsDone);

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    // ------------------------------------------------------------------------------------------------ status

    public async Task<IActionResult> OnPostStatusAsync(TodoStatus to, string? result)
    {
        var todo = await db.Todo.FirstOrDefaultAsync(t => t.Id == Id);
        if (todo is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            todo.Result = result.Trim();
        }

        if (to == TodoStatus.Blocked)
        {
            return await ShowAsync("Zum Blockieren brauchst du einen Grund: das Formular „Blockieren“ fragt danach.");
        }

        if (await todos.ChangeStatusAsync(Id, to) is { } problem)
        {
            return await ShowAsync(problem);
        }

        // ChangeStatusAsync saves; a result that came with the click is saved with it.
        await db.SaveChangesAsync();
        Flash(to switch { TodoStatus.Done => "Erledigt. Gut gemacht.", TodoStatus.Discarded => "Das Todo ist verworfen.", _ => "Der Status ist geändert." });
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostBlockAsync(string? reason, string? needs)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return await ShowAsync("Bitte beschreibe, was die Aufgabe blockiert.");
        }

        if (await todos.ChangeStatusAsync(Id, TodoStatus.Blocked, reason, needs) is { } problem)
        {
            return await ShowAsync(problem);
        }

        Flash("Die Aufgabe ist als blockiert gekennzeichnet.");
        return RedirectToPage(new { id = Id });
    }

    // ------------------------------------------------------------------------------------------------ checklist

    public async Task<IActionResult> OnPostToggleCheckAsync(long itemId)
    {
        var item = await db.TodoChecklistItem.FirstOrDefaultAsync(c => c.Id == itemId && c.TodoId == Id);
        if (item is not null)
        {
            item.IsDone = !item.IsDone;
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id = Id, tab = "check" });
    }

    public async Task<IActionResult> OnPostAddCheckAsync(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && await db.Todo.AnyAsync(t => t.Id == Id))
        {
            var next = (await db.TodoChecklistItem.Where(c => c.TodoId == Id).MaxAsync(c => (int?)c.SortOrder) ?? 0) + 1;
            db.TodoChecklistItem.Add(new TodoChecklistItem { TodoId = Id, Text = text.Trim(), SortOrder = next });
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id = Id, tab = "check" });
    }

    // ------------------------------------------------------------------------------------------------ comments

    public async Task<IActionResult> OnPostCommentAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await ShowAsync("Bitte schreibe einen Kommentar.", "comments");
        }

        if (!await db.Todo.AnyAsync(t => t.Id == Id))
        {
            return NotFound();
        }

        db.TodoComment.Add(new TodoComment { TodoId = Id, Text = text.Trim() });
        await db.SaveChangesAsync();
        return RedirectToPage(new { id = Id, tab = "comments" });
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(long commentId)
    {
        var comment = await db.TodoComment.FirstOrDefaultAsync(c => c.Id == commentId && c.TodoId == Id);
        if (comment is not null && (comment.CreateUserId == me.UserId || me.IsAdmin))
        {
            db.TodoComment.Remove(comment);
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { id = Id, tab = "comments" });
    }

    // ------------------------------------------------------------------------------------------------ attachments

    public async Task<IActionResult> OnPostLinkAsync(string? title, string? url)
    {
        url = url?.Trim();
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return await ShowAsync("Das ist keine Adresse. Sie beginnt mit https:// oder http://.", "files");
        }

        db.TodoAttachment.Add(new TodoAttachment { TodoId = Id, Kind = AttachmentKind.Link, Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title.Trim(), Url = uri.ToString() });
        await db.SaveChangesAsync();
        return RedirectToPage(new { id = Id, tab = "files" });
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return await ShowAsync("Bitte wähle eine Datei aus.", "files");
        }

        if (file.Length > MaxUploadBytes)
        {
            return await ShowAsync($"Die Datei ist größer als {Fmt.FileSize(MaxUploadBytes)}.", "files");
        }

        if (!await db.Todo.AnyAsync(t => t.Id == Id))
        {
            return NotFound();
        }

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var stored = await files.SaveAsync(buffer.ToArray(), file.FileName, string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        db.TodoAttachment.Add(new TodoAttachment { TodoId = Id, Kind = AttachmentKind.File, Title = stored.FileName, StoredFile = stored });
        await db.SaveChangesAsync();
        await log.LogAsync("Anhang hochgeladen", "Todo", Id, stored.FileName);
        return RedirectToPage(new { id = Id, tab = "files" });
    }

    public async Task<IActionResult> OnPostDeleteAttachmentAsync(long attachmentId)
    {
        var attachment = await db.TodoAttachment.Include(a => a.StoredFile).FirstOrDefaultAsync(a => a.Id == attachmentId && a.TodoId == Id);
        if (attachment is not null)
        {
            var stored = attachment.StoredFile;
            db.TodoAttachment.Remove(attachment);
            if (stored is not null)
            {
                db.StoredFile.Remove(stored);
            }

            await db.SaveChangesAsync();
            if (stored is not null)
            {
                try
                {
                    await files.DeleteAsync(stored);
                }
                catch (StorageException)
                {
                    // the row is gone; a file that stays behind in the store is cleaned up by hand, and is better than a failed delete
                }
            }
        }

        return RedirectToPage(new { id = Id, tab = "files" });
    }

    // ------------------------------------------------------------------------------------------------

    private async Task<IActionResult> ShowAsync(string error, string? tab = null)
    {
        await LoadAsync();
        ModelState.AddModelError(string.Empty, error);
        ViewData["Tab"] = tab;
        return Page();
    }

    private async Task<bool> LoadAsync()
    {
        var todo = await db.Todo.AsNoTracking().AsSplitQuery()
            .Include(t => t.OwnerUser).Include(t => t.ReviewerUser).Include(t => t.Project).Include(t => t.Goal).Include(t => t.KeyResult).ThenInclude(k => k!.Goal)
            .Include(t => t.Milestone).Include(t => t.Contributors).ThenInclude(c => c.User).Include(t => t.Checklist)
            .Include(t => t.Comments).ThenInclude(c => c.CreateUser).Include(t => t.Attachments).ThenInclude(a => a.StoredFile)
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOnTodo)
            .FirstOrDefaultAsync(t => t.Id == Id);
        if (todo is null)
        {
            return false;
        }

        Todo = todo;
        History = await history.ForAsync(HistoryEntity.Todo, Id);
        WaitsFor = [.. todo.Dependencies.Select(d => d.DependsOnTodo!)];
        BlockedByThis = await db.TodoDependency.AsNoTracking().Where(d => d.DependsOnTodoId == Id).Select(d => d.Todo!).ToListAsync();
        Head(todo.Title, null, ("Meine Todos", "/Todos"), (todo.Title, null));
        ViewData["ActiveHref"] = "/Todos";
        ViewData["OwnHead"] = true;
        return true;
    }
}
