using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Flow.Core.Session;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Non-activating floating HUD window using native Win32 (WS_EX_NOACTIVATE | WS_EX_TOPMOST).
/// Strictly prevents focus stealing from active text input controls during dictation.
/// Adheres to FLOW Anti-Vibecode Design System: Segoe UI, subtle dark surface, quiet status indicator.
/// </summary>
public sealed class FloatingHudController : IDisposable
{
    private IntPtr _hwnd = IntPtr.Zero;
    private SessionState _currentState = SessionState.Idle;
    private string _statusText = "Ready";
    private float _audioLevel;
    private bool _isDisposed;

    public IntPtr Handle => _hwnd;
    public bool IsVisible => _hwnd != IntPtr.Zero && IsWindowVisible(_hwnd);

    public FloatingHudController()
    {
    }

    /// <summary>
    /// Updates the HUD display with the current voice session state.
    /// </summary>
    public void UpdateState(SessionState state, string? detail = null)
    {
        _currentState = state;
        _statusText = detail ?? state switch
        {
            SessionState.Recording => "Listening...",
            SessionState.Processing => "Transcribing...",
            SessionState.Inserting => "Inserting...",
            SessionState.Completed => "Done",
            SessionState.Cancelled => "Cancelled",
            SessionState.Error => "Error",
            _ => "Ready"
        };

        if (state == SessionState.Idle)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Updates the real-time RMS audio level indicator.
    /// </summary>
    public void UpdateAudioLevel(float rms)
    {
        _audioLevel = Math.Clamp(rms * 5.0f, 0.0f, 1.0f);
        if (IsVisible && _hwnd != IntPtr.Zero)
        {
            InvalidateRect(_hwnd, IntPtr.Zero, true);
        }
    }

    public void Show()
    {
        // Displays non-activating top-most window
        if (_hwnd != IntPtr.Zero)
        {
            ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    public void Hide()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ShowWindow(_hwnd, SW_HIDE);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Win32 Constants and P/Invoke

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    #endregion
}
