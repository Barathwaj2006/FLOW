using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Commands;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Storage;
using Flow.Core.TranscriptProcessing;
using Flow.Core.History;
using Flow.Core.Scratchpad;
using Flow.Host.Windows.Lifecycle;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Tray;
using Flow.Host.Windows.UI;
using Flow.Host.Windows.Updates;
using Flow.Inference;
using Microsoft.Extensions.Logging;
using Velopack;

namespace Flow.Host.Windows;

public static class Program
{
    private static VoiceSessionCoordinator? _coordinator;
    private static WasapiAudioCapture? _capture;
    private static GlobalHotkeyHook? _hotkeyHook;
    private static FloatingHudController? _hud;
    private static TrayIconManager? _tray;
    private static LocalApiServer? _localApiServer;
    private static WindowsPowerStateManager? _powerManager;
    private static IFlowUpdateService? _updateService;

    [STAThread]
    public static int Main(string[] args)
    {
        // 0. Velopack application lifecycle hook
        // MUST run before any mutex, single-instance coordination, or GUI creation.
        // Handles --veloapp-install, --veloapp-updated, --veloapp-uninstall.
        VelopackApp.Build().Run();

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            try
            {
                string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW");
                System.IO.Directory.CreateDirectory(logDir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "startup_crash.log"), $"[{DateTime.UtcNow:O}] FATAL CRASH:\n{ex}\n\n");
            }
            catch { }
            Console.Error.WriteLine($"[FLOW FATAL ERROR] {ex}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        // 0. Attach to parent console if launched from PowerShell/CMD
        bool isConsoleAttached = EnsureConsoleOutput();
        if (isConsoleAttached)
        {
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine("FLOW — AI Voice Productivity Platform (Windows Native x64)");
            Console.WriteLine("Status: Active & Ready");
            Console.WriteLine("Push-to-Talk: Hold [Alt + Space]");
            Console.WriteLine("Hands-Free: [Alt + B]");
            Console.WriteLine("Floating HUD: Click to Dictate");
            Console.WriteLine("Cancel: [Esc] | FLOW Hub: Active");
            Console.WriteLine("============================================================");
        }

        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine();
            Console.WriteLine("Usage: Flow.Host.Windows.exe [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --help, -h          Show this help message and exit.");
            Console.WriteLine("  --status            Display audio device, model, and system diagnostics, then exit.");
            Console.WriteLine("  --minimized, --tray Start quietly minimized in the Windows notification tray.");
            Console.WriteLine();
            Console.WriteLine("Core Dictation Shortcuts:");
            Console.WriteLine("  Hold [Alt + Space]      Hold-to-talk voice dictation");
            Console.WriteLine("  [Alt + B]               Hands-free toggle mode");
            Console.WriteLine("  Click Floating HUD      Start/stop dictation without focus loss");
            Console.WriteLine("  [Esc]                   Cancel active recording");
            return 0;
        }

        if (args.Any(a => a.Equals("--status", StringComparison.OrdinalIgnoreCase)))
        {
            var devMgr = new WasapiDeviceManager();
            var devices = devMgr.EnumerateCaptureDevices();
            var defaultDev = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
            var modelMgr = new WhisperModelManager();

            Console.WriteLine();
            Console.WriteLine("FLOW Diagnostic Status Report:");
            Console.WriteLine($"  - Application Version:   1.0.0 (Phase 9 Release)");
            Console.WriteLine($"  - Windows Version:       {Environment.OSVersion}");
            Console.WriteLine($"  - Architecture:          {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"  - Microphone Status:     {(defaultDev != null ? defaultDev.Name : "No microphone detected")}");
            Console.WriteLine($"  - Devices Detected:      {devices.Count}");
            Console.WriteLine($"  - Whisper Model:         {modelMgr.ActiveProfile.Name} (Installed: {modelMgr.IsModelInstalledAndValid()})");
            Console.WriteLine($"  - Model Path:            {modelMgr.ModelPath}");
            Console.WriteLine($"  - Database Path:         {System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW", "flow_personalization.db")}");
            Console.WriteLine($"  - Offline Sovereignty:   100% Offline (Zero Cloud Audio)");
            Console.WriteLine();
            return 0;
        }

