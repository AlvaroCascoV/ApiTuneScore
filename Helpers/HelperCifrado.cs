using System.Security.Cryptography;
using System.Text;

namespace ApiTuneScore.Helpers;

/// <summary>AES encrypt/decrypt for JWT UserData claim (same pattern as ApiOAuthEmpleadosACV).</summary>
public static class HelperCifrado
{
    private static byte[]? _keyBytes;

    /// <summary>True when a key was loaded; false when missing (host still starts so OpenAPI/health work in Azure until app settings are added).</summary>
    public static bool IsConfigured => _keyBytes != null;

    /// <summary>
    /// Loads the AES key. If <paramref name="explicitKeyFromKeyVault"/> is set (e.g. from <c>SecretClient.GetSecret</c>), it wins;
    /// otherwise resolves from configuration (including <c>AddAzureKeyVault</c> mapped secrets).
    /// </summary>
    public static void Initialize(IConfiguration configuration, string? explicitKeyFromKeyVault = null)
    {
        var key = explicitKeyFromKeyVault?.Trim();
        if (string.IsNullOrWhiteSpace(key))
            key = ResolveClavesCryptoKey(configuration);

        if (string.IsNullOrWhiteSpace(key))
        {
            _keyBytes = null;
            return;
        }

        _keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Resolves the crypto passphrase from configuration. Supports Key Vault secret <c>ClavesCrypto--Key</c> (→ <c>ClavesCrypto:Key</c>)
    /// and the common mistake <c>ClavesCrypto-Key</c> (single dash, flat configuration key).
    /// </summary>
    public static string? ResolveClavesCryptoKey(IConfiguration configuration)
    {
        var key = configuration["ClavesCrypto:Key"];
        if (!string.IsNullOrWhiteSpace(key))
            return key.Trim();

        // Key Vault secret named "ClavesCrypto-Key" (one dash) becomes this flat key, not ClavesCrypto:Key
        key = configuration["ClavesCrypto-Key"];
        if (!string.IsNullOrWhiteSpace(key))
            return key.Trim();

        return null;
    }

    public static string CifrarString(string data)
    {
        byte[] keyData = GetKeyBytes();
        return EncryptString(keyData, data);
    }

    public static string DescifrarString(string data)
    {
        byte[] keyData = GetKeyBytes();
        return DecryptString(keyData, data);
    }

    private static byte[] GetKeyBytes()
    {
        if (_keyBytes == null)
        {
            throw new InvalidOperationException(
                "ClavesCrypto:Key is not configured. Set ClavesCrypto__Key or ClavesCrypto__KeyVaultSecretName in Azure App Service, " +
                "add Key Vault secret ClavesCrypto--Key (configuration provider), use appsettings.Secrets.json locally, " +
                "or set ClavesCrypto:KeyVaultSecretName to the vault secret name for imperative GetSecret.");
        }

        return _keyBytes;
    }

    private static string EncryptString(byte[] key, string plainText)
    {
        byte[] iv = new byte[16];
        byte[] array;

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            using (var streamWriter = new StreamWriter(cryptoStream))
            {
                streamWriter.Write(plainText);
            }

            array = memoryStream.ToArray();
        }

        return Convert.ToBase64String(array);
    }

    private static string DecryptString(byte[] key, string cipherText)
    {
        byte[] iv = new byte[16];
        byte[] buffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream(buffer);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        using var streamReader = new StreamReader(cryptoStream);
        return streamReader.ReadToEnd();
    }
}
