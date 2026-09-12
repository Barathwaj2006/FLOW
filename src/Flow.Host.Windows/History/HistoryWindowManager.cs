using System;
using System.Threading;
using System.Windows.Threading;
using Flow.Core.History;

namespace Flow.Host.Windows.History;

/// <summary>
/// Thread-safe window manager for presenting the WPF History &amp; Productivity Window
/// on an isolated STA thread without blocking or interfering with Win32 audio capture or hotkey loops.
/// </summary>
public static class HistoryWindowManager
{
    private static readonly object s_lock = new();
    private static Thread? s_uiThread;
    private static HistoryWindow? s_window;
    private static Dispatcher? s_dispatcher;

    /// <summary>
    /// Gets whether the History Window is currently open and active.
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
    /// Shows the History Window. If already open, brings it to foreground.
    /// </summary>
    public static void ShowWindow(IHistoryService historyService)
    {
        ArgumentNullException.ThrowIfNull(historyService);

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

                var historyVm = new HistoryViewModel(historyService);
                var statsVm = new StatisticsViewModel(historyService);

                s_window = new HistoryWindow(historyVm, statsVm);

                s_window.Closed += (sender, e) =>
                {
                    lock (s_lock)
                    {
                        s_window = null;
                        s_dispatcher = null;
                    }
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                };

                s_window.Show();
                readyEvent.Set();

                Dispatcher.Run();
            });

            s_uiThread.SetApartmentState(ApartmentState.STA);
            s_uiThread.IsBackground = true;
            s_uiThread.Name = "Flow.HistoryUI.Thread";
            s_uiThread.Start();

            readyEvent.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Closes the History Window if open without deadlock.
    /// </summary>
    public static void CloseWindow()
    {
        Dispatcher? disp;
        HistoryWindow? win;
        Thread? thread;

        lock (s_lock)
        {
            disp = s_dispatcher;
            win = s_window;
            thread = s_uiThread;
        }

        if (win != null && disp != null && !disp.HasShutdownStarted)
        {
            try
            {
                disp.Invoke(() => win.Close());
            }
            catch
            {
                // Ignored if dispatcher already shutting down
            }
        }

        if (thread != null && thread.IsAlive)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
