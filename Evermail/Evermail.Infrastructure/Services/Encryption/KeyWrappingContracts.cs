using System.Text.Json;

namespace Evermail.Infrastructure.Services.Encryption;

public record WrappedDekResult(
    byte[] PlaintextDek,
    string WrappedDekBase64,
    string Algorithm,
    string ProviderKeyVersion,
    string ProviderRequestId,
    string? ProviderMetadataJson);

public record UnwrappedDekResult(
    byte[] PlaintextDek,
    string ProviderRequestId,
    string? ProviderMetadataJson);

public static class ProviderMetadataBuilder
{
    public static string? Serialize(object? payload)
        => payload is null ? null : JsonSerializer.Serialize(payload);
}

