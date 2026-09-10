using System;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Tray;

/// <summary>
/// Native Windows notification area (System Tray) manager using Win32 Shell_NotifyIcon.
/// Ensures zero-dependency, ultra-lightweight presence in the Windows taskbar.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private bool _isAdded;
    private bool _isDisposed;

    public event Action? TrayClicked;
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
            if (eventId is 0x0202 or 0x0201) // WM_LBUTTONUP or WM_LBUTTONDOWN
            {
                TrayClicked?.Invoke();
            }
            else if (eventId == 0x0205) // WM_RBUTTONUP
            {
                ExitRequested?.Invoke();
            }
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    #endregion
}
