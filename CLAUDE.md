# NextProject: notes for working in this repository

Goals, projects and todos for a company, replacing a todo list and a progress spreadsheet: ASP.NET Core 10 Razor Pages, EF Core 10 on PostgreSQL 17,
one container (port **2860**) plus a database. The user-facing text is **German**; code, comments and docs are English. The infrastructure (setup, sign-in,
TOTP, sessions, backup with NAS, UI kit) was taken from the sibling repository `../KommasKorrektus` and is kept in the same style: when in doubt, look there.

## Commands

```powershell
./scripts/redeploy.ps1            # always: rebuild the image and redeploy the dev stack (web 2860, pgAdmin 2861, Postgres 12860)
./scripts/redeploy.ps1 -Fresh     # the same with empty volumes (setup page again; Einstellungen > System can write sample data)
dotnet build NextProject.slnx     # build locally BEFORE a redeploy: a compile error otherwise shows up as "docker compose failed"
dotnet test NextProject.slnx      # integration tests need NEXTPROJECT_TEST_DB, e.g. Host=localhost;Port=12860;Username=postgres;Password=nextproject
./scripts/check-matui.ps1         # is there news in MatDesign for the toolkit copy in wwwroot/ui?
./scripts/sync-matui.ps1 -Archive <matui.zip> -Commit <sha>   # take a new toolkit version
```

Test and look at changes by **rebuilding the container and redeploying the stack**, not with a watcher. Do not pipe `redeploy.ps1` into `Select-Object -First n`
(it stops the script half way); use `-Last`. Wait for `http://localhost:2860/health` before driving the app.

The NAS tests need a Samba server on port 445 (`NEXTPROJECT_TEST_SMB=host;share=backups;user=backup;password=backup-secret`; the CI starts one). On a Windows PC
445 is taken: run Samba as a container in the network of the dev stack and run `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0` in the same network (copy the
source into the container, without `bin` and `obj`). Remember `MSYS_NO_PATHCONV=1` in Git Bash for arguments that start with `/`.

## Layout

- `src/NextProject.Core`: no web dependencies. `Planning/Progress.cs` (the rules of progress), `Security/` (TOTP, password hashing)
- `src/NextProject`: the web app. `Pages/<Area>/` (Razor Pages), `Services/`, `Controls/` (tag helpers `np-*`), `UiKit/` (table, toolbar, pager, board, timeline, shell),
  `Data/` (EF model, migrations), `Storage/` (volume and SMB), `wwwroot/` (css, js, the MatDesign toolkit in `ui/`)
- `tests/NextProject.Tests`: `Core/` and `Web/` unit tests, `Integration/` (the real app against PostgreSQL / Samba)
- `docs/PROGRESS.md` says how progress is worked out; `docs/OPERATIONS.md` how it is run

## Conventions

- **Database**: PostgreSQL, tables PascalCase, `Id` BIGINT, audit columns `CreateDate/CreateUserId/UpdateDate/UpdateUserId` (filled by `NpDbContext.SaveChanges`),
  `UpdateState` (0 deleted, 1 created, 2 updated) for soft delete (`ISoftDeletable`: User, Department, Goal, KeyResult, Project, Todo), enums are stored as **text**,
  a `Token` uuid for anything that acts as a secret key (`UserSession`, `ShareLink`, `StoredFile`). JSON is for configuration (`/data/config/app.json`); logic lives in the database.
  A migration is made with `dotnet ef migrations add <Name> -p src/NextProject -s src/NextProject -o Data/Migrations`. **No automatic EF retry.**
- **The domain**: a todo may stand alone; `Todo.ProjectId`, `GoalId`, `KeyResultId`, `MilestoneId` are all optional (a todo with a key result gets its goal from it). A key result
  belongs to a goal and may belong to a project. **Never merge "todos done" and "goal reached"**: `Progress` and `PlanningService` are the only places that work out progress,
  pages ask them (two numbers side by side). Status changes go through `TodoService` (a block needs a reason, done needs finished dependencies, the history is written there).
