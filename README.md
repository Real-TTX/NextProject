<div align="center">

<img src="src/NextProject/wwwroot/logo.svg" width="96" alt="NextProject" />

# NextProject

**Goals, projects and todos in one place: who does what, by when, and does it get us where we want to go?**

Replaces the todo list and the spreadsheet for the progress of projects. Self-hosted, German interface,
one container plus a PostgreSQL, backups straight to your NAS.

</div>

![Dashboard: my todos, my projects, goals and key results, team status](docs/images/dashboard.png)

---

## What this is about

The management of a company agrees on **goals**, starts **projects** to reach them and splits those into **todos**. In a spreadsheet the
connection between the three gets lost, and "80 % of the todos are done" is mistaken for "the goal is reached". NextProject keeps the
four levels apart and connected:

| Level | Question | In the software |
|---|---|---|
| **Goals** | What do we want to reach as a company? | Goal with yearly and quarterly targets, **key results** that measure it |
| **Projects** | Which initiatives pay into it? | Project with owner, team, milestones, risks, dependencies |
| **Todos** | Who does what by when? | Todo with one owner, status, checklist, comments, attachments, history |
| **Results** | How do we know it worked? | Measurements of the key results, reviews |

A todo may stand alone. Nothing has to belong to a project or a goal.

**Finished is not reached.** The share of finished todos is an *indicator* for a project; how far a goal is reached is told by its key
results, measured on a scale from a start value to a target. Both numbers are always shown side by side
([rules of progress](docs/PROGRESS.md)).

## At a glance

**Work**
- **Todos**: create, assign, prioritise, move, finish or discard; one responsible person, optional contributors, start and due date,
  checklist, comments, attachments (files or links), dependencies, the result written down. *Overdue* is worked out from the due date and
  the status, never set by hand. A **block** needs a reason and says what help or decision is needed. Every change of status, owner and
  due date stays in the **history**
- **Lists that find things**: search, filters (status, owner, priority, project, due date) and sort above every list; tabs *Today*,
  *This week*, *All mine*, *Team*; a **board** (drag a card to another column; every card also has a select for phones and keyboards)
- **Projects & goals as cards** (with a switch to the list): a coloured tile for the state, the two numbers side by side, what needs a look
- **Projects**: owner, team, period, status (planning, in progress, **at risk**, paused, completed, cancelled), priority, budget, effort,
  milestones, risks, dependencies, goals and key results; tabs for the todo list, the board and a **schedule**. The software points out
  what looks bad (blocked todos, overdue ones, a late milestone, a high risk, an end date that has passed, a pace behind time)
- **Goals & OKRs**: goals with yearly and quarterly targets, key results with start value, target, unit, how it is measured and
  **check-ins** (value, and the share of the linked todos that were done at that time, as in the planning sheet)
- **Reviews**: the four questions of the review sheet (deviation of implementation from strategy, learnings, adjustments, steering)

**Overview**
- **Dashboard, team, reports**: what is mine today, where the team is stuck, the table "strategic implementation" and CSV exports for the spreadsheet
- **Calendar** in three views. *Monat* shows every due date, milestone, project end, key-result date and review, with **filters** (whose, project,
  person, department, goal, kind, status, search) and a legend; click a todo to move or finish it, **drag it to another day** to change its due date
  (the start moves along), click a day to create a todo there. *Zeitplan* is a **Gantt view**: the projects below each other, the time from left
  to right (3, 6 or 12 months), the share of finished todos inside each bar, milestones on it, the todos of a project one click away.
  *Auslastung* counts the open todos per person and week
- **iCal subscription**: every user has a private address (`/cal/<secret>.ics`) for Outlook, Apple or Google Calendar that can be replaced
  at any time, and can download the file

**People and operations**
- **Roles**: *Admin*, *User* and *Anonymous*, which is a **share link** to a project or a goal (read only, expiry, optional password,
  counted views, can be revoked)
