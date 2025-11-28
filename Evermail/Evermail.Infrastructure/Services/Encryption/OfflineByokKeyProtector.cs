using System.Security.Cryptography;
using System.Text;
using Evermail.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Evermail.Infrastructure.Services.Encryption;

public sealed class OfflineByokKeyProtector : IOfflineByokKeyProtector
{
    private readonly byte[] _masterKey;

    public OfflineByokKeyProtector(IOptions<OfflineByokOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options?.Value, nameof(options));

        if (string.IsNullOrWhiteSpace(options.Value.MasterKey))
        {
            throw new InvalidOperationException(
                "Offline BYOK master key is not configured. Set configuration value 'OfflineByok:MasterKey' to a base64-encoded 256-bit key.");
        }

        try
        {
            _masterKey = Convert.FromBase64String(options.Value.MasterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Offline BYOK master key must be base64 encoded.", ex);
        }

        if (_masterKey.Length != 32)
        {
            throw new InvalidOperationException("Offline BYOK master key must be 256 bits (32 bytes).");
        }
    }

    public string Protect(byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[masterKey.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_masterKey, tag.Length);
        aes.Encrypt(nonce, masterKey, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public byte[] Unprotect(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new InvalidOperationException("Offline master key is not configured.");
        }

        var payload = Convert.FromBase64String(cipherText);
        if (payload.Length < 12 + 16)
        {
            throw new InvalidOperationException("Offline master key payload is invalid.");
        }

        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var cipher = payload.AsSpan(28);

        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(_masterKey, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plaintext);
        return plaintext;
    }
}





