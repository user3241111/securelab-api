
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = GetSeedPassword();

        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/auth/session-check");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // A-01: Anonymous request must return 401 without a redirect.
    [Fact]
    public async Task A01_AnonymousIncidentCreation_Returns401()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        using var response = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Unauthorized incident",
                description = "Security test",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);

        Assert.DoesNotContain(
            "Bearer",
            response.Headers.WwwAuthenticate.ToString(),
            StringComparison.OrdinalIgnoreCase);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotEqual("text/html", contentType);
    }

    // A-02: Login cookie must identify the current user.
    [Fact]
    public async Task A02_LoginCookie_ReturnsCurrentUser()
    {
        using var client = await factory.LoginAsync(
            "alice", GetSeedPassword());

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var user = document.RootElement;

        Assert.Equal(
            "alice",
            user.GetProperty("userName").GetString());

        Assert.Equal(
            "Аліса Коваль",
            user.GetProperty("displayName").GetString());

        Assert.Equal(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            user.GetProperty("id").GetGuid());
    }

    // A-05: A forged owner must not replace the logged-in user.
    [Fact]
    public async Task A05_ForgedOwner_RemainsAuthenticatedUser()
    {
        using var client = await factory.LoginAsync(
            "bob", GetSeedPassword());

        using var meResponse = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using var meDocument = JsonDocument.Parse(
            await meResponse.Content.ReadAsStringAsync());

        var bobName = meDocument.RootElement
            .GetProperty("displayName")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(bobName));

        // Attempt to assign Bob's new incident to Alice.
        var forgedAliceId =
            "10000000-0000-0000-0000-000000000001";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = $"A05 owner test {Guid.NewGuid():N}",
                description = "Forged owner security test",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow,
                ownerUserId = forgedAliceId
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId = createDocument.RootElement
            .GetProperty("id")
            .GetGuid();

        // Check the owner of the newly created incident.
        using var detailsResponse = await client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        using var detailsDocument = JsonDocument.Parse(
            await detailsResponse.Content.ReadAsStringAsync());

        var actualOwnerName = detailsDocument.RootElement
            .GetProperty("ownerDisplayName")
            .GetString();

        Assert.Equal(bobName, actualOwnerName);
    }

    private static string GetSeedPassword()
    {
        return Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");
    }
}