- **Sign-in**: local accounts, **TOTP** (authenticator app) with recovery codes, lock-out after wrong passwords, sessions kept in the
  database (a restart keeps everybody signed in)
- **Backup**: plans with a **schedule** (hourly, daily, weekly, monthly) to a folder or **directly to a NAS over SMB**, with
  **retention** (last N, daily, weekly, monthly), optional encryption, a restore with a safety copy and a check before anything changes.
  Attachments can live on the NAS as well
- **Mobile**, light, dark and system theme, no scrollbars until something scrolls
- **Sample data** for trying it out: one click on an empty installation

## Screenshots

### Todos, the board and one todo

![Todos of the team](docs/images/todos.png)

Search, filters and sort above the list, the pager directly below. A checkbox finishes a todo in place.

| Board | A todo |
|---|---|
| ![Board with coloured columns](docs/images/board.png) | ![A todo with checklist, comments and history](docs/images/todo-details.png) |

Cards can be dragged to another column; a block asks for its reason first.

### Projects and goals as cards

| Projects | Goals |
|---|---|
| ![Projects as cards](docs/images/projects.png) | ![Goals as cards](docs/images/goals.png) |

Two numbers side by side on purpose: *todos done* and *goal reached*. The switch above the toolbar turns the cards into a list.

| A project | A goal |
|---|---|
| ![A project: signals, numbers, tabs](docs/images/project-details.png) | ![A goal with its key results](docs/images/goal.png) |

### Calendar: month, schedule and workload

![Calendar: the month with filters and a legend](docs/images/calendar.png)

Filters above, a legend, a dialog on a click and **drag and drop** to move a due date.

![Schedule of all projects](docs/images/plan.png)

The projects below each other, the time from left to right, the finished share darker inside each bar, milestones as diamonds.

![Open todos per person and week](docs/images/workload.png)

### Team, reviews and reports

| Team | Reviews | Reports |
|---|---|---|
| ![Who has what to do](docs/images/team.png) | ![Reviews](docs/images/reviews.png) | ![Strategic implementation](docs/images/reports.png) |

### Share links for people without an account

![A shared project, read only](docs/images/share.png)

A link to a project or a goal: read only, with an expiry, an optional password and a message on top. Counted views, revoke any time.

### Sign-in and security

| Sign-in | Second factor and devices |
|---|---|
| ![Sign-in](docs/images/login.png) | ![TOTP and signed-in devices](docs/images/security.png) |

### Backup to your NAS

| Backup | A plan |
|---|---|
| ![Backup overview](docs/images/backup.png) | ![A plan with schedule and retention](docs/images/backup-plan.png) |

### System, dark theme and phones

![System: version, database, contents](docs/images/system.png)

| Dark | Phone | Phone | Phone |
|---|---|---|---|
| ![Dark theme](docs/images/dashboard-dark.png) | ![Dashboard on a phone](docs/images/mobile-dashboard.png) | ![Calendar on a phone](docs/images/mobile-calendar.png) | ![Projects on a phone](docs/images/mobile-projects.png) |

## Quick start

Ready-made images are published to the GitHub Container Registry:

| Tag | Built from | Use it for |
|---|---|---|
| `ghcr.io/real-ttx/nextproject:latest` | `main` | releases |
| `ghcr.io/real-ttx/nextproject:nightly` | `dev` | the newest features |

### 1. Just run it

Copy this into `docker-compose.yml` and start it: one app, one database, two volumes.

```yaml
services:
  nextproject:
    image: ghcr.io/real-ttx/nextproject:latest
    container_name: nextproject
    restart: unless-stopped
    depends_on:
      - db
    ports:
      - "2860:2860"
    volumes:
      - nextproject-data:/data

  db:
    image: postgres:17
    container_name: nextproject-db
    restart: unless-stopped
    environment:
      POSTGRES_PASSWORD: nextproject
    volumes:
      - nextproject-db:/var/lib/postgresql/data

volumes:
  nextproject-data:
  nextproject-db:
```

