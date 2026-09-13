using System;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Tray;

/// <summary>
/// Native Windows notification area (System Tray) manager using Win32 Shell_NotifyIcon.
/// Ensures zero-dependency, ultra-lightweight presence in the Windows taskbar.
/// Generates crisp native GDI icon handles and rich context menus for FLOW Hub, History, Scratchpad, and Settings.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded;
    private bool _isDisposed;

    public event Action? TrayClicked;
    public event Action? FlowHubRequested;
    public event Action? ToggleDictationRequested;
    public event Action? ScratchpadRequested;
    public event Action? HistoryRequested;
    public event Action? SettingsRequested;
    public event Action? DeveloperModeToggled;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public bool IsDeveloperModeEnabled { get; set; }

    public TrayIconManager(IntPtr hwnd, uint callbackMessage = 0x8001)
    {
        _hwnd = hwnd;
        _callbackMessage = callbackMessage;
        _hIcon = CreateDefaultFlowIconHandle();
    }

    /// <summary>
    /// Processes Windows messages to detect clicks on the notification icon.
    /// </summary>
    public void ProcessMessage(uint msg, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            int eventId = lParam.ToInt32();
            if (eventId is 0x0202 or 0x0203) // WM_LBUTTONUP or WM_LBUTTONDBLCLK
            {
                TrayClicked?.Invoke();
                FlowHubRequested?.Invoke();
            }
            else if (eventId == 0x0205) // WM_RBUTTONUP
            {
                ShowContextMenu();
            }
        }
    }

    /// <summary>
    /// Displays native Win32 context menu at the mouse cursor position.
    /// </summary>
    private void ShowContextMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            ExitRequested?.Invoke();
            return;
        }

        try
        {
            const uint MF_STRING = 0x00000000;
            const uint MF_SEPARATOR = 0x00000800;
            const uint TPM_RETURNCMD = 0x0100;
            const uint TPM_RIGHTBUTTON = 0x0002;

            const uint CMD_HUB = 100;
            const uint CMD_TOGGLE_DICTATION = 101;
            const uint CMD_SCRATCHPAD = 102;
            const uint CMD_HISTORY = 103;
            const uint CMD_SETTINGS = 104;
            const uint CMD_DEV_MODE = 105;
            const uint CMD_ABOUT = 106;
            const uint CMD_EXIT = 107;

            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_HUB, "Open FLOW Hub");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_TOGGLE_DICTATION, "Toggle Dictation (Hands-Free)");
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_SCRATCHPAD, "Scratchpad");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_HISTORY, "History");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_SETTINGS, "Settings");
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_ABOUT, "About FLOW");
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_EXIT, "Exit FLOW");

            GetCursorPos(out POINT pt);
            if (_hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(_hwnd);
            }

            uint selected = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            switch (selected)
            {
                case CMD_HUB:
                    FlowHubRequested?.Invoke();
                    break;
                case CMD_TOGGLE_DICTATION:
                    ToggleDictationRequested?.Invoke();
                    break;
                case CMD_SCRATCHPAD:
                    ScratchpadRequested?.Invoke();
                    break;
                case CMD_HISTORY:
                    HistoryRequested?.Invoke();
                    break;
                case CMD_SETTINGS:
                    SettingsRequested?.Invoke();
                    break;
                case CMD_DEV_MODE:
                    IsDeveloperModeEnabled = !IsDeveloperModeEnabled;
                    DeveloperModeToggled?.Invoke();
                    break;
                case CMD_ABOUT:
                    AboutRequested?.Invoke();
                    break;
                case CMD_EXIT:
                    ExitRequested?.Invoke();
                    break;
                default:
                    // User dismissed context menu without selection (return 0) -> safe no-op
                    break;
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    /// <summary>
    /// Installs the icon into the Windows notification tray with initial welcoming toast notification.
    /// </summary>
    public void Install(string tooltip = "FLOW — Local Voice Dictation (Right-Alt to speak)")
    {
        if (_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP | NIF_INFO,
            uCallbackMessage = _callbackMessage,
            hIcon = _hIcon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfoTitle = "FLOW — Voice Productivity",
            szInfo = "FLOW is active. Hold [Right Alt] to dictate anywhere, or click for FLOW Hub.",
            dwInfoFlags = 0x00000001 /* NIIF_INFO */
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    /// <summary>
    /// Updates the tray icon tooltip text.
    /// </summary>
    public void UpdateTooltip(string tooltip)
    {
        if (!_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_TIP | NIF_SHOWTIP,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    /// <summary>
    /// Shows a temporary notification bubble from the tray.
    /// </summary>
    public void ShowBalloon(string title, string message)
    {
        if (!_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_INFO,
            szInfoTitle = title.Length > 63 ? title[..63] : title,
            szInfo = message.Length > 255 ? message[..255] : message,
            dwInfoFlags = 0x00000001 /* NIIF_INFO */
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    /// <summary>
    /// Removes the icon from the Windows notification tray.
    /// </summary>
    public void Remove()
    {
        if (!_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001
        };

        Shell_NotifyIcon(NIM_DELETE, ref nid);
        _isAdded = false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Remove();
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Generates a crisp, native 32x32 GDI icon handle for FLOW: Solid deep-blue circle with white F.
    /// Pure Win32 GDI implementation without external WinForms or System.Drawing dependencies.
    /// </summary>
    private static IntPtr CreateDefaultFlowIconHandle()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return LoadIcon(IntPtr.Zero, (IntPtr)32512 /* IDI_APPLICATION */);

        IntPtr memDc = CreateCompatibleDC(hdc);
        IntPtr hbmColor = CreateCompatibleBitmap(hdc, 32, 32);
        IntPtr hbmMask = CreateCompatibleBitmap(hdc, 32, 32);

        try
        {
            // 1. Draw color bitmap
            IntPtr oldBmp = SelectObject(memDc, hbmColor);

            // Background fill: Dark Slate Blue (0x00EB6325 in BGR -> #2563EB)
            IntPtr brush = CreateSolidBrush(0x00EB6325);
            RECT fullRect = new() { Left = 0, Top = 0, Right = 32, Bottom = 32 };
            FillRect(memDc, ref fullRect, brush);
            DeleteObject(brush);

            // Circular inner badge
            IntPtr pen = CreatePen(0, 2, 0x00E6C700); // Cyan BGR: #00C7E6
            IntPtr oldPen = SelectObject(memDc, pen);
            IntPtr circleBrush = CreateSolidBrush(0x003B291E); // Slate: #1E293B
            IntPtr oldBrush = SelectObject(memDc, circleBrush);
            Ellipse(memDc, 2, 2, 30, 30);
            SelectObject(memDc, oldBrush);
            SelectObject(memDc, oldPen);
            DeleteObject(circleBrush);
            DeleteObject(pen);

            // Bold text 'F'
            SetBkMode(memDc, 1 /* TRANSPARENT */);
            SetTextColor(memDc, 0x00FFFFFF /* WHITE */);
            IntPtr hFont = CreateFont(
                20, 0, 0, 0, 700 /* FW_BOLD */,
                0, 0, 0, 1 /* DEFAULT_CHARSET */,
                0, 0, 0, 0, "Segoe UI"
            );
            IntPtr oldFont = SelectObject(memDc, hFont);
            RECT textRect = new() { Left = 0, Top = 2, Right = 32, Bottom = 32 };
            DrawText(memDc, "F", 1, ref textRect, 0x00000001 /* DT_CENTER */ | 0x00000004 /* DT_VCENTER */ | 0x00000020 /* DT_SINGLELINE */);
            SelectObject(memDc, oldFont);
            DeleteObject(hFont);

            SelectObject(memDc, oldBmp);

            // 2. Prepare mask bitmap (black = opaque, white = transparent)
            oldBmp = SelectObject(memDc, hbmMask);
            IntPtr blackBrush = CreateSolidBrush(0x00000000);
            FillRect(memDc, ref fullRect, blackBrush);
            DeleteObject(blackBrush);
            SelectObject(memDc, oldBmp);

            // 3. Create native icon from bitmaps
            ICONINFO iconInfo = new()
            {
                fIcon = true,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = hbmMask,
                hbmColor = hbmColor
            };

            IntPtr hIcon = CreateIconIndirect(ref iconInfo);
            return hIcon != IntPtr.Zero ? hIcon : LoadIcon(IntPtr.Zero, (IntPtr)32512);
        }
        catch
        {
            return LoadIcon(IntPtr.Zero, (IntPtr)32512);
        }
        finally
        {
            DeleteObject(hbmColor);
            DeleteObject(hbmMask);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    #region Win32 Interop

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIF_SHOWTIP = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
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
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO iconInfo);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref RECT lprc, uint format);

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
    private static extern bool Ellipse(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int iBkMode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint crColor);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality, uint fdwPitchAndFamily,
        string lpszFace);

    #endregion
}
