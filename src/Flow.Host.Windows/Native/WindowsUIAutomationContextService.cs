using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using Flow.Core.Context;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Production Windows UI Automation Context Service.
/// Provides real-time query of active focused control security (password/credential exclusion),
/// application classification, context snapshot capture, and surrounding text context via UIA.
/// </summary>
public sealed class WindowsUIAutomationContextService : IUIContextService
{
    private readonly ILogger<WindowsUIAutomationContextService>? _logger;
    private readonly IApplicationClassifier _classifier;

    public WindowsUIAutomationContextService(
        ILogger<WindowsUIAutomationContextService>? logger = null,
        IApplicationClassifier? classifier = null)
    {
        _logger = logger;
        _classifier = classifier ?? new RuleBasedApplicationClassifier();
    }

    /// <inheritdoc />
    public bool IsFocusInPasswordField() => IsFocusInPasswordField(null);

    /// <summary>
    /// Checks whether the specified or currently focused UI control is a password, PIN, or credential input field.
    /// </summary>
    public bool IsFocusInPasswordField(AutomationElement? targetElement)
    {
        try
        {
            // 1. Query target or active focused element via UI Automation
            AutomationElement? focusedElement = targetElement;
            if (focusedElement == null)
            {
                try
                {
                    focusedElement = AutomationElement.FocusedElement;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Unable to query AutomationElement.FocusedElement directly. Failing closed.");
                    return true;
                }
            }

            if (focusedElement != null)
            {
                // Primary & secondary element inspection
                if (IsElementSensitive(focusedElement))
                {
                    return true;
                }

                // Check immediate container/parent hierarchy (skip top-level Window elements)
                try
                {
                    var walker = TreeWalker.ControlViewWalker;
                    var parent = walker.GetParent(focusedElement);
                    if (parent != null && parent.Current.ControlType != ControlType.Window && IsElementSensitive(parent))
                    {
                        _logger?.LogWarning("Focused element container classified as password/sensitive field.");
                        return true;
                    }
                }
                catch
                {
                    // Parent walk is best-effort
                }
            }

            // 2. Fallback check: Foreground window process/title heuristic (Credential Manager / Windows Security / Password Managers)
            IntPtr foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd != IntPtr.Zero)
            {
                string title = GetWindowTitle(foregroundHwnd).ToLowerInvariant();
                string processName = GetProcessName(foregroundHwnd).ToLowerInvariant();

                if (title.Contains("windows security") || title.Contains("credential") ||
                    title.Contains("password") || title.Contains("bitwarden") || title.Contains("1password") || title.Contains("keepass") ||
                    processName == "credentialuibroker" || processName == "consent" ||
                    processName == "keepass" || processName == "keepassxc" || processName == "1password" || processName == "bitwarden")
                {
                    _logger?.LogWarning("Foreground window is Security / Credential UI ({Title}, {Proc}). Failing closed.", title, processName);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception encountered during password field check. Failing closed for security.");
            return true;
        }
    }

    private static readonly char[] WordDelimiters = new[] { ' ', '_', '-', '.', ':', ';', '/', '\\', '[', ']', '(', ')', '{', '}' };

    private static bool ContainsSensitiveKeyword(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string lower = text.ToLowerInvariant();
        if (lower.Contains("password") || lower.Contains("passwd") || lower.Contains("passcode") ||
            lower.Contains("credential") || lower.Contains("pinbox") || lower.Contains("security code") ||
            lower.Contains("pwdbox"))
        {
            return true;
        }

        string[] words = lower.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word is "pin" or "pincode" or "pin#" or "secret" or "pwd")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether an individual UI Automation element exposes password or credential characteristics.
    /// Inviolable rule: If element properties cannot be queried or element is unavailable, fails closed (returns true).
    /// </summary>
    public bool IsElementSensitive(AutomationElement? element)
    {
        if (element == null) return true;

        try
        {
            // 1. Primary check: AutomationElement.IsPasswordProperty
            object isPasswordProp = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
            if (isPasswordProp is true || (isPasswordProp is bool b && b))
            {
                _logger?.LogWarning("Element classified as password/credential field via UIA IsPasswordProperty.");
                return true;
            }

            // 2. ClassName heuristic
            string className = element.Current.ClassName ?? string.Empty;
            if (ContainsSensitiveKeyword(className))
            {
                _logger?.LogWarning("Element classified as password field via ClassName ({Class}).", className);
                return true;
            }

            // 3. AutomationId heuristic
            string automationId = element.Current.AutomationId ?? string.Empty;
            if (ContainsSensitiveKeyword(automationId))
            {
                _logger?.LogWarning("Element classified as password field via AutomationId ({AutomationId}).", automationId);
                return true;
            }

            // 4. Name heuristic
            string name = element.Current.Name ?? string.Empty;
            if (ContainsSensitiveKeyword(name))
            {
                _logger?.LogWarning("Element classified as password field via Name ({Name}).", name);
                return true;
            }

            // 5. HelpText heuristic
            try
            {
                string helpText = element.Current.HelpText ?? string.Empty;
                if (ContainsSensitiveKeyword(helpText))
                {
                    _logger?.LogWarning("Element classified as password field via HelpText ({HelpText}).", helpText);
                    return true;
                }
            }
            catch
            {
                // HelpText is optional
            }

            return false;
        }
        catch (ElementNotAvailableException)
        {
            _logger?.LogWarning("Element is no longer available during sensitivity check. Failing closed.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error reading element properties during sensitivity check. Failing closed.");
            return true;
        }
    }

    /// <inheritdoc />
    public string GetNearbyContext(int maxCharacters = 200) => GetNearbyContext(maxCharacters, null);

    /// <summary>
    /// Extracts up to <paramref name="maxCharacters"/> characters immediately preceding the caret
    /// in the target or active editable text control via UI Automation TextPattern.
    /// </summary>
    public string GetNearbyContext(int maxCharacters, AutomationElement? targetElement)
    {
        if (maxCharacters <= 0) return string.Empty;

        // INVIOLABLE SAFETY: Never extract context if focus is in a password/credential field
        if (IsFocusInPasswordField(targetElement))
        {
            _logger?.LogWarning("Attempted to query nearby context in a password field. Returning empty context.");
            return string.Empty;
        }

        try
        {
            AutomationElement? focused = targetElement;
            if (focused == null)
            {
                try
                {
                    focused = AutomationElement.FocusedElement;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to get FocusedElement for context extraction.");
                    return string.Empty;
                }
            }

            if (focused == null)
            {
                return string.Empty;
            }

            // Verify element is not a password element (double check on cached property)
            object isPass = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
            if (isPass is true)
            {
                return string.Empty;
            }

            // Query TextPattern
            if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj) || patternObj is not TextPattern textPattern)
            {
                return string.Empty;
            }

            TextPatternRange[] selection = textPattern.GetSelection();
            if (selection == null || selection.Length == 0)
            {
                return string.Empty;
            }

            // Caret selection range
            TextPatternRange caretRange = selection[0];
            TextPatternRange contextRange = caretRange.Clone();

            // Move the Start endpoint backward by up to maxCharacters
            contextRange.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -maxCharacters);
            // Collapse End endpoint to the original Caret Start endpoint
            contextRange.MoveEndpointByRange(TextPatternRangeEndpoint.End, caretRange, TextPatternRangeEndpoint.Start);

