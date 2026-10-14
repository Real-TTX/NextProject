using NextProject.Data;
using NextProject.Data.Entities;
using NextProject.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace NextProject.Services;

/// <summary>
/// Sample data for trying the software out and for the screenshots of the documentation: a small company with departments, people, two goals with measured key
/// results, projects, milestones, risks, a review and todos in every status. It is only written into an empty installation, never over real data.
/// </summary>
public sealed class DemoData(NpDbContext db, UserService users, TimeProvider clock)
{
    public const string Password = "Beispiel-Passwort-2026";

    public async Task<bool> IsEmptyAsync(CancellationToken ct = default)
        => !await db.Goal.AnyAsync(ct) && !await db.Project.AnyAsync(ct) && !await db.Todo.AnyAsync(ct) && !await db.Department.AnyAsync(ct);

    /// <summary>Writes the sample data; returns false (and writes nothing) when the installation already holds goals, projects, todos or departments.</summary>
    public async Task<bool> CreateAsync(long adminId, CancellationToken ct = default)
    {
        if (!await IsEmptyAsync(ct))
        {
            return false;
        }

        var today = Fmt.Today(clock);
        DateOnly D(int days) => today.AddDays(days);

        var sales = new Department { Name = "Vertrieb", Description = "Kundenbetreuung und Verkauf" };
        var dev = new Department { Name = "Entwicklung", Description = "Software und Schnittstellen" };
        var admin = new Department { Name = "Verwaltung", Description = "Organisation und Finanzen" };
        db.Department.AddRange(sales, dev, admin);
        await db.SaveChangesAsync(ct);

        var boss = await db.User.FirstAsync(u => u.Id == adminId, ct);
        boss.JobTitle ??= "Geschäftsführung";
        boss.DepartmentId ??= admin.Id;
        var max = await users.CreateAsync("maximilian.schmidt@example.test", "Maximilian Schmidt", UserRole.User, Password, ct);
        var tom = await users.CreateAsync("tom.weber@example.test", "Tom Weber", UserRole.User, Password, ct);
        var anna = await users.CreateAsync("anna.fischer@example.test", "Anna Fischer", UserRole.User, Password, ct);
        var mandy = await users.CreateAsync("mandy.roth@example.test", "Mandy Roth", UserRole.User, Password, ct);
        (max.JobTitle, max.DepartmentId) = ("Leitung Entwicklung", dev.Id);
        (tom.JobTitle, tom.DepartmentId) = ("Leitung Kundenservice", sales.Id);
        (anna.JobTitle, anna.DepartmentId) = ("Vertriebsleitung", sales.Id);
        (mandy.JobTitle, mandy.DepartmentId) = ("Kundenservice", sales.Id);
        await db.SaveChangesAsync(ct);

        // ---- goals and key results
        var support = new Goal
        {
            Title = "Kundenanrufe zur Bestellverfolgung reduzieren", Year = today.Year, OwnerUserId = tom.Id, DepartmentId = sales.Id, Status = GoalStatus.Active,
            Description = "Jeder Anruf zur Bestellverfolgung bindet Kapazität im Support. Wenn Kunden den Stand selbst sehen, sinkt die Last und die Zufriedenheit steigt.",
            YearTarget = "Die Hälfte der Anrufe zur Bestellverfolgung entfällt.",
            Q1Target = "Schnittstelle zu den Lieferanten steht.", Q2Target = "Alle großen Lieferanten liefern Daten.", Q3Target = "Kunden kennen und nutzen die Funktion.", Q4Target = "Anrufe um 50 % gesenkt.",
        };
        var growth = new Goal
        {
            Title = "Neukundengeschäft im Mittelstand ausbauen", Year = today.Year, OwnerUserId = anna.Id, DepartmentId = sales.Id, Status = GoalStatus.Active,
            Description = "Wachstum soll aus Kunden kommen, die zu uns passen: Mittelstand mit wiederkehrendem Bedarf.",
            YearTarget = "Zwölf neue Rahmenverträge.", Q1Target = "Zielkunden und Ansprache stehen.", Q2Target = "Vier Verträge.", Q3Target = "Acht Verträge.", Q4Target = "Zwölf Verträge.",
        };
        var processes = new Goal
        {
            Title = "Interne Prozesse digitalisieren", Year = today.Year, OwnerUserId = boss.Id, DepartmentId = admin.Id, Status = GoalStatus.Planned,
            Description = "Weniger Papier und Excel, mehr Verbindlichkeit und Überblick.", YearTarget = "Alle Projekte laufen in einem System.",
        };
        db.Goal.AddRange(support, growth, processes);
        await db.SaveChangesAsync(ct);

        // ---- projects
        var tracking = new Project
        {
            Name = "Bestellverfolgung verbessern", OwnerUserId = max.Id, DepartmentId = dev.Id, Status = ProjectStatus.InProgress, Priority = Level.High,
            StartDate = D(-80), EndDate = D(60), Budget = 48000m, Resources = "120 Personentage",
            Description = "Kunden sehen den Stand ihrer Bestellung selbst; Lieferanten melden Status automatisch.",
            Objective = "Die Anrufe zur Bestellverfolgung halbieren.", ExpectedResult = "Eine Schnittstelle, über die Lieferanten den Status liefern, und eine Statusseite für Kunden.",
            AddedValue = "Weniger Last im Support, zufriedenere Kunden.", InfluenceFactors = "+ Lieferanten sind bereit. − Ressourcen für die Schnittstelle sind knapp.",
            StatusNote = "Die Schnittstelle läuft im Test. Zwei Lieferanten fehlen noch.",
        };
        var campaign = new Project
        {
            Name = "Marketingkampagne Q4", OwnerUserId = anna.Id, DepartmentId = sales.Id, Status = ProjectStatus.InProgress, Priority = Level.Medium,
            StartDate = D(-40), EndDate = D(75), Budget = 15000m, Resources = "30 Personentage",
            Description = "Die Kampagne zur Bekanntheit der Statusseite und zur Ansprache neuer Mittelstandskunden.",
            Objective = "Markenbekanntheit im Mittelstand steigern.", ExpectedResult = "Fünf qualifizierte Anfragen pro Woche.", AddedValue = "Neue Kunden aus der Zielgruppe.",
        };
        var onboarding = new Project
        {
            Name = "Onboarding neuer Mitarbeiter", OwnerUserId = boss.Id, DepartmentId = admin.Id, Status = ProjectStatus.Planning, Priority = Level.Low,
            StartDate = D(20), EndDate = D(140), Description = "Ein fester Ablauf für die ersten vier Wochen.", Objective = "Neue Mitarbeiter sind nach vier Wochen produktiv.",
        };
        var digital = new Project
        {
            Name = "Digitalisierung Vertrieb", OwnerUserId = anna.Id, DepartmentId = sales.Id, Status = ProjectStatus.AtRisk, Priority = Level.High,
            StartDate = D(-100), EndDate = D(-5), Budget = 22000m, Resources = "60 Personentage",
            Description = "Angebote, Verträge und Nachfassen laufen in einem System.", Objective = "Kein Angebot geht mehr verloren.",
            StatusNote = "Der Termin ist überschritten, die Freigabe der Verträge steht aus.",
        };
        db.Project.AddRange(tracking, campaign, onboarding, digital);
        await db.SaveChangesAsync(ct);
        db.ProjectGoal.AddRange(
            new ProjectGoal { ProjectId = tracking.Id, GoalId = support.Id }, new ProjectGoal { ProjectId = campaign.Id, GoalId = growth.Id },
            new ProjectGoal { ProjectId = campaign.Id, GoalId = support.Id }, new ProjectGoal { ProjectId = digital.Id, GoalId = growth.Id },
            new ProjectGoal { ProjectId = digital.Id, GoalId = processes.Id }, new ProjectGoal { ProjectId = onboarding.Id, GoalId = processes.Id });
        db.ProjectMember.AddRange(new ProjectMember { ProjectId = tracking.Id, UserId = boss.Id }, new ProjectMember { ProjectId = campaign.Id, UserId = boss.Id }, new ProjectMember { ProjectId = tracking.Id, UserId = tom.Id }, new ProjectMember { ProjectId = tracking.Id, UserId = mandy.Id },
            new ProjectMember { ProjectId = campaign.Id, UserId = mandy.Id }, new ProjectMember { ProjectId = digital.Id, UserId = tom.Id });
        db.ProjectDependency.Add(new ProjectDependency { ProjectId = campaign.Id, DependsOnProjectId = tracking.Id });

        var m1 = new Milestone { ProjectId = tracking.Id, Title = "Schnittstelle im Test", DueDate = D(-25), ReachedDate = D(-27), Description = "Die ersten Daten fließen vom Testsystem." };
        var m2 = new Milestone { ProjectId = tracking.Id, Title = "Alle großen Lieferanten angebunden", DueDate = D(20), Description = "Die zehn größten Lieferanten liefern Daten." };
        var m3 = new Milestone { ProjectId = tracking.Id, Title = "Statusseite für Kunden live", DueDate = D(55) };
        var m4 = new Milestone { ProjectId = campaign.Id, Title = "Kampagnenstart", DueDate = D(10) };
        var m5 = new Milestone { ProjectId = digital.Id, Title = "Verträge freigegeben", DueDate = D(-12) };
        db.Milestone.AddRange(m1, m2, m3, m4, m5);
        db.Risk.AddRange(
            new Risk { ProjectId = tracking.Id, Title = "Lieferanten-Onboarding verzögert sich", Description = "Ein Lieferant braucht länger für die Anpassung seiner Systeme.", Severity = Level.High, Status = RiskStatus.Open, Mitigation = "Wöchentlicher Abgleich, Ausweichlösung per Datei-Upload.", OwnerUserId = max.Id },
            new Risk { ProjectId = tracking.Id, Title = "Ressourcen für die Schnittstelle knapp", Severity = Level.Medium, Status = RiskStatus.Mitigated, Mitigation = "Zwei externe Tage pro Woche eingekauft.", OwnerUserId = max.Id },
            new Risk { ProjectId = campaign.Id, Title = "Budget für Anzeigen reicht nicht", Severity = Level.Low, Status = RiskStatus.Open, OwnerUserId = anna.Id });
        await db.SaveChangesAsync(ct);

        var calls = new KeyResult
        {
            GoalId = support.Id, ProjectId = tracking.Id, Title = "Die Anrufe der Kunden zur Bestellverfolgung sinken auf 25 pro Woche", OwnerUserId = mandy.Id,
            Metric = "Anrufe pro Woche", Collection = "Strichliste", Unit = "Anrufe", StartValue = 50, TargetValue = 25, DueDate = D(90),
        };
        var suppliers = new KeyResult
        {
            GoalId = support.Id, ProjectId = tracking.Id, Title = "Zehn Lieferanten melden ihren Status automatisch", OwnerUserId = max.Id,
            Metric = "Angebundene Lieferanten", Collection = "Liste der Schnittstelle", Unit = "Lieferanten", StartValue = 0, TargetValue = 10, DueDate = D(20),
        };
        var contracts = new KeyResult
        {
            GoalId = growth.Id, ProjectId = campaign.Id, Title = "Zwölf neue Rahmenverträge im Mittelstand", OwnerUserId = anna.Id,
            Metric = "Unterschriebene Rahmenverträge", Collection = "CRM", Unit = "Verträge", StartValue = 0, TargetValue = 12, DueDate = D(150),
        };
        db.KeyResult.AddRange(calls, suppliers, contracts);
        await db.SaveChangesAsync(ct);
        db.KeyResultCheckIn.AddRange(
            new KeyResultCheckIn { KeyResultId = calls.Id, CheckInDate = D(-90), Value = 50, DonePercent = 0, Comment = "Ausgangswert vor dem Projekt." },
            new KeyResultCheckIn { KeyResultId = calls.Id, CheckInDate = D(-60), Value = 48, DonePercent = 15, Comment = "Noch keine Wirkung: die Schnittstelle fehlt." },
            new KeyResultCheckIn { KeyResultId = calls.Id, CheckInDate = D(-30), Value = 41, DonePercent = 40, Comment = "Erste Lieferanten liefern, Kunden fragen weniger." },
            new KeyResultCheckIn { KeyResultId = calls.Id, CheckInDate = D(-3), Value = 36, DonePercent = 55 },
            new KeyResultCheckIn { KeyResultId = suppliers.Id, CheckInDate = D(-30), Value = 3, DonePercent = 40 },
            new KeyResultCheckIn { KeyResultId = suppliers.Id, CheckInDate = D(-3), Value = 6, DonePercent = 55 },
            new KeyResultCheckIn { KeyResultId = contracts.Id, CheckInDate = D(-20), Value = 1, DonePercent = 25 },
            new KeyResultCheckIn { KeyResultId = contracts.Id, CheckInDate = D(-2), Value = 3, DonePercent = 35 });

        db.Review.AddRange(
            new Review
            {
                ProjectId = tracking.Id, ReviewDate = D(-10), Health = ReviewHealth.Watch, NextReviewDate = D(18),
                Deviation = "Die Schnittstelle ist fertig, aber zwei Lieferanten brauchen länger: die Wirkung bleibt hinter dem Plan.",
                Learnings = "Lieferanten brauchen einen festen Ansprechpartner und ein einfaches Testsystem.",
                Adjustments = "Datei-Upload als Übergang für die beiden Nachzügler; wöchentlicher Abgleich.",
                Steering = "Das Review wird alle vier Wochen mit der Geschäftsführung gemacht.",
            },
            new Review { GoalId = growth.Id, ReviewDate = D(-18), Health = ReviewHealth.OnTrack, Deviation = "Keine Abweichung: die Ansprache funktioniert.", Learnings = "Referenzkunden überzeugen mehr als Anzeigen." });
        await db.SaveChangesAsync(ct);

        // ---- todos: every status, some with checklist, comments, dependencies
        Todo T(string title, User owner, TodoStatus status, Level prio, int? due, Project? project = null, KeyResult? kr = null, Milestone? ms = null, int? start = null, string? description = null)
        {
            var todo = new Todo
            {
                Title = title, OwnerUserId = owner.Id, Status = status, Priority = prio, DueDate = due is { } d ? D(d) : null, StartDate = start is { } s ? D(s) : null,
                ProjectId = project?.Id, KeyResultId = kr?.Id, MilestoneId = ms?.Id, Description = description,
            };
            if (status == TodoStatus.Done)
            {
                todo.CompletedDate = clock.GetUtcNow().UtcDateTime.AddDays((due ?? -2) - 1);
            }

            return todo;
        }

        var t1 = T("Technische Umsetzung der Schnittstelle", max, TodoStatus.InProgress, Level.High, 0, tracking, calls, m2, -30, "Die technische Umsetzung der Schnittstelle zur Bestellverfolgung muss umgesetzt werden. Dabei sind die Anforderungen der Lieferanten und die Pflichtfelder zu berücksichtigen.");
        t1.Checklist = [new() { Text = "Datenmodell abstimmen", IsDone = true, SortOrder = 1 }, new() { Text = "Endpunkt für Statusmeldungen bauen", IsDone = true, SortOrder = 2 }, new() { Text = "Pflichtfelder prüfen", SortOrder = 3 }, new() { Text = "Testsystem für Lieferanten bereitstellen", SortOrder = 4 }];
        t1.Contributors = [new() { UserId = tom.Id }, new() { UserId = mandy.Id }];
        t1.Comments = [new() { Text = "Die Schnittstelle läuft nach Plan. Aktuell werden die Tests vorbereitet.", CreateUserId = max.Id, CreateDate = clock.GetUtcNow().UtcDateTime.AddDays(-3) }];
        var t2 = T("Anforderungen mit den Lieferanten abstimmen", mandy, TodoStatus.Done, Level.High, -35, tracking, suppliers, m1, -60);
        var t3 = T("Lieferanten onboarden", mandy, TodoStatus.Open, Level.High, 15, tracking, suppliers, m2, 0);
        var t4 = T("Kunden über die Statusseite informieren", tom, TodoStatus.Open, Level.Medium, 40, tracking, calls, m3);
        var t5 = T("Daten des Testlieferanten einspielen", max, TodoStatus.Blocked, Level.High, -2, tracking, suppliers, m2);
        t5.BlockReason = "Der Testlieferant hat noch keinen Zugang erhalten.";
        t5.BlockNeeds = "Eine Entscheidung der Geschäftsführung zum Datenschutzvertrag.";
        var t6 = T("Feedback der Kunden zur Kommunikation sammeln", tom, TodoStatus.InReview, Level.Medium, 3, tracking, calls);
        t6.ReviewerUserId = anna.Id;
        var t7 = T("Content-Plan für die Kampagne erstellen", anna, TodoStatus.InReview, Level.Medium, 1, campaign, null, m4);
        var t8 = T("Anzeigenmotive abstimmen", mandy, TodoStatus.InProgress, Level.Medium, 6, campaign, null, m4, -3);
        var t9 = T("Landingpage für die Kampagne bauen", max, TodoStatus.Open, Level.Medium, 9, campaign, contracts, m4);
        var t10 = T("Pressemitteilung schreiben", anna, TodoStatus.Done, Level.Low, -6, campaign);
        var t11 = T("Vertragsvorlagen mit der Rechtsabteilung klären", anna, TodoStatus.Open, Level.High, -9, digital, null, m5);
        var t12 = T("Teamworkshop organisieren", boss, TodoStatus.Open, Level.Medium, 12);
        var t13 = T("Reisekosten 2026 abrechnen", boss, TodoStatus.Open, Level.Low, 4);
        var t14 = T("Onboarding-Checkliste entwerfen", boss, TodoStatus.Open, Level.Low, 45, onboarding);
        var t15 = T("Statusbericht für die Geschäftsführung", boss, TodoStatus.InProgress, Level.Medium, 0, null, null, null, -2);
        var t17 = T("Datenschutzvertrag mit dem Testlieferanten freigeben", boss, TodoStatus.InProgress, Level.High, 0, tracking, suppliers, m2, -4, "Der Testlieferant wartet auf die Freigabe des Vertrags zur Auftragsverarbeitung.");
        var t18 = T("Budget für die Kampagne freigeben", boss, TodoStatus.Open, Level.Medium, 2, campaign, null, m4);
        var t16 = T("Altes Kundenportal abschalten", max, TodoStatus.Discarded, Level.Low, null, tracking);
        t16.Result = "Wird nicht mehr gebraucht: die Statusseite ersetzt es.";
        db.Todo.AddRange(t17, t18, t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15, t16);
        await db.SaveChangesAsync(ct);
        db.TodoDependency.AddRange(new TodoDependency { TodoId = t4.Id, DependsOnTodoId = t3.Id }, new TodoDependency { TodoId = t3.Id, DependsOnTodoId = t1.Id });
        db.HistoryEntry.AddRange(
            new HistoryEntry { EntityType = HistoryEntity.Todo, EntityId = t1.Id, Kind = HistoryKind.Created, OccurredDate = clock.GetUtcNow().UtcDateTime.AddDays(-30), UserName = boss.DisplayName, UserId = boss.Id },
            new HistoryEntry { EntityType = HistoryEntity.Todo, EntityId = t1.Id, Kind = HistoryKind.Status, OldValue = "Offen", NewValue = "In Arbeit", OccurredDate = clock.GetUtcNow().UtcDateTime.AddDays(-28), UserName = max.DisplayName, UserId = max.Id },
            new HistoryEntry { EntityType = HistoryEntity.Todo, EntityId = t1.Id, Kind = HistoryKind.DueDate, OldValue = Fmt.Date(D(-7)), NewValue = Fmt.Date(D(0)), OccurredDate = clock.GetUtcNow().UtcDateTime.AddDays(-9), UserName = max.DisplayName, UserId = max.Id },
            new HistoryEntry { EntityType = HistoryEntity.Project, EntityId = digital.Id, Kind = HistoryKind.Status, OldValue = "In Umsetzung", NewValue = "Gefährdet", OccurredDate = clock.GetUtcNow().UtcDateTime.AddDays(-6), UserName = anna.DisplayName, UserId = anna.Id });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
