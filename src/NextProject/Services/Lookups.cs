using NextProject.Data;
using NextProject.Data.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>The lists that the selects of the forms and the filters of the lists offer: people, projects, goals, key results, departments.</summary>
public sealed class Lookups(NpDbContext db)
{
    /// <summary>The people who can be made responsible: active users, and <paramref name="alsoInclude"/> even when that person was switched off since.</summary>
    public async Task<List<SelectListItem>> UsersAsync(long? alsoInclude = null, CancellationToken ct = default)
        => await db.User.AsNoTracking().Where(u => u.IsActive || u.Id == alsoInclude).OrderBy(u => u.DisplayName)
            .Select(u => new SelectListItem(u.DisplayName, u.Id.ToString())).ToListAsync(ct);

    /// <summary>Users with the job as the description (for the pickers).</summary>
    public async Task<List<SelectListItem>> UserChoicesAsync(CancellationToken ct = default)
        => (await db.User.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.DisplayName).Select(u => new { u.Id, u.DisplayName, u.JobTitle }).ToListAsync(ct))
            .Select(u => new SelectListItem(u.DisplayName, u.Id.ToString()) { Group = u.JobTitle is { Length: > 0 } ? new SelectListGroup { Name = u.JobTitle } : null }).ToList();

    public async Task<List<SelectListItem>> ProjectsAsync(bool openOnly = false, long? alsoInclude = null, CancellationToken ct = default)
        => await db.Project.AsNoTracking()
            .Where(p => !openOnly || (p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled) || p.Id == alsoInclude)
            .OrderBy(p => p.Name).Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync(ct);

    public async Task<List<SelectListItem>> GoalsAsync(CancellationToken ct = default)
        => await db.Goal.AsNoTracking().OrderByDescending(g => g.Year).ThenBy(g => g.Title).Select(g => new SelectListItem(g.Title, g.Id.ToString())).ToListAsync(ct);

    /// <summary>Key results with their goal as the description, so that two results with the same name under different goals can be told apart.</summary>
    public async Task<List<SelectListItem>> KeyResultsAsync(CancellationToken ct = default)
        => (await db.KeyResult.AsNoTracking().OrderBy(k => k.Goal!.Title).ThenBy(k => k.Title).Select(k => new { k.Id, k.Title, Goal = k.Goal!.Title }).ToListAsync(ct))
            .Select(k => new SelectListItem(k.Title, k.Id.ToString()) { Group = new SelectListGroup { Name = k.Goal } }).ToList();

    public async Task<List<SelectListItem>> MilestonesAsync(CancellationToken ct = default)
        => (await db.Milestone.AsNoTracking().OrderBy(m => m.Project!.Name).ThenBy(m => m.DueDate).Select(m => new { m.Id, m.Title, Project = m.Project!.Name }).ToListAsync(ct))
            .Select(m => new SelectListItem($"{m.Project}: {m.Title}", m.Id.ToString())).ToList();

    public async Task<List<SelectListItem>> DepartmentsAsync(CancellationToken ct = default)
        => await db.Department.AsNoTracking().OrderBy(d => d.Name).Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToListAsync(ct);

    /// <summary>The todos a todo can wait for: every other todo that is not closed, with its project as the description.</summary>
    public async Task<List<SelectListItem>> TodoChoicesAsync(long? exceptId, CancellationToken ct = default)
        => (await db.Todo.AsNoTracking().Where(t => t.Id != exceptId && t.Status != TodoStatus.Done && t.Status != TodoStatus.Discarded).OrderBy(t => t.Title)
                .Select(t => new { t.Id, t.Title, Project = t.Project != null ? t.Project.Name : null }).Take(500).ToListAsync(ct))
            .Select(t => new SelectListItem(t.Title, t.Id.ToString()) { Group = t.Project is null ? null : new SelectListGroup { Name = t.Project } }).ToList();

    /// <summary>The projects a project can depend on.</summary>
    public async Task<List<SelectListItem>> ProjectChoicesAsync(long? exceptId, CancellationToken ct = default)
        => await db.Project.AsNoTracking().Where(p => p.Id != exceptId).OrderBy(p => p.Name).Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync(ct);

    public static List<SelectListItem> Years(int around, int back = 2, int forward = 3)
        => [.. Enumerable.Range(around - back, back + forward + 1).Select(y => new SelectListItem(y.ToString(), y.ToString()))];
}