- **UI**: lists = toolbar (search, filter, sort) above, table, pager directly below, actions below on the left; create / edit on a separate page; buttons in one row from positive to
  negative with the delete button spaced apart (`np-buttons`). Use the shared controls (`ListBuilder`, `TableModel`, `TodoTable`, `_Toolbar`, `_Table`, `_Pager`, `_Board`, `_Timeline`, `_LinkTabs`,
  `np-input`, `np-select`, `np-picker`, `np-tabs` ...); several of one kind per page must work (every id is derived from the id of the control). Dependent fields use `show-if`
  (hidden fields are disabled and not posted: do not let a default overwrite stored values; `Todos/Edit` reads `Input.Result` only when it was posted). No inline scripts or styles (CSP
  `default-src 'self'`): a value that is data (a position, a bar) is a `data-*` attribute that `app.js` turns into a custom property. Icons from the sprite (`Ui.Icon`); a name that is not in the
  sprite draws nothing, `IconTests` checks every name.
- **Look**: the customer wants it *plakativ* (bold): dark menu, filled chips, big KPI tiles, cards, coloured board heads. Draw these as own components in `wwwroot/css/np-ui.css` (`np-*`) and use MatUi
  only for the basics (shell, forms, buttons, tables, tabs, dialogs, pickers, the schedule). What MatDesign lacks goes into `docs/DESIGN-GAPS.md` (add to it when you draw something new).
  Status is `Labels.Pill` (filled `np-chip`), numbers are `Ui.Kpi`, `Ui.Ring`, `Ui.Meter`.
- **Calendar**: `CalendarService` builds the items (filters in `CalendarFilter`), `Calendar/Index` is the MatUi schedule (`calendar.js` adds the dialog and drag and drop, `POST /api/todos/{id}/due`
  through `TodoService.MoveAsync`), `Calendar/Plan` is the Gantt view (`GanttView`, `_Gantt`, `gantt.js`), `Calendar/Workload` counts open todos per person and week. The iCal feed is
  `/cal/{token}.ics` (anonymous, the token is `User.CalendarToken`, `IcsWriter` writes the file). Bar positions are `data-np-bar` values that `app.js` turns into custom properties.
- **Cherry red** is the accent (`[data-app="nextproject"] { --mat-app-accent: #c8102e }` in `app.css`). It looks like the danger colour: never let colour be the only sign (status pills carry words and icons).
- **Formatting**: the request culture is **invariant on purpose**; format numbers and dates with `Fmt` (de-DE), never with `ToString()` in text a person reads
- **Razor** encodes `ü` as an entity: tests compare with `AppFixture.TextAsync` (decoded)
- **Forms with several handlers**: give the form `asp-page-handler=""`, or an explicit handler, so that a re-rendered page does not post the next Save to the old handler. A form with an
  `action` attribute gets no anti-forgery token on its own: add `asp-antiforgery="true"`.
- **Security**: share links answer `410` (gone) or `404`; the password gate comes before anything else and wrong passwords are counted per link (`ShareService`, in memory). Attachments are
  always downloads (`application/octet-stream`). CSV cells that start with `= + - @` are made harmless.
- **Code**: little nesting, early returns, a class may hold several related business functions; Microsoft C# guidelines; comments say *why*. Match the surrounding style
- **MatUi toolkit** (`wwwroot/ui`, from the sibling repo `../MatDesign`) has a version (`CHANGELOG.md`, `matui.manifest.json`, `scripts/matui-synced.txt`). Never edit the copied files by hand;
  copy all files of a version. `np-icons.svg` is ours (the logo and a few icons).

## Gotchas

- Shell strings with backticks, `$"`, apostrophes or `$$` get mangled by the tool that runs Bash: write files with the editor tools, not with heredocs or `node -e`.
- A `[BindProperty(SupportsGet = true)] long Id` on a page binds `?id=`; list toolbars of a page with tabs carry `id` and `tab` in `Keep` so a filter does not forget them.
- `DemoData` writes sample data only into an empty installation (no goals, projects, todos or departments).
- The dev compose has an explicit subnet (`10.253.28.0/24`): Docker runs out of default address pools on a machine with many stacks.

## Git

Branches: `main` (release), `dev` (development). Commit and push **under the user's name** only: no `Co-Authored-By` trailer, no "Generated with" line (the global rule of the
user overrides any default). Versions: release `<major>.<minor>.<build>-<date>`, dev `nightly-<build>-<date>`, local `local-<date>` (`Directory.Build.props`, set by the GitHub action).
The folder `initial/` (the customer's spreadsheet, slides and mock-ups that started the project) is not committed.
