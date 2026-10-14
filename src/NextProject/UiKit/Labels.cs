using NextProject.Data.Entities;
using Microsoft.AspNetCore.Html;

namespace NextProject.UiKit;

/// <summary>The German words for the enums of the data model, and the tone (colour) of a status pill.</summary>
public static class Labels
{
    // ---- todos

    public static string Of(TodoStatus s) => s switch
    {
        TodoStatus.Open => "Offen",
        TodoStatus.InProgress => "In Arbeit",
        TodoStatus.Blocked => "Blockiert",
        TodoStatus.InReview => "In Prüfung",
        TodoStatus.Done => "Erledigt",
        _ => "Verworfen",
    };

    public static string Tone(TodoStatus s) => s switch
    {
        TodoStatus.InProgress => "busy",
        TodoStatus.Blocked => "error",
        TodoStatus.InReview => "info",
        TodoStatus.Done => "ok",
        TodoStatus.Discarded => "plain",
        _ => "",
    };

    public static string Icon(TodoStatus s) => s switch
    {
        TodoStatus.Open => "circle",
        TodoStatus.InProgress => "play",
        TodoStatus.Blocked => "lock",
        TodoStatus.InReview => "eye",
        TodoStatus.Done => "check-circle",
        _ => "x-circle",
    };

    /// <summary>What the status says in a sentence: the legend of the workflow.</summary>
    public static string Meaning(TodoStatus s) => s switch
    {
        TodoStatus.Open => "Erfasst, aber noch nicht begonnen.",
        TodoStatus.InProgress => "Die Aufgabe wird bearbeitet.",
        TodoStatus.Blocked => "Ein Hindernis verhindert die weitere Bearbeitung.",
        TodoStatus.InReview => "Das Ergebnis wartet auf eine Prüfung oder Freigabe.",
        TodoStatus.Done => "Die Aufgabe wurde erfolgreich abgeschlossen.",
        _ => "Die Aufgabe wird nicht mehr benötigt.",
    };

    /// <summary>The colour of a status as a tile: the same story as the chip (in progress blue, blocked red, in review purple, done green).</summary>
    public static string KpiTone(TodoStatus s) => s switch
    {
        TodoStatus.InProgress => "busy",
        TodoStatus.Blocked => "error",
        TodoStatus.InReview => "info",
        TodoStatus.Done => "ok",
        _ => "plain",
    };

    public static string Of(Level l) => l switch
    {
        Level.Low => "Niedrig",
        Level.High => "Hoch",
        _ => "Mittel",
    };

    public static string Tone(Level l) => l switch
    {
        Level.High => "error",
        Level.Medium => "warn",
        _ => "plain",
    };

    // ---- projects, goals

    public static string Of(ProjectStatus s) => s switch
    {
        ProjectStatus.Planning => "Planung",
        ProjectStatus.InProgress => "In Umsetzung",
        ProjectStatus.AtRisk => "Gefährdet",
        ProjectStatus.Paused => "Pausiert",
        ProjectStatus.Completed => "Abgeschlossen",
        _ => "Abgebrochen",
    };

    public static string Tone(ProjectStatus s) => s switch
    {
        ProjectStatus.Planning => "info",
        ProjectStatus.InProgress => "busy",
        ProjectStatus.AtRisk => "warn",
        ProjectStatus.Completed => "ok",
        _ => "plain",
    };

    public static string Of(GoalStatus s) => s switch
    {
        GoalStatus.Planned => "Geplant",
        GoalStatus.Active => "Aktiv",
        GoalStatus.Achieved => "Erreicht",
        _ => "Verworfen",
    };

    public static string Tone(GoalStatus s) => s switch
    {
        GoalStatus.Planned => "info",
        GoalStatus.Active => "busy",
        GoalStatus.Achieved => "ok",
        _ => "plain",
    };

    public static string Of(RiskStatus s) => s switch
    {
        RiskStatus.Open => "Offen",
        RiskStatus.Mitigated => "Abgeschwächt",
        RiskStatus.Occurred => "Eingetreten",
        _ => "Geschlossen",
    };

    public static string Tone(RiskStatus s) => s switch
    {
        RiskStatus.Occurred => "error",
        RiskStatus.Open => "warn",
        RiskStatus.Mitigated => "info",
        _ => "plain",
    };

    public static string Of(ReviewHealth h) => h switch
    {
        ReviewHealth.OnTrack => "Im Plan",
        ReviewHealth.Watch => "Beobachten",
        _ => "Abweichung",
    };

    public static string Tone(ReviewHealth h) => h switch
    {
        ReviewHealth.OnTrack => "ok",
        ReviewHealth.Watch => "warn",
        _ => "error",
    };

    public static string Of(HistoryKind k) => k switch
    {
        HistoryKind.Created => "Angelegt",
        HistoryKind.Status => "Status",
        HistoryKind.Owner => "Verantwortung",
        HistoryKind.DueDate => "Termin",
        HistoryKind.Priority => "Priorität",
        HistoryKind.Comment => "Kommentar",
        HistoryKind.Deleted => "Gelöscht",
        _ => "Änderung",
    };

    public static string Icon(HistoryKind k) => k switch
    {
        HistoryKind.Created => "plus",
        HistoryKind.Status => "refresh",
        HistoryKind.Owner => "user",
        HistoryKind.DueDate => "calendar",
        HistoryKind.Priority => "alert",
        HistoryKind.Comment => "message-square",
        HistoryKind.Deleted => "trash",
        _ => "edit",
    };

    // ---- the rest

    public static string Of(UserRole r) => r switch
    {
        UserRole.Admin => "Administrator",
        _ => "Benutzer",
    };

    public static string Of(BackupScope s) => s == BackupScope.SystemOnly ? "System (Datenbank und Einstellungen)" : "System und Anhänge";

    public static string Of(ScheduleKind k) => k switch
    {
        ScheduleKind.Hourly => "Stündlich",
        ScheduleKind.Daily => "Täglich",
        ScheduleKind.Weekly => "Wöchentlich",
        _ => "Monatlich",
    };

    public static string Of(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Montag",
        DayOfWeek.Tuesday => "Dienstag",
        DayOfWeek.Wednesday => "Mittwoch",
        DayOfWeek.Thursday => "Donnerstag",
        DayOfWeek.Friday => "Freitag",
        DayOfWeek.Saturday => "Samstag",
        _ => "Sonntag",
    };

    // ---- pills

    public static IHtmlContent Pill(TodoStatus s) => Ui.Pill(Of(s), Tone(s));

    public static IHtmlContent Pill(Level l) => Ui.Pill(Of(l), Tone(l));

    public static IHtmlContent Pill(ProjectStatus s) => Ui.Pill(Of(s), Tone(s));

    public static IHtmlContent Pill(GoalStatus s) => Ui.Pill(Of(s), Tone(s));

    public static IHtmlContent Pill(RiskStatus s) => Ui.Pill(Of(s), Tone(s));

    public static IHtmlContent Pill(ReviewHealth h) => Ui.Pill(Of(h), Tone(h));
}
