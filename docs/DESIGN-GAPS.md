# Design gaps: what NextProject draws itself

The customer wants the app **bold** ("plakativ"), like their mock-ups (dark menu, filled chips, big numbers, cards with a picture of their state, a board with dark heads).
MatDesign has no design for most of that, and some of it bends its rules (the look is flat on purpose there). Until MatDesign decides, NextProject draws these things
itself in `src/NextProject/wwwroot/css/np-ui.css` (classes `np-*`) and uses MatUi only for what it does well: the shell and its menu behaviour, forms, buttons, tables,
tabs, dialogs, pickers, toasts and the schedule. This file lists what to look at later: one entry per thing, what it is, where it is, and what MatDesign could take over.

## Rules of MatDesign that this app bends (on purpose, for the bold look)

| Rule / principle | What NextProject does instead |
|---|---|
| Flat look: borders instead of shadows | Soft shadows on panels, tiles and cards (`--np-shadow`), a stronger one on hover |
| "No icon tile next to a title" (`SHELL-09`) | A round icon in front of every panel title, a tinted square tile on project and goal cards |
| Sentence-case labels | Upper-case small headings in table heads and menu groups |
| Quiet, tinted status pills (`mat-pill`) | Filled chips with white (or dark) text, seven tones (`np-chip`) |
| Light menu from the tokens of the theme | A dark menu in both themes, the active item filled with the accent |
| Small radii | 16 px panels, 12 px buttons, 22 px hero |
| Accent only for actions and selection | The whole sign-in page and the dashboard hero are the accent colour |

## Components

### 1. Dark menu (`.mat-nav` token override)
The menu sets its own surface and text tokens inside `.mat-nav` (dark burgundy `#1d0a10`, white text, the active item in the accent colour with a glow). Menu behaviour, badges,
submenus and the switcher stay MatUi. The count badge (`mat-nav__badge`) is a filled amber chip, white with a dark number on the active item: the tinted toolkit badge disappeared on the
dark menu and on the cherry item. *Proposal:* a `data-nav="dark"` variant of the shell, with the contrast checks for the tokens it sets (`COLOR-09` style).

### 2. Panel (`np-panel`, tag helper `np-card`)
A card with a head: a round tinted icon, a bold title, a quiet line at the right end. Replaces `mat-card` + `mat-card__head`. *Proposal:* a "Panel" in the catalogue.

### 3. KPI tile (`np-kpi`, `Ui.Kpi`)
A round tinted picture, a big number (2.15 rem, 800), a bold label and a note (red when it is bad). Tones: accent, ok, busy, info, warn, error, plain. A tile can be a link.
Used on the dashboard, every project, goal, todo and report page and on shared pages. MatUi's `mat-stat` has no picture and no tone.

### 4. Filled chips (`np-chip`, `Ui.Pill`)
Status and priority as filled chips: `ok` green, `busy` blue, `info` purple, `error` red, `warn` amber (dark text), `accent`, `plain` and a quiet `soft`. All pass AA with their text
colour (white on `#1b7f4a` 5.0, on `#1f5fd1` 5.8, on `#6b46c1` 6.4, on `#b3261e` 6.5, on `#5b6270` 6.1; `#2b1700` on `#f6a21b` 8.3). The text is always there: colour is never the only sign.
*Proposal:* a second, filled style next to `mat-pill`, with tokens (`--mat-chip-*`), light and dark.
**Problem to decide:** cherry red (the accent) and the danger colour are close. The chips for blocked and high priority use a darker `#b3261e`; the word carries the meaning.

### 5. Hero (`np-hero`)
The greeting of the dashboard as a block in the accent colour: date, big name, one sentence, three actions (a white button and two outlined ones), the logo as a large faint mark.
*Proposal:* a "Hero / banner" for start pages, and the button pair on a coloured surface (`np-btn-light`, `np-btn-outline-light`).