            string text = contextRange.GetText(maxCharacters);
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Bounded constraint
            if (text.Length > maxCharacters)
            {
                text = text[^maxCharacters..];
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Nearby context extraction encountered non-fatal error; returning empty.");
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public string GetSelectedText(int maxCharacters = 10000) => GetSelectedText(maxCharacters, null);

    /// <summary>
    /// Extracts currently selected text in the active or target editable control via UI Automation TextPattern.
    /// Strictly bounded, non-destructive, and returns empty string if in a password field or if UIA is unavailable.
    /// </summary>
    public string GetSelectedText(int maxCharacters, AutomationElement? targetElement)
    {
        if (maxCharacters <= 0) return string.Empty;

        // INVIOLABLE SAFETY: Never extract selection if focus is in a password/credential field
        if (IsFocusInPasswordField(targetElement))
        {
            _logger?.LogWarning("Attempted to query selected text in a password field. Returning empty.");
            return string.Empty;
        }

        try
        {
            AutomationElement? focused = targetElement;
            if (focused == null)
            {
                try
                {
                    focused = AutomationElement.FocusedElement;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to get FocusedElement for selection extraction.");
                    return string.Empty;
                }
            }

            if (focused == null)
            {
                return string.Empty;
            }

            // Verify element is not a password element (double check on cached property)
            object isPass = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
            if (isPass is true)
            {
                return string.Empty;
            }

            // Query TextPattern
            if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj) || patternObj is not TextPattern textPattern)
            {
                return string.Empty;
            }

            TextPatternRange[] selection = textPattern.GetSelection();
            if (selection == null || selection.Length == 0)
            {
                return string.Empty;
            }

            TextPatternRange selectedRange = selection[0];
            string selectedText = selectedRange.GetText(maxCharacters);
            if (string.IsNullOrEmpty(selectedText))
            {
                return string.Empty;
            }

            if (selectedText.Length > maxCharacters)
            {
                selectedText = selectedText[..maxCharacters];
            }

            return selectedText;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Selection extraction encountered non-fatal error; returning empty.");
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public bool HasSelectedText()
    {
        var text = GetSelectedText(100);
        return !string.IsNullOrEmpty(text);
    }

    /// <inheritdoc />
    public ForegroundTargetInfo GetForegroundTargetInfo()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return ForegroundTargetInfo.Empty;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        string procName = GetProcessName(hwnd);
        string title = GetWindowTitle(hwnd);

        return new ForegroundTargetInfo(hwnd, pid, string.IsNullOrEmpty(procName) ? "Unknown" : procName, title);
    }

