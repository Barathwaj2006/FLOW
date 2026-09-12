using System;
using System.Security.Cryptography;
using System.Text;
using Flow.Core.Context;

namespace Flow.Core.History;

/// <summary>
/// Enforces sensitive target isolation, credential protection, and secret exclusion (WF-043).
/// INVIOLABLE: Password fields, credential windows, and sensitive targets NEVER persist transcripts.
/// </summary>
public sealed class HistoryPrivacyService : IHistoryPrivacyService
{
    public bool IsSafeToPersist(ContextSnapshot? context)
    {
        if (context == null) return true;

        if (context.IsSensitive)
        {
            return false;
        }

        if (context.Category == ApplicationCategory.Sensitive)
        {
            return false;
        }

        if (context.FocusedControl.IsPassword)
        {
            return false;
        }

        // Additional credential check
        string app = context.TargetInfo?.ProcessName?.ToLowerInvariant() ?? "";
        if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            app = app[..^4];
        }

        if (app is "keepass" or "1password" or "bitwarden" or "credentialui" or "credwiz" or "consent" or "lastpass" or "dashlane" or "enpass" or "authy")
        {
            return false;
        }

        return true;
    }

    public bool ShouldSaveTranscriptText(HistorySettings settings, ContextSnapshot? context)
    {
        if (!settings.HistoryEnabled || !settings.SaveTranscriptText)
        {
            return false;
        }

        return IsSafeToPersist(context);
    }

    public string? ComputeTextHash(string? text)
    {
        if (text == null) return null;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
