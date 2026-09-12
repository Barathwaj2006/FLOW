using System;

namespace Flow.Core.Context;

/// <summary>
/// Service interface for querying Windows UI context, including active control safety,
/// application category classification, and surrounding text context via Windows UI Automation.
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

    /// <summary>
    /// Queries the currently focused / foreground window information at session start (HWND, PID, process name, window title).
    /// Used to preserve target application focus and prevent cross-application text leakage during dictation.
    /// </summary>
    ForegroundTargetInfo GetForegroundTargetInfo();

    /// <summary>
    /// Captures a complete, immutable context snapshot for the specified session (WF-029, WF-030, WF-031A, WF-031B).
    /// Evaluates target window, application category, focused control, password exclusion, nearby text, and selection.
    /// </summary>
    ContextSnapshot CaptureContext(Guid sessionId, int maxNearbyCharacters = 200, int maxSelectionCharacters = 10000);

    /// <summary>
    /// Gets information describing the currently focused UI control.
    /// </summary>
    FocusedControlInfo GetFocusedControlInfo();

    /// <summary>
    /// Classifies the application category for the given target info.
    /// </summary>
    ApplicationCategory GetApplicationCategory(ForegroundTargetInfo targetInfo);

    /// <summary>
    /// Validates whether the initial foreground target is still active and valid before text insertion.
    /// </summary>
    bool ValidateTargetStillActive(ForegroundTargetInfo initialTarget);
}

/// <summary>
/// Information describing the foreground application target at session start.
/// </summary>
public sealed record ForegroundTargetInfo(
    IntPtr Hwnd,
    uint ProcessId,
    string ProcessName,
    string WindowTitle
)
{
    public static readonly ForegroundTargetInfo Empty = new(IntPtr.Zero, 0, "Unknown", string.Empty);
}

/// <summary>
/// Null / testing implementation of <see cref="IUIContextService"/>.
/// </summary>
public sealed class NullUIContextService : IUIContextService
{
    private readonly bool _isPasswordField;
    private readonly string _nearbyContext;
    private readonly string _selectedText;
    private readonly ForegroundTargetInfo _targetInfo;
    private readonly ApplicationCategory _category;
    private readonly FocusedControlInfo _focusedControl;
    private readonly bool _isTargetActive;
    private readonly IApplicationClassifier _classifier;

    public NullUIContextService(
        bool isPasswordField = false,
        string nearbyContext = "",
        string selectedText = "",
        ForegroundTargetInfo? targetInfo = null,
        ApplicationCategory? category = null,
        FocusedControlInfo? focusedControl = null,
        bool isTargetActive = true,
        IApplicationClassifier? classifier = null)
    {
        _isPasswordField = isPasswordField;
        _nearbyContext = nearbyContext;
        _selectedText = selectedText;
        _targetInfo = targetInfo ?? ForegroundTargetInfo.Empty;
        _classifier = classifier ?? new RuleBasedApplicationClassifier();
        _category = category ?? _classifier.Classify(_targetInfo);
        _focusedControl = focusedControl ?? (isPasswordField
            ? new FocusedControlInfo("PasswordBox", string.Empty, string.Empty, string.Empty, true, false, false)
            : new FocusedControlInfo("TextBox", string.Empty, string.Empty, string.Empty, false, true, false));
        _isTargetActive = isTargetActive;
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

    public ForegroundTargetInfo GetForegroundTargetInfo() => _targetInfo;

    public ContextSnapshot CaptureContext(Guid sessionId, int maxNearbyCharacters = 200, int maxSelectionCharacters = 10000)
    {
        if (_isPasswordField)
        {
            return ContextSnapshot.CreateSensitive(sessionId, _targetInfo);
        }

        string nearby = GetNearbyContext(maxNearbyCharacters);
        string selection = GetSelectedText(maxSelectionCharacters);

        return new ContextSnapshot(
            sessionId,
            DateTimeOffset.UtcNow,
            _targetInfo,
            _category,
            _focusedControl,
            false,
            string.IsNullOrEmpty(nearby) ? null : nearby,
            string.IsNullOrEmpty(selection) ? null : selection,
            null,
            1.0f,
            "NullUIContextService"
        );
    }

    public FocusedControlInfo GetFocusedControlInfo() => _focusedControl;

    public ApplicationCategory GetApplicationCategory(ForegroundTargetInfo targetInfo) => _classifier.Classify(targetInfo);

    public bool ValidateTargetStillActive(ForegroundTargetInfo initialTarget)
    {
        if (!_isTargetActive) return false;
        if (initialTarget == null || initialTarget.Hwnd == IntPtr.Zero) return true;
        return _targetInfo.Hwnd == initialTarget.Hwnd && _targetInfo.ProcessId == initialTarget.ProcessId;
    }
}
