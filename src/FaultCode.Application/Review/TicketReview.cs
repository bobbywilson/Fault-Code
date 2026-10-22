namespace FaultCode.Application.Review;

public sealed record ReviewTicket(Guid Id, string Identifier, string Title, string Status, string StatusType,
    string Priority, string? Assignee, string[] Labels, string? Project, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string? DueDate, DateTimeOffset? CompletedAt, string Url);
public sealed record ReviewTicketReference(Guid Id, string Identifier, string Title, string Status, string Relationship);
public sealed record ReviewTicketComment(Guid Id, string? Author, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReviewTicketAttachment(string Title, string? Subtitle, string Url);
public sealed record ReviewTicketDetail(ReviewTicket Ticket, string? Description, string? Creator, string? Cycle, string? Milestone,
    decimal? Estimate, ReviewTicketReference? Parent, ReviewTicketReference[] Subtasks, ReviewTicketReference[] Related,
    ReviewTicketComment[] Comments, ReviewTicketAttachment[] Attachments, DateTimeOffset RetrievedAt);
public sealed record ReviewTicketList(ReviewTicket[] Items, DateTimeOffset RetrievedAt, string Team);
public interface ITicketReviewSource
{
    Task<ReviewTicketList> List(bool refresh, CancellationToken ct);
    Task<ReviewTicketDetail> Get(Guid id, CancellationToken ct);
}
public sealed class TicketReviewException(int status) : Exception
{ public int Status { get; } = status; }
