
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class AuthorizationTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException(
            "Set SeedUsers__Password outside Git.");

    // A-03: Reporter can read their own incident.
    [Fact]
    public async Task A03_Reporter_CanReadOwnIncident()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A-03R: Reporter cannot read another user's incident.
    [Fact]
    public async Task A03R_Reporter_CannotReadForeignIncident()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // A-04: Reporter list is filtered by ownership.
    [Fact]
    public async Task A04_Reporter_ListExcludesForeignIncident()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.GetAsync("/api/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var ids = document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        Assert.Contains(DbSeeder.AliceIncidentId, ids);
        Assert.DoesNotContain(DbSeeder.BobIncidentId, ids);
    }

    // A-06: Reporter cannot update a foreign incident.
    [Fact]
public async Task A04_Reporter_CannotUpdateForeignIncident()    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.PutAsJsonAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}",
            new
            {
                title = "Unauthorized title change",
                description = "Unauthorized description change"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // A-07: Deletion is forbidden by the access matrix.
    [Fact]
public async Task A04_Reporter_CannotDeleteOwnIncident()    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.DeleteAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // A-08b: Unexpected fields must be rejected.
    [Fact]
    public async Task A08b_Reporter_CannotModifyProtectedFields()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.PutAsJsonAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}",
            new
            {
                title = "Test title",
                description = "Test description",
                ownerUserId = Guid.Parse("10000000-0000-0000-0000-000000000002")            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // A-12: Reporter cannot read an internal comment.
    [Fact]
    public async Task A12_Reporter_CannotReadInternalComment()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.GetAsync(
            "/api/incident-comments/30000000-0000-0000-0000-000000000003");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Analyst can read incidents belonging to other users.
    [Fact]
    public async Task Analyst_CanReadForeignIncident()
    {
        using var client = await factory.LoginAsync("morgan", Password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Reporter cannot perform the privileged triage operation.
    [Fact]
    public async Task Reporter_CannotTriageIncident()
    {
        using var client = await factory.LoginAsync("alice", Password);

        using var response = await client.PostAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}/triage",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // A-06: Reporter cannot perform privileged triage.
    // The incident must remain in New status.
    [Fact]
    public async Task A06_Reporter_TriageDenied_StatusUnchanged()
    {
        using var client = await factory.LoginAsync("alice", Password);

        var incidentId = await CreateNewIncidentAsync(client);

        using var beforeResponse = await client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        var beforeStatus = await ReadStatusAsync(beforeResponse);
        Assert.Equal("New", beforeStatus);

        using var triageResponse = await client.PostAsync(
            $"/api/incidents/{incidentId}/triage", null);

        Assert.Equal(HttpStatusCode.Forbidden, triageResponse.StatusCode);

        using var afterResponse = await client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        var afterStatus = await ReadStatusAsync(afterResponse);

        Assert.Equal("New", afterStatus);
        Assert.Equal(beforeStatus, afterStatus);
    }

    // A-07: Analyst can perform New -> Triaged transition.
    [Fact]
    public async Task A07_Analyst_TriageAllowed_StatusChanged()
    {
        using var client = await factory.LoginAsync("morgan", Password);

        var incidentId = await CreateNewIncidentAsync(client);

        using var beforeResponse = await client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        var beforeStatus = await ReadStatusAsync(beforeResponse);
        Assert.Equal("New", beforeStatus);

        using var triageResponse = await client.PostAsync(
            $"/api/incidents/{incidentId}/triage", null);

        Assert.Equal(HttpStatusCode.NoContent, triageResponse.StatusCode);

        using var afterResponse = await client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        var afterStatus = await ReadStatusAsync(afterResponse);

        Assert.Equal("Triaged", afterStatus);
    }

    private static async Task<Guid> CreateNewIncidentAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = $"Authorization test {Guid.NewGuid():N}",
                description = "Automated authorization regression test",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string?> ReadStatusAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("status")
            .GetString();
    }

}
