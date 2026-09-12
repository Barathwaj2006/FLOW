using System;
using System.IO;
using System.Linq;

namespace Flow.Core.Commands;

/// <summary>
/// Validates Web URLs for safe navigation commands (WF-036).
/// Strictly allows https:// and http:// schemes.
/// Rejects dangerous schemes (javascript:, file:, shell:, ms-settings:, data:, etc.).
/// </summary>
public static class UrlSafetyValidator
{
    private static readonly string[] AllowedSchemes = { Uri.UriSchemeHttps, Uri.UriSchemeHttp };

    public static bool TryValidateUrl(string rawUrl, out Uri? validatedUri, out string? failureReason)
    {
        validatedUri = null;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            failureReason = "URL is null or empty.";
            return false;
        }

        string trimmed = rawUrl.Trim();

        // If scheme is missing (no colon present), default to https://
        // If a colon is present (e.g. javascript:, file:, shell:), keep it as-is so scheme validation rejects it
        if (!trimmed.Contains(':'))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            failureReason = "Malformed URL format.";
            return false;
        }

        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            failureReason = $"Prohibited URL scheme '{uri.Scheme}'. Only HTTPS and HTTP are permitted.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || (!uri.Host.Contains('.') && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            failureReason = "Invalid host in URL.";
            return false;
        }

        validatedUri = uri;
        return true;
    }
}

/// <summary>
/// Validates local folders for safe exploration commands.
/// Rejects relative traversal (..), non-existent directories, or destructive commands.
/// </summary>
public static class FolderSafetyValidator
{
    public static bool TryValidateFolder(string rawPath, out string? safePath, out string? failureReason)
    {
        safePath = null;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            failureReason = "Folder path is empty.";
            return false;
        }

        string trimmed = rawPath.Trim();

        // Must be rooted Windows or user path
        if (!Path.IsPathRooted(trimmed))
        {
            failureReason = "Relative directory traversal is not permitted.";
            return false;
        }

        if (trimmed.Contains(".."))
        {
            failureReason = "Path traversal sequences ('..') are strictly prohibited.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            if (!Directory.Exists(fullPath))
            {
                failureReason = $"Directory does not exist: {fullPath}";
                return false;
            }

            safePath = fullPath;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Invalid folder path syntax: {ex.Message}";
            return false;
        }
    }
}
