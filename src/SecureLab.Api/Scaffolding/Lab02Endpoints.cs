
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        // GET: пошук інцидентів.
        // Вразливу реалізацію залишено для наступного етапу лабораторної.
        app.MapGet("/api/incidents/search", async (
            string? q,
            string? sortBy,
            SecureLabDbContext db,
            CancellationToken ct) =>
        {
            var order = sortBy switch
            {
                null or "" or "createdAtUtc" => "created_at_utc DESC",
                "severity" => "severity",
                "status" => "status",
                _ => sortBy
            };

            var sql = "SELECT * FROM incidents WHERE title ILIKE '%" + (q ?? "")
                + "%' OR description ILIKE '%" + (q ?? "")
                + "%' ORDER BY " + order + " LIMIT 50";

            var rows = await db.Incidents
                .FromSqlRaw(sql)
                .AsNoTracking()
                .ToListAsync(ct);

            return Results.Ok(rows.Select(row => new
            {
                row.Id,
                row.Title,
                row.Description,
                Severity = row.Severity.ToString(),
                Status = row.Status.ToString(),
                row.CreatedAtUtc
            }));
        });

        // POST: створення інциденту з серверною валідацією.
        app.MapPost("/api/incidents", async (
            CreateIncidentRequest request,
            SecureLabDbContext db,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();

            // Перевірка назви до Trim().
            if (string.IsNullOrWhiteSpace(request.Title) ||
                request.Title.Length > 160)
            {
                errors["title"] =
                    ["Title is required and must not exceed 160 characters."];
            }

            // Перевірка опису до Trim().
            if (request.Description is { Length: > 4000 })
            {
                errors["description"] =
                    ["Description must not exceed 4000 characters."];
            }

            // Перевірка допустимого значення Severity.
            if (!Enum.TryParse<IncidentSeverity>(
                    request.Severity,
                    true,
                    out var severity) ||
                !Enum.IsDefined(severity))
            {
                errors["severity"] = ["Invalid severity value."];
            }

            var now = DateTimeOffset.UtcNow;

            // Дата обов'язкова та не може бути більш ніж
            // на 5 хвилин у майбутньому.
            if (request.OccurredAtUtc is null ||
                request.OccurredAtUtc > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] =
                    ["A valid occurrence time is required."];
            }

            // Некоректні дані: HTTP 400.
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            // Нормалізація та створення інциденту.
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = DbSeeder.AliceId,
                Title = request.Title!.Trim(),
                Description = request.Description?.Trim() ?? "",
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc =
                    request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            // Успішне створення: HTTP 201.
            return Results.Created(
                $"/api/incidents/{incident.Id}",
                new
                {
                    incident.Id,
                    incident.Title,
                    incident.Description,
                    Severity = incident.Severity.ToString(),
                    Status = incident.Status.ToString(),
                    incident.OccurredAtUtc,
                    incident.CreatedAtUtc
                });
        });
    }
}

// DTO для вхідного POST-запиту.
public sealed record CreateIncidentRequest(
    string? Title,
    string? Description,
    string? Severity,
    DateTimeOffset? OccurredAtUtc);
