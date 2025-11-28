namespace Evermail.Infrastructure.Services.Encryption;

public interface IOfflineByokKeyProtector
{
    string Protect(byte[] masterKey);

    byte[] Unprotect(string cipherText);
}





