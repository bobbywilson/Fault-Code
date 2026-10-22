using FaultCode.Api.Authorization;
using FaultCode.Api.Identity;
using FaultCode.Application.Review;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace FaultCode.Api.Review;

public static class TicketReviewEndpoints
{
    public static void MapTicketReview(this WebApplication app)
    {
        var policy = new AuthorizationPolicyBuilder(DemoSession.Scheme, JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.Find("faults.read")!, CallerKind.Delegated)).Build();
        app.MapGet("/workspace/tickets", async (HttpContext c, ITicketReviewSource source) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            var refresh = c.Request.Query["refresh"] == "true";
            try { return Results.Json(await source.List(refresh, c.RequestAborted)); }
            catch (TicketReviewException e) { return Results.Problem(statusCode: e.Status, title: "Tickets could not be retrieved."); }
        }).RequireAuthorization(policy).ExcludeFromDescription();
        app.MapGet("/workspace/tickets/{id:guid}", async (Guid id, HttpContext c, ITicketReviewSource source) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            try { return Results.Json(await source.Get(id, c.RequestAborted)); }
            catch (TicketReviewException e) { return Results.Problem(statusCode: e.Status, title: e.Status == 404 ? "Ticket not found." : "Ticket could not be retrieved."); }
        }).RequireAuthorization(policy).ExcludeFromDescription();
    }
}
