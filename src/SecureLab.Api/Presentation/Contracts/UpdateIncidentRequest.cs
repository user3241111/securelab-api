
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecureLab.Api.Presentation.Contracts;

public sealed record UpdateIncidentRequest(
    string Title,
    string Description)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtraFields
        { get; init; } = new();
}
