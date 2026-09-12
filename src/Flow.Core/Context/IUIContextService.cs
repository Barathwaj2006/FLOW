using System;

namespace Flow.Core.Context;

/// <summary>
/// Service interface for querying Windows UI context, including active control safety
/// and surrounding text context via Windows UI Automation.
/// </summary>
public interface IUIContextService
{
    /// <summary>
    /// Checks whether the currently focused UI control is a password, PIN, or credential input field.
    /// Inviolable rule: If true, recording, transcription, context extraction, and text insertion MUST be refused.
    /// Fails closed if the control cannot be reliably classified in a sensitive context.
    /// </summary>
    bool IsFocusInPasswordField();

    /// <summary>
    /// Extracts up to <paramref name="maxCharacters"/> characters immediately preceding the caret
    /// in the active editable text control via UI Automation TextPattern.
    /// Strictly bounded, non-destructive, and returns empty string if in a password field or if UIA is unavailable.
    /// </summary>
    string GetNearbyContext(int maxCharacters = 200);

    /// <summary>
    /// Extracts the currently selected text in the active editable control via UI Automation TextPattern.
    /// Strictly bounded by <paramref name="maxCharacters"/> and returns empty string if in a password field or if UIA is unavailable.
    /// </summary>
    string GetSelectedText(int maxCharacters = 10000);

    /// <summary>
    /// Checks whether an active non-empty text selection exists in the focused control.
    /// </summary>
    bool HasSelectedText();
}

/// <summary>
/// Null / testing implementation of <see cref="IUIContextService"/>.
/// </summary>
public sealed class NullUIContextService : IUIContextService
{
    private readonly bool _isPasswordField;
    private readonly string _nearbyContext;
    private readonly string _selectedText;

    public NullUIContextService(bool isPasswordField = false, string nearbyContext = "", string selectedText = "")
    {
        _isPasswordField = isPasswordField;
        _nearbyContext = nearbyContext;
        _selectedText = selectedText;
    }

    public bool IsFocusInPasswordField() => _isPasswordField;

    public string GetNearbyContext(int maxCharacters = 200)
    {
        if (_isPasswordField || string.IsNullOrEmpty(_nearbyContext)) return string.Empty;
        if (_nearbyContext.Length <= maxCharacters) return _nearbyContext;
        return _nearbyContext[^maxCharacters..];
    }

    public string GetSelectedText(int maxCharacters = 10000)
    {
        if (_isPasswordField || string.IsNullOrEmpty(_selectedText)) return string.Empty;
        if (_selectedText.Length <= maxCharacters) return _selectedText;
        return _selectedText[..maxCharacters];
    }

    public bool HasSelectedText()
    {
        return !_isPasswordField && !string.IsNullOrEmpty(_selectedText);
    }
}
