using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.Session;
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

        var sanitizer = new DeterministicTextSanitizer();
        var insertionLogger = loggerFactory.CreateLogger<WindowsTextInsertionService>();
        var insertionService = new WindowsTextInsertionService(insertionLogger);

        var coordinatorLogger = loggerFactory.CreateLogger<VoiceSessionCoordinator>();
        // Enforce 20-minute desktop ceiling with 19-minute warning
        _coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asrRegistry,
            sanitizer,
            insertionService,
            coordinatorLogger,
            maxRecordingSeconds: 1200.0,  // 20 minutes
            warningThresholdSeconds: 1140.0 // 19 minutes
        );

        // 2. Windows UI & System Tray
        _hud = new FloatingHudController();
        _tray = new TrayIconManager(IntPtr.Zero);
        _tray.Install("FLOW — Local Voice Dictation (Hold Right-Alt to speak, double-tap for hands-free)");

        // 3. Live WASAPI Audio Capture
        _capture = new WasapiAudioCapture(chunk =>
        {
            _coordinator.ProcessAudioChunk(chunk);
        }, loggerFactory.CreateLogger<WasapiAudioCapture>());

        // 4. Global Push-to-Talk and Double-Tap Hands-Free Hook
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

        _coordinator.StateChanged += (state, detail) =>
        {
            _hud.UpdateState(state, detail);
            _tray.UpdateTooltip($"FLOW — {detail ?? state.ToString()}");
        };

        _coordinator.SessionWarning += warning =>
        {
            logger.LogWarning("Session Warning: {Warning}", warning);
            _hud.UpdateState(_coordinator.CurrentState, warning);
            _tray.UpdateTooltip($"FLOW: {warning}");
        };

        _coordinator.AudioLevelChanged += rms =>
        {
            _hud.UpdateAudioLevel(rms);
        };

        _coordinator.FinalTextInserted += text =>
        {
            logger.LogInformation("Text inserted successfully into target cursor position. Length: {Length}", text.Length);
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

        logger.LogInformation("FLOW Voice Core initialized and listening. Push-to-talk: Hold [Right Alt]. Hands-free: Double-tap [Right Alt].");

        // Native Windows message loop
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup
        _hotkeyHook.Dispose();
        _capture.Dispose();
        _hud.Dispose();
        _tray.Dispose();

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
