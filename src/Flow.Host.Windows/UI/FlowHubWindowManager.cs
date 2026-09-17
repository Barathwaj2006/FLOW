using System;
using System.Threading;
using System.Windows.Threading;
using Flow.Core.History;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Session;
using Flow.Host.Windows.Native;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Thread-safe window manager for presenting the WPF FlowHub Window
/// on an isolated STA thread without blocking or interfering with Win32 audio capture or hotkey loops.
/// </summary>
public static class FlowHubWindowManager
{
    private static readonly object s_lock = new();
    private static Thread? s_uiThread;
    private static IFlowMainWindow? s_window;
    private static Dispatcher? s_dispatcher;

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

    public static bool IsWebView2ShellActive
    {
        get
        {
            lock (s_lock)
            {
                return s_window is FlowShellWindow;
            }
        }
    }

    public static event Action? ExitApplicationRequested;

    /// <summary>
    /// Verifies if Microsoft Edge WebView2 Runtime is installed and available on this machine.
    /// </summary>
    public static bool IsWebView2Available()
    {
        try
        {
            string version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shows the FLOW Hub window. Uses modern embedded FlowShellWindow (WebView2) if available,
    /// or seamlessly falls back to native WPF FlowHubWindow if the runtime is absent.
    /// </summary>
    public static void ShowWindow(
        VoiceSessionCoordinator? coordinator = null,
        WasapiAudioCapture? capture = null,
        WasapiDeviceManager? deviceManager = null,
        IHistoryService? historyService = null,
        IScratchpadService? scratchpadService = null,
        IPersonalDictionaryRepository? dictRepo = null,
        PersonalDictionaryEngine? dictEngine = null,
        ISnippetRepository? snippetRepo = null,
        SnippetExpansionEngine? snippetEngine = null,
        IStyleRepository? styleRepo = null,
        StyleFormattingEngine? styleEngine = null,
        Flow.Core.Storage.ISettingsRepository? settingsRepo = null,
        GlobalHotkeyHook? hotkeyHook = null,
        int targetTab = 0,
        Flow.Inference.WhisperModelManager? modelManager = null,
        Updates.IFlowUpdateService? updateService = null)
    {
        lock (s_lock)
        {
            if (s_window != null && s_dispatcher != null && !s_dispatcher.HasShutdownStarted)
            {
                s_dispatcher.BeginInvoke(() =>
                {
                    if (s_window != null)
                    {
                        if (s_window.Visibility != System.Windows.Visibility.Visible)
                        {
                            s_window.Show();
                        }
                        if (s_window.WindowState == System.Windows.WindowState.Minimized)
                        {
                            s_window.WindowState = System.Windows.WindowState.Normal;
                        }
                        s_window.SelectTab(targetTab);
                        s_window.Activate();
                        s_window.Focus();
                    }
                });
                return;
            }

            var readyEvent = new ManualResetEventSlim(false);

            s_uiThread = new Thread(() =>
            {
                try
                {
                    s_dispatcher = Dispatcher.CurrentDispatcher;

                    bool useWebView2 = false;
                    try
                    {
                        useWebView2 = IsWebView2Available();
                    }
                    catch
                    {
                        useWebView2 = false;
                    }

                    if (useWebView2)
                    {
                        try
                        {
                            s_window = new FlowShellWindow(
                                coordinator,
                                capture,
                                deviceManager,
                                historyService,
                                scratchpadService,
                                dictRepo,
                                dictEngine,
                                snippetRepo,
                                snippetEngine,
                                styleRepo,
                                styleEngine,
                                settingsRepo,
                                hotkeyHook,
                                modelManager,
                                updateService);
                        }
                        catch (Exception shellEx)
                        {
                            s_window = null;
                            try
                            {
                                string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW");
                                System.IO.Directory.CreateDirectory(logDir);
                                System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "shell_fallback.log"), $"[{DateTime.UtcNow:O}] WebView2 Shell Init Failed, falling back to WPF:\n{shellEx}\n\n");
                            }
                            catch { }
                        }
                    }

                    if (s_window == null)
                    {
                        s_window = new FlowHubWindow(
                            coordinator,
                            capture,
                            deviceManager,
                            historyService,
                            scratchpadService,
                            dictRepo,
                            dictEngine,
                            snippetRepo,
                            snippetEngine,
                            styleRepo,
                            styleEngine,
                            settingsRepo,
                            hotkeyHook);
                    }

                    s_window.ExitApplicationRequested += () =>
                    {
                        ExitApplicationRequested?.Invoke();
                    };

                    if (s_window is System.Windows.Window wpfWindow)
                    {
                        wpfWindow.Closed += (sender, args) =>
                        {
                            lock (s_lock)
                            {
                                s_window = null;
                                s_dispatcher = null;
                                s_uiThread = null;
                            }
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        };
                    }

                    s_window.SelectTab(targetTab);
                    s_window.Show();
                    readyEvent.Set();

                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    try
                    {
                        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW");
                        System.IO.Directory.CreateDirectory(logDir);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "startup_crash.log"), $"[{DateTime.UtcNow:O}] UI THREAD CRASH:\n{ex}\n\n");
                    }
                    catch { }
                    readyEvent.Set();
                }
            })
            {
                IsBackground = true,
                Name = "Flow.Hub.UIThread"
            };

            s_uiThread.SetApartmentState(ApartmentState.STA);
            s_uiThread.Start();

            readyEvent.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Brings the window to the foreground if open.
    /// </summary>
    public static void BringToForeground()
    {
        lock (s_lock)
        {
            if (s_window != null && s_dispatcher != null && !s_dispatcher.HasShutdownStarted)
            {
                s_dispatcher.BeginInvoke(() =>
                {
                    if (s_window != null)
                    {
                        if (s_window.Visibility != System.Windows.Visibility.Visible)
                        {
                            s_window.Show();
                        }
                        if (s_window.WindowState == System.Windows.WindowState.Minimized)
                        {
                            s_window.WindowState = System.Windows.WindowState.Normal;
                        }
                        s_window.Activate();
                        s_window.Focus();
                    }
                });
            }
            else
            {
                ShowWindow();
            }
        }
    }

    /// <summary>
    /// Closes the FLOW Hub window.
    /// </summary>
    public static void CloseWindow()
    {
        lock (s_lock)
        {
            if (s_window != null && s_dispatcher != null && !s_dispatcher.HasShutdownStarted)
            {
                s_dispatcher.Invoke(() =>
                {
                    if (s_window != null)
                    {
                        s_window.AllowRealClose = true;
                        s_window.Close();
                    }
                });
            }
        }
    }
}
