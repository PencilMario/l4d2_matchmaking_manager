using System.Security.Cryptography;
using System.Text;
using L4d2MatchmakingCore.Configuration;

namespace L4d2MatchmakingCore.Servers;

public interface ISecretProtector
{
    string Protect(string password);
    string? Unprotect(string? ciphertext);
}

public interface IRconCredentialProtector : ISecretProtector;

public sealed class RconCredentialProtector : IRconCredentialProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[]? _key;

    public RconCredentialProtector(CoreOptions options)
    {
        if (options.RconEncryptionKey is not { } encodedKey)
            return;

        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("core_rcon_encryption_key_invalid", exception);
        }

        if (_key.Length != KeySize)
            throw new InvalidOperationException("core_rcon_encryption_key_invalid");
    }

    public string Protect(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var key = GetKey();
        var plaintext = Encoding.UTF8.GetBytes(password);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
            return $"v1:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(ciphertext)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string? Unprotect(string? ciphertext)
    {
        if (ciphertext is null)
            return null;

        var parts = ciphertext.Split(':');
        if (parts.Length != 4 || parts[0] != "v1")
            throw new InvalidOperationException("rcon_credential_decryption_failed");

        try
        {
            var nonce = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var encrypted = Convert.FromBase64String(parts[3]);
            if (nonce.Length != NonceSize || tag.Length != TagSize)
                throw new InvalidOperationException("rcon_credential_decryption_failed");

            var plaintext = new byte[encrypted.Length];
            using var aes = new AesGcm(GetKey(), TagSize);
            aes.Decrypt(nonce, encrypted, tag, plaintext);
            try
            {
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("rcon_credential_decryption_failed", exception);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("rcon_credential_decryption_failed", exception);
        }
    }

    private byte[] GetKey() => _key ?? throw new InvalidOperationException("rcon_encryption_key_not_configured");
}
