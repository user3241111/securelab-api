
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentCommentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentCommentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/incident-comments/{id:guid}",
                GetCommentAsync)
            .WithName("GetIncidentComment")
            .WithTags("Incident Comments")
            .RequireAuthorization()
            .Produces<IncidentCommentResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetCommentAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Отримуємо коментар разом з інформацією
        // про власника батьківського інциденту.
        var comment = await dbContext.IncidentComments
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                OwnerUserId = item.Incident.OwnerUserId,
                item.IsInternal,
                Response = new IncidentCommentResponse(
                    item.Id,
                    item.Author.DisplayName,
                    item.Text,
                    item.CreatedAtUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (comment is null)
        {
            return Results.Problem(
                title: "Коментар не знайдено",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Перевіряємо права через батьківський інцидент
        // та ознаку внутрішнього коментаря.
        if (!IncidentAccessRules.CanReadComment(
                httpContext.User,
                comment.OwnerUserId,
                comment.IsInternal))
        {
            return Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(comment.Response);
    }
}
