
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateContentAsync)
            .WithName("UpdateIncidentContent")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static IResult Forbidden()
    {
        return Results.Problem(
            title: "Forbidden",
            statusCode: StatusCodes.Status403Forbidden);
    }

    private static IResult NotFound()
    {
        return Results.Problem(
            title: "Інцидент не знайдено",
            statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;

        if (!IncidentAccessRules.CanReadList(user))
        {
            return Forbidden();
        }

        var isAnalyst = IncidentAccessRules.IsAnalyst(user);

        var currentUserId =
            IncidentAccessRules.GetCurrentUserId(user);

        if (!isAnalyst && !currentUserId.HasValue)
        {
            return Forbidden();
        }

        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(
                    status,
                    ignoreCase: true,
                    out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] =
                        [
                            "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
                        ]
                    });
            }

            parsedStatus = candidate;
        }

        var ownerFilter = isAnalyst
            ? (Guid?)null
            : currentUserId;

        var incidents = await queries.GetListAsync(
            parsedStatus,
            cancellationToken,
            ownerFilter);

        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => (Guid?)incident.OwnerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!ownerUserId.HasValue)
        {
            return NotFound();
        }

        if (!IncidentAccessRules.CanRead(
                httpContext.User,
                ownerUserId.Value))
        {
            return Forbidden();
        }

        var includeInternalComments =
            IncidentAccessRules.IsAnalyst(httpContext.User);

        var incident = await queries.GetDetailsAsync(
            id,
            cancellationToken,
            includeInternalComments);

        return incident is null
            ? NotFound()
            : Results.Ok(incident);
    }

    private static async Task<IResult> UpdateContentAsync(
        Guid id,
        UpdateIncidentRequest request,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        // Перевіряємо право користувача на зміну інциденту.
        if (!IncidentAccessRules.CanUpdate(
                httpContext.User,
                incident.OwnerUserId))
        {
            return Forbidden();
        }

        // A-08b: відхиляємо додаткові або заборонені поля.
        if (request.ExtraFields.Count > 0)
        {
            return Results.Problem(
                title: "Недопустимі поля запиту",
                detail: "Дозволено змінювати лише Title та Description.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Перевіряємо дозволені поля.
        if (string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Description))
        {
            return Results.BadRequest(
                "Title і Description не можуть бути порожніми.");
        }

        // Змінюємо тільки дозволені поля.
        incident.Title = request.Title;
        incident.Description = request.Description;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        // Перевіряємо права згідно з матрицею доступу.
        if (!IncidentAccessRules.CanDelete(
                httpContext.User,
                incident.OwnerUserId))
        {
            return Forbidden();
        }

        dbContext.Incidents.Remove(incident);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(
                    status,
                    ignoreCase: true,
                    out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] =
                        [
                            "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
                        ]
                    });
            }

            parsedStatus = candidate;
        }

        var summary = await queries.GetSeveritySummaryAsync(
            parsedStatus,
            cancellationToken);

        return Results.Ok(summary);
    }
}