```bash
docker compose up -d
```

Open **http://localhost:2860**. The first visit asks for the **first administrator** (there is no default account). Under
*Einstellungen › System* an empty installation can be filled with **sample data**. The health check is `/health`. An update is
`docker compose pull && docker compose up -d`; the migrations run at start-up.

### 2. With backups on your NAS

The simplest way needs **no mount at all**: in *Einstellungen › Backup* add a target of the kind *NAS (SMB-Freigabe)* with the server, the share
and an account that may write to it, test it, and make a plan (when, what, how many to keep). SMB 2.0.2 to 3.0.2 over TCP 445.

If the NAS is already mounted on the Docker host (NFS, a Synology share, a TrueNAS dataset), hand a folder of it to the container and add
a target of the kind *Ordner* with the path **inside the container**:

```yaml
services:
  nextproject:
    image: ghcr.io/real-ttx/nextproject:latest
    container_name: nextproject
    restart: unless-stopped
    depends_on:
      - db
    ports:
      - "2860:2860"
    environment:
      NEXTPROJECT__Server__TimeZone: Europe/Berlin
    volumes:
      # The application's own data - keep this one (and keys/ in every backup)
      - nextproject-data:/data
      # A folder of the NAS. Left of the colon is the host, right of it what NextProject sees.
      - /volume1/backups/nextproject:/backups

  db:
    image: postgres:17
    container_name: nextproject-db
    restart: unless-stopped
    environment:
      POSTGRES_PASSWORD: nextproject
    volumes:
      - nextproject-db:/var/lib/postgresql/data

volumes:
  nextproject-data:
  nextproject-db:
```

Then *Einstellungen › Backup › Ziele*: kind *Ordner*, path `/backups`. A relative path (`backups`) ends up inside the `nextproject-data` volume.

| System | Shares are usually under | Example line |
|---|---|---|
| Synology | `/volume1` | `- /volume1/backups/nextproject:/backups` |
| TrueNAS SCALE | `/mnt/<pool>` | `- /mnt/tank/backups/nextproject:/backups` |
| Unraid | `/mnt/user` | `- /mnt/user/backups/nextproject:/backups` |
| QNAP | `/share` | `- /share/Backups/nextproject:/backups` |
| plain Linux | anywhere | `- /srv/backups/nextproject:/backups` |

The attachments of the todos can live on the NAS too (*Einstellungen › Speicher (NAS)*): they are moved over and back at the click of a button.

### 3. Behind a reverse proxy, with your own database password

Do not publish the port: let the proxy (Traefik, Caddy, nginx) reach the container, and set the public address so that shared links are right.

```yaml
services:
  nextproject:
    image: ghcr.io/real-ttx/nextproject:latest
    container_name: nextproject
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    expose:
      - "2860"
    environment:
      NEXTPROJECT__Server__PublicUrl: https://projects.example.com
      NEXTPROJECT__Server__ForceSecureCookie: "true"
      NEXTPROJECT__Server__TimeZone: Europe/Berlin
      NEXTPROJECT__Database__Password: ${DB_PASSWORD}
    volumes:
      - nextproject-data:/data
    networks: [default, proxy]

  db:
    image: postgres:17
    container_name: nextproject-db
    restart: unless-stopped
    environment:
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 20
    volumes:
      - nextproject-db:/var/lib/postgresql/data

volumes:
  nextproject-data:
  nextproject-db:

networks:
  proxy:
    external: true
```

Put `DB_PASSWORD=...` into an `.env` file next to it. A Caddy proxy for it is two lines:

```
projects.example.com {
    reverse_proxy nextproject:2860
}
```

### 4. From source

```bash
docker compose up -d --build                              # release build of this folder
docker compose -f docker-compose.dev.yml up -d --build    # development stack with pgAdmin
```

### Settings that matter

The settings live in `/data/config/app.json` and are edited on the pages under *Einstellungen*. How the machine is deployed can be set by
environment variables, which win over the file and are never written back:

