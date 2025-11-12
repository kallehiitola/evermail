using System.Security.Cryptography;
using System.Text;

namespace Evermail.Infrastructure.Services;

public interface ITwoFactorService
{
    string GenerateSecret();
    string GenerateQrCodeUrl(string email, string secret, string issuer = "Evermail");
    bool ValidateCode(string secret, string code);
    List<string> GenerateBackupCodes(int count = 10);
}

public class TwoFactorService : ITwoFactorService
{
    public string GenerateSecret()
    {
        var bytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Base32Encode(bytes);
    }

    public string GenerateQrCodeUrl(string email, string secret, string issuer = "Evermail")
    {
        var encoded = Uri.EscapeDataString($"otpauth://totp/{issuer}:{email}?secret={secret}&issuer={issuer}");
        return $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={encoded}";
    }

    public bool ValidateCode(string secret, string code)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        
        // Check current time window and ±1 window for clock skew
        for (int i = -1; i <= 1; i++)
        {
            var calculatedCode = GenerateCode(secret, currentTime + i);
            if (calculatedCode == code)
            {
                return true;
            }
        }
        
        return false;
    }

    public List<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>();
        for (int i = 0; i < count; i++)
        {
            codes.Add(GenerateRandomCode());
        }
        return codes;
    }

    private string GenerateCode(string secret, long timeStep)
    {
        var secretBytes = Base32Decode(secret);
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeBytes);
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) |
                     ((hash[offset + 1] & 0xFF) << 16) |
                     ((hash[offset + 2] & 0xFF) << 8) |
                     (hash[offset + 3] & 0xFF);

        var otp = binary % 1000000;
        return otp.ToString("D6");
    }

    private string GenerateRandomCode()
    {
        var bytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var code = BitConverter.ToUInt32(bytes, 0) % 100000000;
        return code.ToString("D8");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        int bits = 0;
        int value = 0;

        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;

            while (bits >= 5)
            {
                result.Append(alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(alphabet[(value << (5 - bits)) & 0x1F]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new List<byte>();
        int bits = 0;
        int value = 0;

        foreach (var c in encoded.ToUpperInvariant())
        {
            if (c == '=') break;
            
            var index = alphabet.IndexOf(c);
            if (index < 0) continue;

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                result.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return result.ToArray();
    }
}

