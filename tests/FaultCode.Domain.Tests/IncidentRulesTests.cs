using FaultCode.Domain;

namespace FaultCode.Domain.Tests;

public sealed class IncidentRulesTests
{
    [Fact]
    public void Full_transition_matrix_has_only_declared_edges()
    {
        var statuses = new[] { "Received", "Investigating", "Escalated", "Resolved", "NotificationPending", "Notified" };
        var edges = new[] { "Received/Investigating", "Investigating/Escalated", "Investigating/Resolved", "Escalated/Investigating", "Escalated/Resolved", "Resolved/NotificationPending", "NotificationPending/Notified" };
        foreach (var from in statuses) foreach (var to in statuses) Assert.Equal(edges.Contains(from + "/" + to), IncidentRules.CanTransition(from, to));
        Assert.False(IncidentRules.CanTransition("unknown", "Received"));
    }
    [Fact]
    public void Occurrence_and_source_are_bounded_UTC_and_ascii()
    {
        var now = DateTimeOffset.Parse("2026-10-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(IncidentRules.Occurrence(now, now)); Assert.True(IncidentRules.Occurrence(now.AddDays(-7), now));
        Assert.False(IncidentRules.Occurrence(now.AddTicks(1), now)); Assert.False(IncidentRules.Occurrence(now.AddDays(-7).AddTicks(-1), now));
        Assert.False(IncidentRules.Occurrence(now.ToOffset(TimeSpan.FromHours(-4)), now));
        foreach (var token in new[] { "", "bad source", "bad/source", "é", new string('a', 101) }) Assert.False(IncidentRules.Token(token));
        Assert.True(IncidentRules.Token("SYN_event-1"));
    }
}