| Variable | Default | Meaning |
|---|---|---|
| `NEXTPROJECT_PORT` | `2860` | Port inside the container |
| `NEXTPROJECT_DATA` | `/data` | Data directory (settings, keys, files, backups) |
| `NEXTPROJECT__Database__Host` / `__Port` / `__Database` / `__Username` / `__Password` | `db`, `5432`, `nextproject`, `postgres`, `nextproject` | The PostgreSQL. The database is made on the first start |
| `NEXTPROJECT__Server__PublicUrl` | – | The address people use (links that are handed on) |
| `NEXTPROJECT__Server__TimeZone` | `Europe/Berlin` | Time zone of the installation (schedules, "today") |
| `NEXTPROJECT__Server__ForceSecureCookie` | `false` | `true` behind HTTPS |

### After the first sign-in

- **Mein Konto › Sicherheit**: set up the **authenticator app** (QR code, ten recovery codes). *Einstellungen › Sicherheit* can require it for administrators.
- **Einstellungen › Benutzer** and **Abteilungen**: add the team. The application sends no mail; give people their first password yourself.
- **Einstellungen › Backup**: a target and a plan. Do this on day one.
- Five wrong passwords lock an account for 15 minutes.

### The `/data` volume

```
/data
├─ config/app.json   the settings
├─ keys/             protect the cookies and the saved passwords: keep it in every backup
├─ files/            attachments (unless a NAS is chosen)
├─ backups/          local backup targets and safety copies
└─ tmp/
```

Lose `keys/` and everybody has to sign in again; saved NAS passwords have to be entered again. More in [docs/OPERATIONS.md](docs/OPERATIONS.md).

## Development

```powershell
./scripts/redeploy.ps1          # rebuild the image and redeploy the dev stack: web 2860, pgAdmin 2861, Postgres 12860
./scripts/redeploy.ps1 -Fresh   # the same with empty volumes (setup page again)
dotnet build NextProject.slnx
dotnet test NextProject.slnx    # integration tests need NEXTPROJECT_TEST_DB (and NEXTPROJECT_TEST_SMB), see CLAUDE.md
```

```bash
export NEXTPROJECT_TEST_DB='Host=localhost;Port=12860;Username=postgres;Password=nextproject'
dotnet test NextProject.slnx    # without the variable the integration tests are skipped (and say so)
```

The NAS tests run against a real Samba server (the GitHub action starts one). Notes for working in the repository: [CLAUDE.md](CLAUDE.md).

## How it is built

- **ASP.NET Core 10** (Razor Pages), **EF Core** on **PostgreSQL 17**, enums stored as text, soft delete, audit columns on every table
- The interface uses the **MatDesign** toolkit ([MatUi](src/NextProject/wwwroot/ui)) for the basics (shell, forms, tables, dialogs, the schedule) plus our
  own bold components in `np-ui.css` ([what MatDesign lacks](docs/DESIGN-GAPS.md)). Plain CSS and JavaScript, no framework, no build step
- Progress is worked out in one place (`Progress`, `PlanningService`); status changes go through `TodoService`, which writes the history
- Sign-in with cookies and sessions in the database, TOTP (RFC 6238), passwords hashed; share links with a password gate
- Backup with AES-GCM and PBKDF2 (optional), SMB through SMBLibrary, tests against PostgreSQL and a real Samba server
- Strict content security policy: no inline scripts or styles

More: [features and rules of progress](docs/PROGRESS.md) · [operation](docs/OPERATIONS.md) · [what MatDesign lacks](docs/DESIGN-GAPS.md)

## Branches & versioning

| Branch | Purpose | Version |
|---|---|---|
| `main` | Release | `<major>.<minor>.<build>-<yyyyMMdd>` |
| `dev` | Development | `nightly-<build>-<yyyyMMdd>` |
| local | – | `local-<yyyyMMdd>` |

The build number comes from the GitHub action (`github.run_number`). Images are published to the GitHub Container Registry.
