using System;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Tray;

/// <summary>
/// Native Windows notification area (System Tray) manager using Win32 Shell_NotifyIcon.
/// Ensures zero-dependency, ultra-lightweight presence in the Windows taskbar.
/// Supports context menu for History &amp; Productivity and safe exit.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private bool _isAdded;
    private bool _isDisposed;

    public event Action? TrayClicked;
    public event Action? HistoryRequested;
    public event Action? ExitRequested;

    public TrayIconManager(IntPtr hwnd, uint callbackMessage = 0x8001)
    {
        _hwnd = hwnd;
        _callbackMessage = callbackMessage;
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
                HistoryRequested?.Invoke();
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

            const uint CMD_HISTORY = 101;
            const uint CMD_EXIT = 102;

            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_HISTORY, "History & Productivity");
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_EXIT, "Exit FLOW");

            GetCursorPos(out POINT pt);
            if (_hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(_hwnd);
            }

            uint selected = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            if (selected == CMD_HISTORY)
            {
                HistoryRequested?.Invoke();
            }
            else if (selected == CMD_EXIT)
            {
                ExitRequested?.Invoke();
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    /// <summary>
    /// Installs the icon into the Windows notification tray.
    /// </summary>
    public void Install(string tooltip = "FLOW — Local Voice Dictation")
    {
        if (_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_TIP | NIF_SHOWTIP,
            uCallbackMessage = _callbackMessage,
            szTip = tooltip
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
            szTip = tooltip
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
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Win32 Interop

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
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

    #endregion
}
