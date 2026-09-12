using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Flow.Core.Session;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Production non-activating floating HUD window using native Win32 (WS_EX_NOACTIVATE | WS_EX_TOPMOST).
/// Strictly prevents focus stealing from active text input controls during dictation.
/// Paints a clean Fluent dark surface, state indicator dot, Segoe UI text, and real-time audio waveform activity.
/// </summary>
public sealed class FloatingHudController : IDisposable
{
    private const string WindowClassName = "Flow.FloatingHudClass";
    private const int HudWidth = 280;
    private const int HudHeight = 44;

    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static readonly WndProcDelegate s_staticWndProc = StaticWndProc;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, FloatingHudController> s_instances = new();

    private IntPtr _hwnd = IntPtr.Zero;
    private SessionState _currentState = SessionState.Idle;
    private string _statusText = "Ready";
    private bool _isCommandMode;
    private float _audioLevel;
    private bool _isDisposed;

    public IntPtr Handle => _hwnd;
    public bool IsVisible => _hwnd != IntPtr.Zero && IsWindowVisible(_hwnd);
    public string StatusText => _statusText;
    public bool IsCommandMode => _isCommandMode;
    public float AudioLevel => _audioLevel;

    public FloatingHudController()
    {
        InitializeWindow();
    }

    private static void EnsureClassRegistered()
    {
        lock (s_classLock)
        {
            if (s_classRegistered) return;

            var wcx = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                style = 0x0002 /* CS_HREDRAW */ | 0x0001 /* CS_VREDRAW */,
                lpfnWndProc = s_staticWndProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = LoadCursor(IntPtr.Zero, 32512 /* IDC_ARROW */),
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero
            };

            RegisterClassEx(ref wcx);
            s_classRegistered = true;
        }
    }

    private void InitializeWindow()
    {
        try
        {
            EnsureClassRegistered();

            int screenW = GetSystemMetrics(0 /* SM_CXSCREEN */);
            int screenH = GetSystemMetrics(1 /* SM_CYSCREEN */);
            int x = (screenW - HudWidth) / 2;
            int y = screenH - HudHeight - 80;

            const uint WS_POPUP = 0x80000000;
            const uint WS_EX_TOPMOST = 0x00000008;
            const uint WS_EX_TOOLWINDOW = 0x00000080;
            const uint WS_EX_NOACTIVATE = 0x08000000;

            _hwnd = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                WindowClassName,
                "FLOW Floating HUD",
                WS_POPUP,
                x, y, HudWidth, HudHeight,
                IntPtr.Zero, IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero
            );

            if (_hwnd != IntPtr.Zero)
            {
                s_instances[_hwnd] = this;
            }
        }
        catch
        {
            // Fails safe if in non-GUI / test environment
        }
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (s_instances.TryGetValue(hWnd, out var instance))
        {
            return instance.CustomWndProc(hWnd, msg, wParam, lParam);
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Updates the HUD display with the current voice session state.
    /// </summary>
    public void UpdateState(SessionState state, string? detail = null, bool isCommandMode = false)
    {
        _currentState = state;
        _isCommandMode = isCommandMode;
        _statusText = detail ?? state switch
        {
            SessionState.Recording => isCommandMode ? "🪄 Command: Listening..." : "Listening...",
            SessionState.Processing => isCommandMode ? "🪄 Transforming..." : "Transcribing...",
            SessionState.Inserting => isCommandMode ? "🪄 Applying..." : "Inserting...",
            SessionState.Backtracking => "Backtracking...",
            SessionState.Completed => isCommandMode ? "🪄 Transformed" : "Done",
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
            if (_hwnd != IntPtr.Zero)
            {
                InvalidateRect(_hwnd, IntPtr.Zero, false);
            }
        }
    }

    /// <summary>
    /// Updates the real-time RMS audio level indicator and redraws the waveform bars.
    /// </summary>
    public void UpdateAudioLevel(float rms)
    {
        _audioLevel = Math.Clamp(rms * 6.0f, 0.0f, 1.0f);
        if (IsVisible && _hwnd != IntPtr.Zero)
        {
            InvalidateRect(_hwnd, IntPtr.Zero, false);
        }
    }

    public void Show()
    {
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

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_PAINT = 0x000F;
        const uint WM_ERASEBKGND = 0x0014;
        const uint WM_MOUSEACTIVATE = 0x0021;
        const uint WM_NCHITTEST = 0x0084;
        const int MA_NOACTIVATE = 3;
        const int HTTRANSPARENT = -1;

        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                return (IntPtr)MA_NOACTIVATE;

            case WM_NCHITTEST:
                // Mouse clicks pass straight through to target application
                return (IntPtr)HTTRANSPARENT;

            case WM_ERASEBKGND:
                return (IntPtr)1; // Double buffered, avoid flicker

            case WM_PAINT:
                PaintHud(hWnd);
                return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void PaintHud(IntPtr hWnd)
    {
        IntPtr hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);
        if (hdc == IntPtr.Zero) return;

        try
        {
            GetClientRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            IntPtr memDc = CreateCompatibleDC(hdc);
            IntPtr memBmp = CreateCompatibleBitmap(hdc, width, height);
            IntPtr oldBmp = SelectObject(memDc, memBmp);

            try
            {
                // 1. Dark pill background
                IntPtr bgBrush = CreateSolidBrush(RGB(24, 24, 28));
                IntPtr borderPen = CreatePen(0 /* PS_SOLID */, 1, RGB(52, 52, 58));
                IntPtr oldBrush = SelectObject(memDc, bgBrush);
                IntPtr oldPen = SelectObject(memDc, borderPen);

                RoundRect(memDc, 0, 0, width, height, 22, 22);

                SelectObject(memDc, oldBrush);
                SelectObject(memDc, oldPen);
                DeleteObject(bgBrush);
                DeleteObject(borderPen);

                // 2. Status Dot
                uint dotColor = _currentState switch
                {
                    SessionState.Recording => RGB(245, 75, 75),   // Coral/Red
                    SessionState.Processing => RGB(85, 150, 255),  // Blue
                    SessionState.Inserting => RGB(165, 105, 255), // Purple
                    SessionState.Completed => RGB(55, 205, 115),  // Green
                    SessionState.Cancelled => RGB(235, 160, 50),  // Amber
                    SessionState.Error => RGB(245, 60, 60),       // Red
                    _ => RGB(120, 120, 130)                       // Gray
                };

                IntPtr dotBrush = CreateSolidBrush(dotColor);
                IntPtr dotPen = CreatePen(0, 1, dotColor);
                oldBrush = SelectObject(memDc, dotBrush);
                oldPen = SelectObject(memDc, dotPen);

                Ellipse(memDc, 14, (height - 8) / 2, 14 + 8, (height - 8) / 2 + 8);

                SelectObject(memDc, oldBrush);
                SelectObject(memDc, oldPen);
                DeleteObject(dotBrush);
                DeleteObject(dotPen);

                // 3. Status Text (Segoe UI)
                SetBkMode(memDc, 1 /* TRANSPARENT */);
                SetTextColor(memDc, RGB(230, 230, 235));

                IntPtr hFont = CreateFont(
                    15, 0, 0, 0, 500 /* FW_MEDIUM */,
                    0, 0, 0, 1 /* DEFAULT_CHARSET */,
                    0, 0, 0, 0, "Segoe UI"
                );
                IntPtr oldFont = SelectObject(memDc, hFont);

                RECT textRect = new() { Left = 30, Top = 0, Right = width - 70, Bottom = height };
                DrawText(memDc, _statusText, -1, ref textRect, 0x00000004 /* DT_VCENTER */ | 0x00000020 /* DT_SINGLELINE */ | 0x00000040 /* DT_END_ELLIPSIS */);

                SelectObject(memDc, oldFont);
                DeleteObject(hFont);

                // 4. Real Waveform Foundation Indicator (5 dynamic bars based on RMS audio level)
                if (_currentState == SessionState.Recording)
                {
                    int barBaseX = width - 58;
                    int barW = 3;
                    int gap = 3;
                    float[] weights = { 0.5f, 0.85f, 1.0f, 0.85f, 0.5f };

                    uint barColor = _isCommandMode ? RGB(180, 120, 255) : RGB(100, 200, 255);
                    IntPtr barBrush = CreateSolidBrush(barColor);
                    oldBrush = SelectObject(memDc, barBrush);

                    for (int i = 0; i < 5; i++)
                    {
                        float barNorm = Math.Clamp(_audioLevel * weights[i], 0.0f, 1.0f);
                        int barH = Math.Max(3, (int)(barNorm * 22));
                        int bx = barBaseX + i * (barW + gap);
                        int by = (height - barH) / 2;

                        RECT barRect = new() { Left = bx, Top = by, Right = bx + barW, Bottom = by + barH };
                        FillRect(memDc, ref barRect, barBrush);
                    }

                    SelectObject(memDc, oldBrush);
                    DeleteObject(barBrush);
                }

                // BitBlt to screen DC
                BitBlt(hdc, 0, 0, width, height, memDc, 0, 0, 0x00CC0020 /* SRCCOPY */);
            }
            finally
            {
                SelectObject(memDc, oldBmp);
                DeleteObject(memBmp);
                DeleteDC(memDc);
            }
        }
        finally
        {
            EndPaint(hWnd, ref ps);
        }
    }

    private static uint RGB(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_hwnd != IntPtr.Zero)
            {
                s_instances.TryRemove(_hwnd, out _);
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

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

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

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int DrawText(IntPtr hDC, string lpchText, int nCount, ref RECT lpRect, uint uFormat);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern bool RoundRect(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern bool Ellipse(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int iBkMode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint crColor);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
        uint fdwPitchAndFamily, string lpszFace);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion
}
