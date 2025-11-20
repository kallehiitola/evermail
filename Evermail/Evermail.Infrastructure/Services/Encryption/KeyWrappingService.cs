using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public class KeyWrappingService : IKeyWrappingService
{
    private readonly Dictionary<string, IKeyWrappingProvider> _providers;

    public KeyWrappingService(IEnumerable<IKeyWrappingProvider> providers)
    {
        _providers = providers.ToDictionary(
            p => p.ProviderName,
            p => p,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
        => Resolve(settings.Provider).GenerateDataKeyAsync(settings, cancellationToken);

    public async Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        string wrappedDekBase64,
        CancellationToken cancellationToken = default)
    {
        var provider = Resolve(settings.Provider);
        var cipherBytes = Convert.FromBase64String(wrappedDekBase64);
        return await provider.UnwrapDataKeyAsync(settings, cipherBytes, cancellationToken);
    }

    private IKeyWrappingProvider Resolve(string provider)
    {
        if (_providers.TryGetValue(provider, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException(
            $"No key wrapping provider registered for '{provider}'.");
    }
}

