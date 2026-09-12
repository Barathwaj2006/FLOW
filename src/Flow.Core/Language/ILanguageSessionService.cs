using System;

namespace Flow.Core.Language;

/// <summary>
/// Service managing persistent default language and session-scoped language overrides.
/// Strictly enforces session isolation: session overrides must not leak into subsequent sessions.
/// </summary>
public interface ILanguageSessionService
{
    /// <summary>
    /// User's persistent default language preference (e.g., Auto or English).
    /// </summary>
    LanguageInfo DefaultLanguage { get; }

    /// <summary>
    /// Transient session override language, if explicitly set for the current recording session.
    /// </summary>
    LanguageInfo? SessionOverride { get; }

    /// <summary>
    /// Currently effective language for speech recognition (SessionOverride ?? DefaultLanguage).
    /// </summary>
    LanguageInfo ActiveLanguage { get; }

    /// <summary>
    /// Updates the persistent default language preference.
    /// </summary>
    void SetDefaultLanguage(LanguageCode code);

    /// <summary>
    /// Sets a temporary language override for the current/next session.
    /// </summary>
    void SetSessionLanguage(LanguageCode code);

    /// <summary>
    /// Resets the session-scoped override back to the default language.
    /// MUST be invoked upon session completion or cancellation.
    /// </summary>
    void ResetSession();

    /// <summary>
    /// Event fired when the active language changes.
    /// </summary>
    event Action<LanguageInfo>? ActiveLanguageChanged;
}

/// <summary>
/// Thread-safe implementation of ILanguageSessionService.
/// </summary>
public sealed class LanguageSessionService : ILanguageSessionService
{
    private readonly object _lock = new();
    private LanguageInfo _defaultLanguage;
    private LanguageInfo? _sessionOverride;

    public LanguageInfo DefaultLanguage
    {
        get
        {
            lock (_lock) return _defaultLanguage;
        }
        private set
        {
            lock (_lock) _defaultLanguage = value;
            NotifyChanged();
        }
    }

    public LanguageInfo? SessionOverride
    {
        get
        {
            lock (_lock) return _sessionOverride;
        }
        private set
        {
            lock (_lock) _sessionOverride = value;
            NotifyChanged();
        }
    }

    public LanguageInfo ActiveLanguage
    {
        get
        {
            lock (_lock) return _sessionOverride ?? _defaultLanguage;
        }
    }

    public event Action<LanguageInfo>? ActiveLanguageChanged;

    public LanguageSessionService(LanguageInfo? initialDefault = null)
    {
        _defaultLanguage = initialDefault ?? LanguageCatalog.Auto;
    }

    public void SetDefaultLanguage(LanguageCode code)
    {
        if (LanguageCatalog.TryGetLanguage(code.Value, out var lang))
        {
            DefaultLanguage = lang;
        }
        else
        {
            // Fall back safely to English with warning
            DefaultLanguage = LanguageCatalog.English;
        }
    }

    public void SetSessionLanguage(LanguageCode code)
    {
        if (LanguageCatalog.TryGetLanguage(code.Value, out var lang))
        {
            SessionOverride = lang;
        }
        else
        {
            // Deterministic fallback: do not set an invalid override
            SessionOverride = null;
        }
    }

    public void ResetSession()
    {
        lock (_lock)
        {
            if (_sessionOverride == null) return;
            _sessionOverride = null;
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        ActiveLanguageChanged?.Invoke(ActiveLanguage);
    }
}
