
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string GetSeedPassword()
    {
        return Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");
    }

    [Fact]
    public async Task GetList_ReturnsSeededIncidents()
    {
        using var client = await factory.LoginAsync(
            "morgan", GetSeedPassword());

        var incidents = await client.GetFromJsonAsync<
            List<IncidentListItemResponse>>("/api/incidents");

        Assert.NotNull(incidents);

        Assert.Contains(
            incidents,
            incident => incident.Id == DbSeeder.AliceIncidentId);

        Assert.Contains(
            incidents,
            incident => incident.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        using var client = await factory.LoginAsync(
            "morgan", GetSeedPassword());

        using var response = await client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDetails_DoesNotExposeInternalOwnerFields()
    {
        using var client = await factory.LoginAsync(
            "alice", GetSeedPassword());

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.False(
            document.RootElement.TryGetProperty("ownerUserId", out _));

        Assert.False(
            document.RootElement.TryGetProperty("email", out _));

        Assert.True(
            document.RootElement.TryGetProperty(
                "ownerDisplayName", out var ownerName));

        Assert.False(string.IsNullOrWhiteSpace(
            ownerName.GetString()));
    }

    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/auth-lab.html");

        Assert.DoesNotContain(
            "innerHTML", html, StringComparison.Ordinal);

        Assert.Contains(
            "textContent", html, StringComparison.Ordinal);
    }
}
