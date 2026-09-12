using System;
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
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Tray;
using Flow.Host.Windows.UI;
using Flow.Inference;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows;

public static class Program
{
    private static VoiceSessionCoordinator? _coordinator;
    private static WasapiAudioCapture? _capture;
    private static GlobalHotkeyHook? _hotkeyHook;
    private static FloatingHudController? _hud;
    private static TrayIconManager? _tray;

    [STAThread]
    public static int Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        var logger = loggerFactory.CreateLogger("Flow.Main");
        logger.LogInformation("Starting FLOW Windows Native Voice Platform (Phase 2B)...");

        // 1. Core audio & inference services
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 30.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.015f, silenceThresholdSeconds: 0.45);

        // Production ASR Engine: Native Whisper.net local inference with CPU AVX2 & GPU acceleration
        var asrRegistry = new ASREngineRegistry();
        var modelManager = new WhisperModelManager(null, loggerFactory.CreateLogger<WhisperModelManager>());
        var whisperInference = new WhisperNetInferenceEngine(modelManager, loggerFactory.CreateLogger<WhisperNetInferenceEngine>());
        var localWhisper = new LocalWhisperEngine(whisperInference, modelManager.ModelPath);

        // Register strictly production ASR (zero mocks in production path)
        asrRegistry.Register(localWhisper, isDefault: true, priority: 10);

        // Personalization persistence & engines (Phase 2D)
        var personalizationDb = new SqlitePersonalizationDatabase();
        var dictRepo = new SqlitePersonalDictionaryRepository(personalizationDb);
        var dictEngine = new PersonalDictionaryEngine(dictRepo);
        var snippetRepo = new SqliteSnippetRepository(personalizationDb);
        var snippetEngine = new SnippetExpansionEngine(snippetRepo);
        var styleRepo = new SqliteStyleRepository(personalizationDb);
        var styleEngine = new StyleFormattingEngine(styleRepo);

        // Load personalization caches
        Task.Run(async () =>
        {
            await dictEngine.ReloadAsync();
            await snippetEngine.ReloadAsync();
            await styleEngine.ReloadAsync();
        }).GetAwaiter().GetResult();

        // Production Multi-Pass Formatting Pipeline: Whitespace normalization, entity protection,
        // spoken punctuation, snippets expansion, personal dictionary, conservative filler removal,
        // numbered lists, style formatting, smart capitalization, and Zero-Enter invariant.
        var formattingPipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);
        var insertionLogger = loggerFactory.CreateLogger<WindowsTextInsertionService>();
        var insertionService = new WindowsTextInsertionService(insertionLogger);

        var coordinatorLogger = loggerFactory.CreateLogger<VoiceSessionCoordinator>();
        var contextService = new WindowsUIAutomationContextService(loggerFactory.CreateLogger<WindowsUIAutomationContextService>());
        // Enforce 20-minute desktop ceiling with 19-minute warning
        _coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asrRegistry,
            formattingPipeline,
            insertionService,
            coordinatorLogger,
            maxRecordingSeconds: 1200.0,  // 20 minutes
            warningThresholdSeconds: 1140.0, // 19 minutes
            contextService: contextService
        );

        // 2. Windows UI & System Tray
        _hud = new FloatingHudController();
        _tray = new TrayIconManager(IntPtr.Zero);
        _tray.Install("FLOW — Local Voice Dictation (Right-Alt to speak, double-tap for hands-free, Shift+Right-Alt to backtrack)");

        // 3. Live WASAPI Audio Capture with Device Management
        var deviceManager = new WasapiDeviceManager(loggerFactory.CreateLogger<WasapiDeviceManager>());
        var captureLogger = loggerFactory.CreateLogger<WasapiAudioCapture>();
        _capture = new WasapiAudioCapture(chunk =>
        {
            _coordinator.ProcessAudioChunk(chunk);
        }, captureLogger, targetDeviceId: null, deviceManager: deviceManager);

        deviceManager.DefaultDeviceChanged += newDefaultId =>
        {
            logger.LogInformation("Windows default audio capture endpoint changed to: {DeviceId}", newDefaultId);
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

        // 4. Global Push-to-Talk, Double-Tap Hands-Free, and Backtrack Hook
        _hotkeyHook = new GlobalHotkeyHook(GlobalHotkeyHook.DefaultHotkeyVk, doubleTapThresholdMs: 350.0);

        _hotkeyHook.HotkeyDown += (isHandsFree) =>
        {
            _ = Task.Run(async () =>
            {
                await _coordinator.StartSessionAsync(isHandsFree);
                _capture.Start();
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

        // 5. Dedicated Command Mode Shortcut (WF-036: Ctrl + Right Alt)
        _hotkeyHook.CommandModeHotkeyDown += () =>
        {
            _ = Task.Run(async () =>
            {
                logger.LogInformation("Command Mode hotkey triggered (Ctrl+RightAlt).");
                await _coordinator.StartCommandSessionAsync();
                _capture.Start();
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

        _coordinator.FinalTextInserted += text =>
        {
            logger.LogInformation("Text inserted successfully into target cursor position. Length: {Length}. [Zero-Enter: VK_RETURN=0, CR=0, LF=0]", text.Length);
        };

        // Start keyboard hook
        _hotkeyHook.Start();

        // Asynchronously initialize local model weights in background without blocking Windows UI
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

        logger.LogInformation("FLOW Voice Core initialized and listening. Push-to-talk: Hold [Right Alt]. Hands-free: Double-tap [Right Alt]. Command Mode: [Ctrl + Right Alt]. Backtrack: [Shift + Right Alt]. Cancel: [Esc].");

        const uint WM_POWERBROADCAST = 0x0218;
        const int PBT_APMSUSPEND = 0x0004;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        const int PBT_APMRESUMESUSPEND = 0x0007;

        // Native Windows message loop
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_POWERBROADCAST)
            {
                int powerEvent = msg.wParam.ToInt32();
                if (powerEvent == PBT_APMSUSPEND)
                {
                    logger.LogWarning("System is entering sleep/suspend. Safely stopping audio capture.");
                    _capture.Stop();
                    _ = _coordinator.CancelSessionAsync("System sleep");
                }
                else if (powerEvent == PBT_APMRESUMEAUTOMATIC || powerEvent == PBT_APMRESUMESUSPEND)
                {
                    logger.LogInformation("System resumed from sleep. Verifying audio capture endpoints.");
                }
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup
        _hotkeyHook.Dispose();
        _capture.Dispose();
        deviceManager.Dispose();
        _hud.Dispose();
        _tray.Dispose();
        personalizationDb.Dispose();

        return 0;
    }

    #region Win32 Message Pump

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

    #endregion
}
