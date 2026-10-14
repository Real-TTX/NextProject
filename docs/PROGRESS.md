# The rules of progress

Every number about progress comes from `Core/Planning/Progress.cs` and `Services/PlanningService.cs`, and has a test. A page never works out
progress for itself.

## Four things that are not the same

| It was ... | ... means | Where it is written |
|---|---|---|
| a **todo** is finished | somebody said "done" (status *Erledigt*, with the time of completion) | `Todo.Status`, `Todo.CompletedDate` |
| a **milestone** is reached | somebody confirmed it with a date. Finished todos are a hint, not the proof | `Milestone.ReachedDate` |
| a **project** is successful | the owner closes it (*Abgeschlossen*); the key results show whether it delivered | `Project.Status` |
| a **goal** is reached | its key results are on their way to the target | `KeyResult.Attainment`, average per goal |

## Numbers

- **Completion of a project** (indicator): finished todos / (all todos − discarded). A project without todos has no completion.
- **Attainment of a key result**: `(current − start) / (target − start)`, between 0 and 1. It works for a value that has to go down
  (50 → 25 calls a week). *Current* is the value of the newest check-in, or the start value as long as nothing was measured. Start and
  target must differ.
- **Attainment of a goal**: the average of its key results. A goal without a key result is **not measured**, which is shown as such
  and never as "0 %".
- **Check-in**: the measured value on a day, and the share of the todos linked to the key result that were done on that day. So the
  planning sheet's two columns ("key action done %" and "effect of the key action %") can be compared later.
- **Overdue** is not a status: a todo is overdue when it has a due date before today and is not done and not discarded.

## What the software points out about a project

Not for a project that is *Abgeschlossen*, *Abgebrochen* or *Pausiert*. Thresholds are set under *Einstellungen › Allgemein*.

| Signal | Tone |
|---|---|
| the end date has passed | error |
| overdue todos at the limit (default 3) or more | error |
| fewer overdue todos | warning |
| blocked todos | warning |
| a milestone that is late and not reached | warning |
| a high risk that is open or has occurred | warning |
| with start and end date: the share of finished todos is more than 25 points behind the share of the time that is over (at least 3 todos) | warning |

A project is *endangered* when it has an error. The status itself stays what the owner set ("Gefährdet" is a decision, not a calculation).

## The todo workflow

*Offen* → *In Arbeit* → *In Prüfung* → *Erledigt*, with *Blockiert* and *Verworfen* on the side.

- *Blockiert* needs a reason (and may say what help or decision is needed); both are shown to everybody who sees the todo and stay in the history.
- *Erledigt* is refused while a todo it depends on is open. A todo that waits for itself, directly or in a circle, is refused.
- Taking a finished or discarded todo up again clears the time of completion.
- Status, owner, due date and priority changes are written to the history with who did it and when.
