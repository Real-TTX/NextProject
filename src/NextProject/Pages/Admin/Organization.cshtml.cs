using System.ComponentModel.DataAnnotations;
using NextProject.Configuration;
using NextProject.Services;
using Microsoft.AspNetCore.Mvc;

namespace NextProject.Pages.Admin;

/// <summary>The company that uses the installation (its name stands on the pages that are shared) and the two numbers that decide what the software flags.</summary>
public sealed class OrganizationModel(ConfigStore config, ActivityLogger log) : PageModelBase
{
    public sealed class InputModel
    {
        [Display(Name = "Name des Unternehmens")]
        public string? Name { get; set; }

        [Range(1, 60, ErrorMessage = "Das sind zwischen 1 und 60 Tage.")]
        [Display(Name = "Demnächst fällig heißt: innerhalb von Tagen")]
        public int DueSoonDays { get; set; } = 7;

        [Range(1, 50, ErrorMessage = "Das sind zwischen 1 und 50 Aufgaben.")]
        [Display(Name = "Ein Projekt ist gefährdet ab überfälligen Aufgaben")]
        public int OverdueLimit { get; set; } = 3;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string TimeZone => Fmt.Zone.Id;

    public void OnGet()
    {
        Prepare();
        var o = config.Current.Organization;
        Input = new InputModel { Name = o.Name, DueSoonDays = o.DueSoonDays, OverdueLimit = o.OverdueLimit };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Prepare();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        config.Update(c =>
        {
            c.Organization.Name = Input.Name?.Trim() ?? "";
            c.Organization.DueSoonDays = Input.DueSoonDays;
            c.Organization.OverdueLimit = Input.OverdueLimit;
        });
        await log.LogAsync("Allgemeine Einstellungen geändert", "Organization");
        Flash("Die Einstellungen sind gespeichert.");
        return RedirectToPage();
    }

    private void Prepare()
    {
        Head("Allgemein", "Das Unternehmen und wann die Software auf etwas hinweist.", ("Einstellungen", null), ("Allgemein", null));
        ViewData["Width"] = "narrow";
    }
}