### 6. Project and goal cards (`np-pcard`, `np-cards`), ring (`np-ring`, `Ui.Ring`)
A card with a tile whose colour says the state, a bold name, a status chip, two bars ("todos done" and "goal reached", side by side on purpose), three facts, the owner. The goal
card has a ring with the share in the middle. A switch **cards / list** (`np-viewtoggle`) sits above the toolbar; the cards are the standard.
*Proposal:* "Entity card" and "Ring" for the catalogue; the switch as a standard part of a list page (`LIST-*`).

### 7. Board (`_Board`, `np-board`)
Kanban columns with a solid coloured head (icon, name, count), cards that can be dragged to another column (HTML5, a select on every card for phone and keyboard),
done cards in green with a struck-through title, blocked cards with a red edge. Drag and drop: `app.js`; the server is `POST /api/todos/{id}/status`.
*Proposal:* a "Board" pattern with the keyboard and touch way written down.

### 8. Gantt: the schedule of the projects (`_Gantt`, `np-gantt`)
Projects below each other, the time from left to right (3, 6 or 12 months, month and week ticks), the share of finished todos as a darker part inside the bar, milestones as diamonds
on the bar, today as a dashed line, the names fixed at the left while the time scrolls, a project opens to show its todos. MatUi's schedule has no resource rows.
*Proposal:* a "Timeline / Gantt" view of the schedule (rows = resources or projects), with the accessible text in every row (the dates are written in the bar and in the label).
The small single-project timeline (`_Timeline`, `np-tl`) is an older, simpler variant of the same thing: take both together.

### 9. Workload table (`np-workload`, `np-load`)
Open todos per person and week; the number is written in a filled round chip whose colour says how full the week is (1–3 blue, 4–6 amber, 7+ red). *Proposal:* a heat/number cell.

### 10. Key result card (`np-krcard`, `np-scale`)
A big current value with its unit, a chip with the share reached, a bar from the start value to the target with both written under it, the owner and the project.

### 11. Link tabs (`_LinkTabs`) and the filled active tab
Tabs that are pages of their own (a scope of a list: today, this week, all, team; the three views of the calendar). The active tab is filled with the accent. *Proposal:* pills tabs as
links with `aria-current="page"` in the catalogue (MatUi has `mat-tabs--pills` with `is-active`; the filled look is ours).

### 12. Calendar behaviour on top of the MatUi schedule (`calendar.js`)
- A click on a todo opens a dialog (move to a date, finish, open) instead of following the link (`mat:schedule-select` is cancelled for todos).
- A todo dragged to another day in the month view moves its due date (the start date moves along); `POST /api/todos/{id}/due`.
- A click on a day sets the day of "Neues Todo" (`mat:schedule-day`).
- Filters in the toolbar above, a legend, an iCal subscription and a file download.
It relies on `root._matSchedule.events[i].raw` to find the event of a chip: **a public way to get the event of a node would be better** (`MatUi.schedule.eventOf(node)`).
*Proposal:* drag-to-reschedule as an option of the schedule (`data-movable`), with a keyboard way.

### 13. Sign-in page
The whole page in the accent colour, one white card with a large radius and a deep shadow.

### 14. Smaller things
Toolbar and table with the larger radius and the shadow; table heads upper-case; buttons bolder; headings 800 weight and tighter; the thick progress bar (10 px) with its number
(`Ui.Meter`, `np-meter`); timeline of events (`np-timeline`) with round icons; scrollbars that are only visible while scrolling.
Icons added to the sprite (`np-icons.svg`): `np-logo`, `target`, `flag`, `trending-up`, `paperclip`, `list-checks`, `columns`, `history`, `share`, `user-plus`, `circle`, `lock-open`, `send`, `undo`.

## What is still missing in the toolkit (needed by the app)
- A multi-select picker that posts the keys (works, but the options need a description line: done with the group name of a `SelectListItem`).
- A way to ask the schedule for the event of a node (see 12).
- Dark-mode values for the chip colours (they are saturated enough to work on both, but nobody has looked at them in `contrast` and `oled`).