        // 1. Enforce strict single-instance execution
        using var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.TryAcquireSingleInstance())
        {
            if (isConsoleAttached)
            {
                Console.WriteLine("[FLOW] Another instance of FLOW is already active on this system.");
                Console.WriteLine("[FLOW] Signaled existing instance to bring its window to foreground.");
            }
            SingleInstanceCoordinator.SignalExistingInstance();
            return 0;
        }

        bool startMinimized = args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--background", StringComparison.OrdinalIgnoreCase));

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        var logger = loggerFactory.CreateLogger("Flow.Main");
        logger.LogInformation("Starting FLOW Windows Native Voice Platform...");

        // 2. Core audio & inference services
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 30.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.015f, silenceThresholdSeconds: 0.45);

        // Production ASR Engine: Native Whisper.net local inference with CPU AVX2 & GPU acceleration
        var asrRegistry = new ASREngineRegistry();
        var modelManager = new WhisperModelManager(null, loggerFactory.CreateLogger<WhisperModelManager>());
        var whisperInference = new WhisperNetInferenceEngine(modelManager, loggerFactory.CreateLogger<WhisperNetInferenceEngine>());
        var localWhisper = new LocalWhisperEngine(whisperInference, modelManager.ModelPath);

        // Register strictly production ASR (zero mocks in production path)
        asrRegistry.Register(localWhisper, isDefault: true, priority: 10);

        // Personalization persistence & engines
        var personalizationDb = new SqlitePersonalizationDatabase();
        var dictRepo = new SqlitePersonalDictionaryRepository(personalizationDb);
        var dictEngine = new PersonalDictionaryEngine(dictRepo);
        var snippetRepo = new SqliteSnippetRepository(personalizationDb);
        var snippetEngine = new SnippetExpansionEngine(snippetRepo);
        var styleRepo = new SqliteStyleRepository(personalizationDb);
        var styleEngine = new StyleFormattingEngine(styleRepo);
        var settingsRepo = new SqliteSettingsRepository(personalizationDb);
        var initialSettings = Task.Run(async () => await settingsRepo.LoadSettingsAsync()).GetAwaiter().GetResult();

        // Load personalization caches
        Task.Run(async () =>
        {
            await dictEngine.ReloadAsync();
            await snippetEngine.ReloadAsync();
            await styleEngine.ReloadAsync();
        }).GetAwaiter().GetResult();

        // History & Productivity Services
        var historyRepo = new SqliteHistoryRepository(personalizationDb);
        var privacyService = new HistoryPrivacyService();
        var retentionService = new HistoryRetentionService(historyRepo);
        var statsService = new ProductivityStatisticsService(historyRepo);
        var exportService = new HistoryExportService(historyRepo);

        var historyService = new HistoryService(
            historyRepo,
            historyRepo,
            statsService,
            retentionService,
            exportService,
            privacyService
        );

        // Scratchpad & Quick Capture Services
        var scratchpadRepo = new SqliteScratchpadRepository(personalizationDb);
        var scratchpadExportService = new ScratchpadExportService();
        var scratchpadService = new ScratchpadService(scratchpadRepo, scratchpadRepo, scratchpadExportService);

        // Production Multi-Pass Formatting Pipeline: Whitespace normalization, entity protection,
        // spoken punctuation, snippets expansion, personal dictionary, conservative filler removal,
        // numbered lists, style formatting, smart capitalization, and Zero-Enter invariant.
        var formattingPipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);
        var insertionLogger = loggerFactory.CreateLogger<WindowsTextInsertionService>();
        var insertionService = new WindowsTextInsertionService(insertionLogger);

        var coordinatorLogger = loggerFactory.CreateLogger<VoiceSessionCoordinator>();
        var contextService = new WindowsUIAutomationContextService(loggerFactory.CreateLogger<WindowsUIAutomationContextService>());

        var biasingService = new Flow.Core.Personalization.PersonalizationBiasingService(dictEngine, dictRepo);

        // Enforce 20-minute desktop ceiling with 19-minute warning
        _coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asrRegistry,
            formattingPipeline,
            insertionService,
            coordinatorLogger,
            maxRecordingSeconds: 1200.0,
            warningThresholdSeconds: 1140.0,
            contextService: contextService,
            historyService: historyService,
            biasingService: biasingService
        );

        // Apply persisted settings dynamically to coordinator
        _coordinator.SetVadThreshold(initialSettings.VadThreshold);
        _coordinator.SelectedLanguage = initialSettings.Language;
        _coordinator.SetSessionLanguage(new Flow.Core.Language.LanguageCode(initialSettings.Language));

        // 2.5 Local HTTP API Bridge Server (100% Offline Local Whisper endpoint for web UI)
        _localApiServer = new LocalApiServer(
            whisperInference,
            formattingPipeline,
            dictRepo,
            dictEngine,
            historyRepo,
            modelManager,
            coordinator: _coordinator,
            logger: loggerFactory.CreateLogger<LocalApiServer>()
        );
        _localApiServer.Start();

        // 3. Windows UI & System Tray
        _hud = new FloatingHudController();
        _tray = new TrayIconManager(_hud.Handle);
        _hud.WindowMessageReceived += (msg, lParam) => _tray.ProcessMessage(msg, lParam);

        // Native Windows power management listener (sleep / resume transitions)
        _powerManager = new WindowsPowerStateManager(_hud.Handle, loggerFactory.CreateLogger<WindowsPowerStateManager>());
        _hud.PowerBroadcastReceived += powerEvent => _powerManager.ProcessPowerBroadcast(powerEvent);

        // 4. Live WASAPI Audio Capture with Device Management
        var deviceManager = new WasapiDeviceManager(loggerFactory.CreateLogger<WasapiDeviceManager>());
        var captureLogger = loggerFactory.CreateLogger<WasapiAudioCapture>();
        _capture = new WasapiAudioCapture(chunk =>
        {
            _coordinator.ProcessAudioChunk(chunk);
        }, captureLogger, targetDeviceId: initialSettings.AudioDeviceId, deviceManager: deviceManager);

        _powerManager.Suspending += () =>
        {
            logger.LogWarning("Windows power event: System is entering sleep/suspend. Safely stopping capture and disarming hotkeys.");
            _hotkeyHook?.Disarm();
            _capture.Stop();
            _ = _coordinator.CancelSessionAsync("System sleep");
        };

        _powerManager.Resuming += () =>
        {
            logger.LogInformation("Windows power event: System resumed from sleep. Scheduling audio endpoint re-enumeration.");
            _ = Task.Run(async () =>
            {
                try
                {
                    // Delay 500ms to allow Windows Audio service (Audiosrv) and USB/Bluetooth drivers to stabilize
                    await Task.Delay(500);
                    var devices = deviceManager.RefreshDevices();
                    string? defaultDevId = deviceManager.GetDefaultCaptureDeviceId();
                    logger.LogInformation("Audio endpoints re-enumerated post-wake ({Count} active device(s), default: {DefaultDevId}).",
                        devices.Count, defaultDevId ?? "None");

                    if (string.IsNullOrEmpty(initialSettings.AudioDeviceId))
                    {
                        _capture.SwitchToDevice(defaultDevId);
                    }
                    else
                    {
                        if (!deviceManager.IsDeviceActive(initialSettings.AudioDeviceId))
                        {
                            logger.LogWarning("Configured audio endpoint '{Id}' is inactive post-wake. Falling back to default: '{DefaultId}'.",
                                initialSettings.AudioDeviceId, defaultDevId ?? "None");
                            _capture.SwitchToDevice(defaultDevId);
                        }
                    }

                    _hotkeyHook?.Arm();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error re-initializing audio capture endpoints after system resume.");
                    _hotkeyHook?.Arm();
                }
            });
        };

        _capture.DeviceInvalidated += oldId =>
        {
            logger.LogWarning("Microphone endpoint invalidated or disconnected: '{DeviceId}'. Gracefully rolling over to fallback default.", oldId ?? "Unknown");
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_coordinator.CurrentState == SessionState.Recording)
                    {
                        await _coordinator.CancelSessionAsync("Microphone disconnected");
                    }

                    await Task.Delay(300); // Allow OS endpoint routing table to update
                    deviceManager.RefreshDevices();
                    string? fallbackId = deviceManager.GetDefaultCaptureDeviceId();
                    logger.LogInformation("Rolling over capture to fallback endpoint: '{FallbackId}'", fallbackId ?? "Default");
                    _capture.SwitchToDevice(fallbackId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during audio endpoint invalidation rollover.");
                }
            });
        };

        deviceManager.DefaultDeviceChanged += newDefaultId =>
        {
            logger.LogInformation("Windows default audio capture endpoint changed to: {DeviceId}", newDefaultId);
            if (string.IsNullOrEmpty(initialSettings.AudioDeviceId))
            {
                _capture.SwitchToDevice(newDefaultId);
            }
        };
        deviceManager.DeviceStateChanged += (devId, state) =>
        {
            logger.LogInformation("Windows audio capture endpoint state changed: {DeviceId}, state={State}", devId, state);
        };

        _capture.CaptureError += ex =>
        {
            logger.LogError(ex, "Physical microphone capture failed or disconnected.");
            _ = Task.Run(async () =>
            {
                await _coordinator.CancelSessionAsync($"Mic error: {ex.Message}");
            });
        };

        // 4.5 Background Auto-Updater Engine (Velopack)
        _updateService = new VelopackUpdateService(
            coordinator: _coordinator,
            capture: _capture,
            logger: loggerFactory.CreateLogger<VelopackUpdateService>()
        );

        // 5. Global Push-to-Talk, Double-Tap Hands-Free, and Backtrack Hook
        _hotkeyHook = new GlobalHotkeyHook(initialSettings.HotkeyVk, doubleTapThresholdMs: 350.0);

        void ShowHub(int targetTab = 0)
        {
            FlowHubWindowManager.ShowWindow(
                _coordinator,
                _capture,
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
                _hotkeyHook,
                targetTab: targetTab,
                modelManager: modelManager,
                updateService: _updateService);
        }

        // Wire Tray Icon Actions
        _tray.FlowHubRequested += () =>
        {
            logger.LogInformation("FLOW Hub window requested.");
            ShowHub(0);
        };

        _tray.ToggleDictationRequested += () =>
        {
            if (_coordinator.CurrentState == SessionState.Recording)
            {
                _capture.Stop();
                _ = Task.Run(async () => await _coordinator.EndSessionAsync());
            }
            else
            {
                _hud.RepositionForActiveMonitor();
                _ = Task.Run(async () =>
                {
                    if (await _coordinator.StartSessionAsync(isHandsFree: true) && _coordinator.CurrentState == SessionState.Recording)
                    {
                        _capture.Start();
                    }
                });
            }
        };

        // Floating HUD Click Interaction (Click to Start/Stop Hands-Free Dictation without focus loss)
        _hud.Clicked += () =>
        {
            if (_coordinator.CurrentState == SessionState.Recording)
            {
                logger.LogInformation("Floating HUD clicked: Concluding active dictation session.");
                _capture.Stop();
                _ = Task.Run(async () => await _coordinator.EndSessionAsync());
            }
            else if (_coordinator.CurrentState == SessionState.Idle)
            {
                logger.LogInformation("Floating HUD clicked: Starting hands-free dictation session.");
                _hud.RepositionForActiveMonitor();
                _ = Task.Run(async () =>
                {
                    if (await _coordinator.StartSessionAsync(isHandsFree: true) && _coordinator.CurrentState == SessionState.Recording)
                    {
                        _capture.Start();
                    }
                });
            }
        };

        _tray.ScratchpadRequested += () =>
        {
            logger.LogInformation("Scratchpad workspace requested.");
            ShowHub(5);
        };

        _tray.HistoryRequested += () =>
        {
            logger.LogInformation("History requested.");
            ShowHub(1);
        };

        _tray.SettingsRequested += () =>
        {
            logger.LogInformation("Settings requested.");
            ShowHub(6);
        };

        _tray.AboutRequested += () =>
        {
            logger.LogInformation("About requested.");
            ShowHub(7);
        };

        _tray.ExitRequested += () =>
        {
            logger.LogInformation("Exit requested from system tray.");
            PostQuitMessage(0);
        };

        FlowHubWindowManager.ExitApplicationRequested += () =>
        {
            logger.LogInformation("Exit requested from FLOW Hub.");
            PostQuitMessage(0);
        };

        _tray.Install("FLOW — Local Voice Dictation (Hold Alt+Space or Right Alt to speak)");

        _hotkeyHook.HotkeyDown += (isHandsFree) =>
        {
            _hud.RepositionForActiveMonitor();
            _ = Task.Run(async () =>
            {
                if (await _coordinator.StartSessionAsync(isHandsFree) && _coordinator.CurrentState == SessionState.Recording)
                {
                    _capture.Start();
                }
            });
        };

        _hotkeyHook.HotkeyUp += () =>
        {
            _ = Task.Run(async () =>
            {
                _capture.Stop();
                await _coordinator.EndSessionAsync();
            });
        };

        _hotkeyHook.HotkeyCancelled += () =>
        {
            _ = Task.Run(async () =>
            {
                _capture.Stop();
                await _coordinator.CancelSessionAsync("Cancelled by user (Esc)");
            });
        };

        _hotkeyHook.CommandModeHotkeyDown += () =>
        {
            _hud.RepositionForActiveMonitor();
            _ = Task.Run(async () =>
            {
                logger.LogInformation("Command Mode hotkey triggered.");
                if (await _coordinator.StartCommandSessionAsync() && _coordinator.CurrentState == SessionState.Recording)
                {
                    _capture.Start();
                }
            });
        };

        _hotkeyHook.CommandModeHotkeyUp += () =>
        {
            _ = Task.Run(async () =>
            {
                _capture.Stop();
                await _coordinator.EndSessionAsync();
            });
        };

        _hotkeyHook.BacktrackRequested += () =>
        {
            _ = Task.Run(async () =>
            {
                logger.LogInformation("Backtrack hotkey triggered. Reverting last insertion...");
                bool success = await _coordinator.BacktrackAsync();
                if (success)
                {
                    logger.LogInformation("Backtrack successfully reverted previous insertion.");
                }
                else
                {
                    logger.LogWarning("Backtrack safe no-op: Focus changed or no insertion history.");
                }
            });
        };

        _coordinator.StateChanged += (state, detail) =>
        {
            if (state != SessionState.Recording && _capture.IsCapturing)
            {
                _capture.Stop();
            }
            _hud.UpdateState(state, detail, _coordinator.CurrentMode == SessionMode.Command);
            _tray.UpdateTooltip($"FLOW — {detail ?? state.ToString()}");
        };

        _coordinator.SessionWarning += warning =>
        {
            logger.LogWarning("Session Warning: {Warning}", warning);
            _hud.UpdateState(_coordinator.CurrentState, warning, _coordinator.CurrentMode == SessionMode.Command);
            _tray.UpdateTooltip($"FLOW: {warning}");
        };

        _coordinator.AudioLevelChanged += rms =>
        {
            _hud.UpdateAudioLevel(rms);
        };

        _coordinator.CommandProcessed += (intent, safety) =>
        {
            if (intent is TransformCommandIntent t)
            {
                logger.LogInformation("Command Mode: Applied Transform '{Transform}'. Safety Policy: {Verdict}", t.Transform, safety.Verdict);
            }
            else if (intent is EditorCommandIntent e)
            {
                logger.LogInformation("Command Mode: Executed Editor Action '{Action}'. Safety Policy: {Verdict}", e.ActionName, safety.Verdict);
            }
        };

        _coordinator.PartialTranscriptReceived += partial =>
        {
            _hud.UpdatePartialTranscript(partial);
        };

        _coordinator.FinalTextInserted += text =>
        {
            logger.LogInformation("Text inserted successfully into target cursor position. Length: {Length}. [Zero-Enter: VK_RETURN=0, CR=0, LF=0]", text.Length);
        };

        // Install keyboard hook in disarmed state
        _hotkeyHook.Start(autoArm: false);

        // Asynchronously pre-warm local model weights in background
        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Pre-warming local Whisper model weights in background...");
                bool initialized = await localWhisper.InitializeAsync();
                if (initialized)
                {
                    logger.LogInformation("Local Whisper model weights verified and ready.");
                }
                else
                {
                    logger.LogWarning("Whisper model weights not yet pre-loaded. Will acquire on first dictation trigger.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background model pre-warm encountered an error.");
            }
        });

        // 6. Launch FLOW Hub window unless user requested minimized startup
        if (!startMinimized)
        {
            ShowHub(0);
        }

        // 7. Verify Inviolable Startup Invariant: State MUST be Idle, Hands-Free OFF, Recording FALSE
        if (_coordinator.CurrentState != SessionState.Idle)
        {
            logger.LogError("FATAL INVARIANT VIOLATION: Coordinator started in state {State}. Forcing Idle reset.", _coordinator.CurrentState);
            _coordinator.CancelSessionAsync("Startup state correction").GetAwaiter().GetResult();
        }

        if (_capture.IsCapturing)
        {
            logger.LogError("FATAL INVARIANT VIOLATION: Microphone capture was active at startup. Forcing Stop.");
            _capture.Stop();
        }

        // Arm keyboard hook only after UI is presented and all subsystems are verified idle
        _hotkeyHook.Arm();

        // 8. Arm background update polling timer (checks every 4 hours)
        _updateService?.StartBackgroundCheckTimer(TimeSpan.FromHours(4));

        logger.LogInformation("FLOW Voice Core initialized and listening. Hold-to-talk: [Alt + Space]. Hands-free: [Alt + B]. Click HUD to dictate. Cancel: [Esc].");

        // Native Windows message loop
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == SingleInstanceCoordinator.ActivationMessage)
            {
                logger.LogInformation("Received single-instance activation message from secondary launch.");
                FlowHubWindowManager.BringToForeground();
            }
            else if (msg.message == WindowsPowerStateManager.WM_POWERBROADCAST)
            {
                _powerManager?.ProcessPowerBroadcast(msg.wParam.ToInt32());
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup
        _localApiServer?.Dispose();
        _updateService?.Dispose();
        FlowHubWindowManager.CloseWindow();
        _hotkeyHook.Dispose();
        _powerManager?.Dispose();
        _capture.Dispose();
        deviceManager.Dispose();
        _hud.Dispose();
        _tray.Dispose();
        personalizationDb.Dispose();

        return 0;
    }

    #region Win32 Interop

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private static bool EnsureConsoleOutput()
    {
        try
        {
            IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (stdOutHandle == IntPtr.Zero || stdOutHandle == (IntPtr)(-1))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            }

            if (stdOutHandle != IntPtr.Zero && stdOutHandle != (IntPtr)(-1))
            {
                var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                var stream = new System.IO.FileStream(safeHandle, System.IO.FileAccess.Write);
                var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
                return true;
            }
        }
        catch { }
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    #endregion
}
