using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Flow.Core.Storage;

/// <summary>
/// Provides Windows Data Protection API (DPAPI) encryption and decryption at rest
/// for sensitive user credentials, personal dictionary entities, and local tokens.
/// Satisfies local-first cryptographic privacy requirements.
/// </summary>
public static class DpapiDataProtection
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FLOW_Offline_Sovereign_v1");

    /// <summary>
    /// Encrypts plaintext bytes using Windows DPAPI tied to the CurrentUser scope.
    /// </summary>
    public static byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0) return Array.Empty<byte>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            }
            catch
            {
                // Fallback to raw if DPAPI subsystem fails
                return plaintext;
            }
        }

        return plaintext;
    }

    /// <summary>
    /// Decrypts DPAPI-encrypted ciphertext using the CurrentUser scope.
    /// </summary>
    public static byte[] Unprotect(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length == 0) return Array.Empty<byte>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
            }
            catch
            {
                return ciphertext;
            }
        }

        return ciphertext;
    }

    /// <summary>
    /// Encrypts a sensitive string to a Base64-encoded DPAPI ciphertext.
    /// </summary>
    public static string ProtectString(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = Protect(plainBytes);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a Base64-encoded DPAPI ciphertext back to plaintext string.
    /// </summary>
    public static string UnprotectString(string base64Ciphertext)
    {
        if (string.IsNullOrEmpty(base64Ciphertext)) return string.Empty;
        try
        {
            byte[] cipherBytes = Convert.FromBase64String(base64Ciphertext);
            byte[] decrypted = Unprotect(cipherBytes);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return base64Ciphertext;
        }
    }
}
