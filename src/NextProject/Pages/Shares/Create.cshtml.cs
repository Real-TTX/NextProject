using System.ComponentModel.DataAnnotations;
using NextProject.Configuration;
using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Shares;

/// <summary>Makes a link that shows a project or a goal to somebody who has no account (the role "Anonym"): read only, for a limited time, optionally with a password.</summary>
public sealed class CreateModel(NpDbContext db, ShareService shares, ConfigStore config, TimeProvider clock, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Bitte gib dem Link einen Titel.")]
        [Display(Name = "Titel")]
        public string Title { get; set; } = "";

        [Display(Name = "Nachricht")]
        public string? Message { get; set; }

        [Range(1, 365, ErrorMessage = "Ein Link gilt zwischen 1 und 365 Tagen.")]
        [Display(Name = "Gültig für Tage")]
        public int Days { get; set; } = 14;

        [Display(Name = "Passwort")]
        public string? Password { get; set; }

        public bool ShowTodos { get; set; } = true;

        [Display(Name = "Für")]
        public string? RecipientName { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public ShareKind Kind { get; set; }

    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string TargetName { get; private set; } = "";

    public string BackHref => Kind == ShareKind.Project ? $"/Projects/Details?id={Id}" : $"/Goals/Details?id={Id}";

    public ShareLink? Created { get; private set; }

    public string? CreatedUrl { get; private set; }

    public List<ShareLink> Links { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadTargetAsync())
        {
            return NotFound();
        }

        Input.Title = TargetName;
        Input.Days = config.Current.Security.ShareLinkDays;
        await LoadLinksAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadTargetAsync())
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(Input.Password) && Input.Password.Length < 6)
        {
            ModelState.AddModelError("Input.Password", "Das Passwort braucht mindestens 6 Zeichen. Oder lass es leer.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLinksAsync();
            return Page();
        }

        Created = await shares.CreateAsync(Kind, Id, Input.Title, Input.Message, Input.Days, Input.Password, Input.ShowTodos, Input.RecipientName);
        CreatedUrl = shares.LinkOf(Created);
        await log.LogAsync("Freigabe-Link erstellt", Kind.ToString(), Id, Input.Title);
        Input = new InputModel { Title = TargetName, Days = config.Current.Security.ShareLinkDays };
        ModelState.Clear();
        await LoadLinksAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(long linkId)
    {
        var link = await db.ShareLink.FirstOrDefaultAsync(l => l.Id == linkId);
        if (link is not null)
        {
            link.RevokedDate ??= clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
            Flash("Der Link ist widerrufen. Wer ihn hat, sieht nichts mehr.");
        }

        return RedirectToPage(new { kind = Kind, id = Id });
    }

    public string UrlOf(ShareLink link) => shares.LinkOf(link);

    private async Task<bool> LoadTargetAsync()
    {
        var name = Kind == ShareKind.Project
            ? await db.Project.AsNoTracking().Where(p => p.Id == Id).Select(p => p.Name).FirstOrDefaultAsync()
            : await db.Goal.AsNoTracking().Where(g => g.Id == Id).Select(g => g.Title).FirstOrDefaultAsync();
        if (name is null)
        {
            return false;
        }

        TargetName = name;
        Head("Freigeben", $"Ein Link zu „{name}“ für alle, die kein Konto haben.", (Kind == ShareKind.Project ? "Projekte" : "Ziele & OKRs", Kind == ShareKind.Project ? "/Projects" : "/Goals"), (name, BackHref), ("Freigeben", null));
        ViewData["Width"] = "narrow";
        ViewData["ActiveHref"] = Kind == ShareKind.Project ? "/Projects" : "/Goals";
        return true;
    }

    private async Task LoadLinksAsync()
        => Links = await db.ShareLink.AsNoTracking().Where(l => Kind == ShareKind.Project ? l.ProjectId == Id : l.GoalId == Id).OrderByDescending(l => l.CreateDate).ToListAsync();
}
