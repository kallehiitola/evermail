using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Evermail.AdminApp.Ops;

public sealed class AdminKeyVaultService
{
    private readonly SecretClient _client;

    public AdminKeyVaultService(SecretClient client)
    {
        _client = client;
    }

    public async Task<(bool Success, string? Error)> TrySetRuntimeModeAsync(
        string mode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.SetSecretAsync(
                name: "EvermailRuntime--Mode",
                value: mode,
                cancellationToken: cancellationToken);

            return (true, null);
        }
        catch (RequestFailedException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}


