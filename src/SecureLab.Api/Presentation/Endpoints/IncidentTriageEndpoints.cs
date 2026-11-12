using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentTriageEndpoints
{
    public static IEndpointRouteBuilder MapIncidentTriageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/incidents/{id:guid}/triage",
                TriageAsync)
            .WithName("TriageIncident")
            .WithTags("Incidents")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> TriageAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Завантажуємо інцидент із відстеженням змін.
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Ідентифікатор користувача беремо лише з principal.
        var currentUserId =
            IncidentAccessRules.GetCurrentUserId(
                httpContext.User);

        // Перевірка права на привілейовану операцію.
        if (!currentUserId.HasValue ||
            !IncidentAccessRules.CanTriage(httpContext.User))
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;            var occurredAtUtc = DateTimeOffset.UtcNow;

            var logger = loggerFactory.CreateLogger(
                "SecureLab.Authorization");

            // Безпечний структурований аудит відмови.
            logger.LogWarning(
                "authorization.denied " +
                "SubjectId={SubjectId} " +
                "Action={Action} " +
                "ResourceId={ResourceId} " +
                "Result={Result} " +
                "OccurredAtUtc={OccurredAtUtc} " +
                "TraceId={TraceId}",
                currentUserId?.ToString() ?? "unknown",
                "incident.triage",
                id,
                "deny",
                occurredAtUtc,
                traceId);

            return Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = traceId
                });
        }

        // Дозволений лише перехід New -> Triaged.
        if (incident.Status != IncidentStatus.New)
        {
            return Results.Problem(
                title: "Неприпустимий перехід статусу",
                detail: "Triage дозволено лише для інциденту зі статусом New.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var previousStatus = incident.Status;
        var now = DateTimeOffset.UtcNow;

        // Наступний статус визначається сервером.
        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = now;

        // Зберігаємо історію зміни статусу.
        dbContext.IncidentStatusHistory.Add(
            new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                ChangedByUserId = currentUserId.Value,
                OldStatus = previousStatus,
                NewStatus = IncidentStatus.Triaged,
                Note = "Тріаж інциденту аналітиком.",
                CreatedAtUtc = now
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
