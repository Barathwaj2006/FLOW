using System;
using System.Threading;
using System.Windows.Threading;
using Flow.Core.Scratchpad;

namespace Flow.Host.Windows.Scratchpad;

/// <summary>
/// Thread-safe window manager for presenting the WPF Scratchpad Window
/// on an isolated STA thread without blocking or interfering with Win32 audio capture or hotkey loops.
/// </summary>
public static class ScratchpadWindowManager
{
    private static readonly object s_lock = new();
    private static Thread? s_uiThread;
    private static ScratchpadWindow? s_window;
    private static Dispatcher? s_dispatcher;

    /// <summary>
    /// Gets whether the Scratchpad Window is currently open and active.
    /// </summary>
    public static bool IsOpen
    {
        get
        {
            lock (s_lock)
            {
                return s_window != null;
            }
        }
    }

    /// <summary>
    /// Shows the Scratchpad Window. If already open, brings it to foreground.
    /// </summary>
    public static void ShowWindow(IScratchpadService scratchpadService)
    {
        ArgumentNullException.ThrowIfNull(scratchpadService);

        lock (s_lock)
        {
            if (s_window != null && s_dispatcher != null && !s_dispatcher.HasShutdownStarted)
            {
                s_dispatcher.BeginInvoke(() =>
                {
                    if (s_window != null)
                    {
                        if (s_window.WindowState == System.Windows.WindowState.Minimized)
                        {
                            s_window.WindowState = System.Windows.WindowState.Normal;
                        }
                        s_window.Activate();
                        s_window.Focus();
                    }
                });
                return;
            }

            var readyEvent = new ManualResetEventSlim(false);

            s_uiThread = new Thread(() =>
            {
                s_dispatcher = Dispatcher.CurrentDispatcher;

                var viewModel = new ScratchpadViewModel(scratchpadService);
                s_window = new ScratchpadWindow(viewModel);

                s_window.Closed += (sender, e) =>
                {
                    lock (s_lock)
                    {
                        s_window = null;
                        s_dispatcher = null;
                    }
                    viewModel.Dispose();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                };

                s_window.Show();
                readyEvent.Set();

                Dispatcher.Run();
            });

            s_uiThread.SetApartmentState(ApartmentState.STA);
            s_uiThread.IsBackground = true;
            s_uiThread.Name = "Flow.ScratchpadUI.Thread";
            s_uiThread.Start();

            readyEvent.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Closes the Scratchpad Window if open without deadlock.
    /// </summary>
    public static void CloseWindow()
    {
        Dispatcher? disp;
        lock (s_lock)
        {
            disp = s_dispatcher;
        }

        if (disp != null && !disp.HasShutdownStarted)
        {
            try
            {
                disp.Invoke(() =>
                {
                    lock (s_lock)
                    {
                        s_window?.Close();
                    }
                });
            }
            catch
            {
                // Safe ignore if dispatcher is closing
            }
        }
    }
}
