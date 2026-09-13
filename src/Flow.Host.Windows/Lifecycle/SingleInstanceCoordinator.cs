using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Flow.Host.Windows.Lifecycle;

/// <summary>
/// Robust single-instance coordinator using Windows named Mutex and custom Win32 broadcast messages.
/// Strictly guarantees that launching Flow.Host.Windows multiple times will never spawn duplicate background instances.
/// Instead, subsequent launches notify and focus the existing instance and immediately terminate.
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    public const string DefaultMutexName = @"Local\FLOW_VoiceProductivity_SingleInstance_Mutex_v1";
    public const string ActivationMessageName = "FLOW_ACTIVATE_MAIN_WINDOW_V1";

    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private bool _isDisposed;

    public static uint ActivationMessage { get; } = RegisterWindowMessage(ActivationMessageName);

    public SingleInstanceCoordinator(string? mutexName = null)
    {
        _mutexName = mutexName ?? DefaultMutexName;
    }

    /// <summary>
    /// Attempts to claim the single-instance mutex.
    /// Returns true if this is the primary instance; false if an instance is already running.
    /// </summary>
    public bool TryAcquireSingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, _mutexName, out _ownsMutex);
            return _ownsMutex;
        }
        catch (Exception)
        {
            _ownsMutex = false;
            return false;
        }
    }

    /// <summary>
    /// Signals the existing FLOW instance to bring its window to the foreground.
    /// </summary>
    public static void SignalExistingInstance()
    {
        PostMessage((IntPtr)0xFFFF /* HWND_BROADCAST */, ActivationMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_ownsMutex && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore release errors during process teardown
                }
                _mutex.Dispose();
                _mutex = null;
                _ownsMutex = false;
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Win32 Interop

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