    /// <inheritdoc />
    public ContextSnapshot CaptureContext(Guid sessionId, int maxNearbyCharacters = 200, int maxSelectionCharacters = 10000)
        => CaptureContext(sessionId, maxNearbyCharacters, maxSelectionCharacters, null);

    /// <summary>
    /// Captures a complete, immutable context snapshot for the specified session and optional target element.
    /// Inviolable rule: If sensitive or ambiguous, fails closed and sets NearbyText = null, SelectionText = null.
    /// </summary>
    public ContextSnapshot CaptureContext(
        Guid sessionId,
        int maxNearbyCharacters,
        int maxSelectionCharacters,
        AutomationElement? targetElement)
    {
        try
        {
            ForegroundTargetInfo targetInfo = GetForegroundTargetInfo();

            // 1. Password/Sensitive check first (Fail Closed)
            if (IsFocusInPasswordField(targetElement))
            {
                _logger?.LogWarning("CaptureContext detected password/sensitive target. Emitting sensitive snapshot without text.");
                return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
            }

            // 2. Classify application category
            ApplicationCategory category = _classifier.Classify(targetInfo);
            if (category == ApplicationCategory.Sensitive)
            {
                return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
            }

            // 3. Focused control info
            FocusedControlInfo controlInfo = GetFocusedControlInfo(targetElement);
            if (controlInfo.IsPassword)
            {
                return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
            }

            // 4. Resolve focused element safely for text extraction
            // FAIL-CLOSED RULE: If targetElement is null, resolve focused element.
            // If focused element cannot be resolved or is ambiguous/uncertain, FAIL CLOSED and do not extract text.
            AutomationElement? resolvedElement = targetElement;
            if (resolvedElement == null)
            {
                try
                {
                    resolvedElement = AutomationElement.FocusedElement;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to resolve focused element in CaptureContext. Failing closed for security.");
                    return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
                }
            }

            if (resolvedElement == null)
            {
                _logger?.LogWarning("No focused element resolvable. Failing closed for security.");
                return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
            }

            // Check if resolved element is sensitive
            if (IsElementSensitive(resolvedElement))
            {
                return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
            }

            // Target correlation check:
            // If targetInfo has a valid non-zero PID, ensure the focused element belongs to that process (or child/host thread).
            // If the focused element belongs to a completely different non-related process, we are uncertain -> fail closed!
            if (targetElement == null && targetInfo.ProcessId != 0)
            {
                try
                {
                    int elementPid = resolvedElement.Current.ProcessId;
                    if (elementPid != 0 && elementPid != targetInfo.ProcessId)
                    {
                        // Check if the element window has an ancestor matching targetInfo.Hwnd
                        int elemHwnd = resolvedElement.Current.NativeWindowHandle;
                        bool belongsToTarget = false;
                        if (elemHwnd != 0 && targetInfo.Hwnd != IntPtr.Zero)
                        {
                            IntPtr rootHwnd = GetAncestor(new IntPtr(elemHwnd), GA_ROOT);
                            if (rootHwnd == targetInfo.Hwnd || (IntPtr)elemHwnd == targetInfo.Hwnd)
                            {
                                belongsToTarget = true;
                            }
                        }

                        if (!belongsToTarget)
                        {
                            _logger?.LogWarning("Focused element belongs to PID {ElementPid} but foreground window is PID {TargetPid}. Unverified focus — failing closed.", elementPid, targetInfo.ProcessId);
                            return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to verify process correlation of focused element. Failing closed.");
                    return ContextSnapshot.CreateSensitive(sessionId, targetInfo);
                }
            }

            // 5. Bounded nearby text extraction (Max 200)
            int boundedNearby = Math.Clamp(maxNearbyCharacters, 0, ContextSnapshot.MaxNearbyCharacters);
            string nearbyText = GetNearbyContext(boundedNearby, resolvedElement);

            // 6. Bounded selection extraction (Max 10000)
            int boundedSelection = Math.Clamp(maxSelectionCharacters, 0, ContextSnapshot.MaxSelectionCharacters);
            string selectionText = GetSelectedText(boundedSelection, resolvedElement);

            return new ContextSnapshot(
                sessionId,
                DateTimeOffset.UtcNow,
                targetInfo,
                category,
                controlInfo,
                false,
                string.IsNullOrEmpty(nearbyText) ? null : nearbyText,
                string.IsNullOrEmpty(selectionText) ? null : selectionText,
                null,
                1.0f,
                "Windows.UIAutomation"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in CaptureContext; falling back to safe Empty snapshot.");
            return ContextSnapshot.CreateEmpty(sessionId);
        }
    }

    /// <inheritdoc />
    public FocusedControlInfo GetFocusedControlInfo() => GetFocusedControlInfo(null);

    /// <summary>
    /// Gets focused control info for the specified element or active element.
    /// </summary>
    public FocusedControlInfo GetFocusedControlInfo(AutomationElement? targetElement)
    {
        try
        {
            AutomationElement? focused = targetElement;
            if (focused == null)
            {
                try
                {
                    focused = AutomationElement.FocusedElement;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Unable to query AutomationElement.FocusedElement.");
                }
            }

            if (focused == null)
            {
                return FocusedControlInfo.Empty;
            }

            string controlType = "Unknown";
            try
            {
                controlType = focused.Current.ControlType?.ProgrammaticName ?? "Unknown";
            }
            catch { }

            string automationId = string.Empty;
            try
            {
                automationId = focused.Current.AutomationId ?? string.Empty;
            }
            catch { }

            string className = string.Empty;
            try
            {
                className = focused.Current.ClassName ?? string.Empty;
            }
            catch { }

            string name = string.Empty;
            try
            {
                name = focused.Current.Name ?? string.Empty;
            }
            catch { }

            bool isPassword = false;
            try
            {
                isPassword = IsElementSensitive(focused);
            }
            catch { }

            bool hasText = false;
            try
            {
                hasText = focused.TryGetCurrentPattern(TextPattern.Pattern, out _);
            }
            catch { }

            bool hasValue = false;
            try
            {
                hasValue = focused.TryGetCurrentPattern(ValuePattern.Pattern, out _);
            }
            catch { }

            return new FocusedControlInfo(controlType, automationId, className, name, isPassword, hasText, hasValue);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Exception in GetFocusedControlInfo; returning Empty.");
            return FocusedControlInfo.Empty;
        }
    }

    /// <inheritdoc />
    public ApplicationCategory GetApplicationCategory(ForegroundTargetInfo targetInfo)
    {
        return _classifier.Classify(targetInfo);
    }

    /// <inheritdoc />
    public bool ValidateTargetStillActive(ForegroundTargetInfo initialTarget)
    {
        if (initialTarget == null || initialTarget.Hwnd == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            // 1. Is the window handle still a valid window?
            if (!IsWindow(initialTarget.Hwnd))
            {
                _logger?.LogWarning("Target window HWND {Hwnd} is no longer valid.", initialTarget.Hwnd);
                return false;
            }

            // 2. Is it still the active foreground window?
            IntPtr currentForeground = GetForegroundWindow();
            if (currentForeground != initialTarget.Hwnd)
            {
                _logger?.LogWarning("Foreground window changed from HWND {InitialHwnd} to {CurrentHwnd}.", initialTarget.Hwnd, currentForeground);
                return false;
            }

            // 3. Is the process still alive and matching?
            GetWindowThreadProcessId(currentForeground, out uint currentPid);
            if (currentPid != initialTarget.ProcessId)
            {
                _logger?.LogWarning("Target PID changed from {InitialPid} to {CurrentPid}.", initialTarget.ProcessId, currentPid);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating target window liveness.");
            return false;
        }
    }

    #region Win32 Helpers

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        return GetWindowText(hWnd, sb, 256) > 0 ? sb.ToString() : string.Empty;
    }

    private static string GetProcessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return string.Empty;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion
}
