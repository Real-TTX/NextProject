using NextProject.Data;
using NextProject.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Pages.Todos;

/// <summary>Hands out an attachment. It is always a download, never shown in the browser: a file that somebody uploaded must not run as a page of this site.</summary>
public sealed class DownloadModel(NpDbContext db, FileStoreService files) : PageModelBase
{
    public async Task<IActionResult> OnGetAsync(Guid token)
    {
        var file = await db.TodoAttachment.AsNoTracking().Where(a => a.StoredFile!.Token == token).Select(a => a.StoredFile).FirstOrDefaultAsync();
        if (file is null)
        {
            return NotFound();
        }

        try
        {
            var stream = await files.OpenAsync(file, HttpContext.RequestAborted);
            return File(stream, "application/octet-stream", file.FileName);
        }
        catch (StorageException)
        {
            return NotFound();
        }
    }
}
