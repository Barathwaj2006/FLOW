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
        });
        var logger = loggerFactory.CreateLogger("Flow.Main");
        logger.LogInformation("Starting FLOW Windows Native Voice Platform...");

        // 1. Core audio & inference services
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 30.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.015f, silenceThresholdSeconds: 0.45);

        var asrRegistry = new ASREngineRegistry();
        var localWhisper = new LocalWhisperEngine();
        var mockEngine = new MockASREngine("mock-dev", "Local Fast Fallback", "whisper-test", true);
        asrRegistry.Register(localWhisper, isDefault: true, priority: 10);
        asrRegistry.Register(mockEngine, isDefault: false, priority: 5);

        var sanitizer = new DeterministicTextSanitizer();
        var insertionLogger = loggerFactory.CreateLogger<WindowsTextInsertionService>();
        var insertionService = new WindowsTextInsertionService(insertionLogger);

        var coordinatorLogger = loggerFactory.CreateLogger<VoiceSessionCoordinator>();
        _coordinator = new VoiceSessionCoordinator(ringBuffer, vad, asrRegistry, sanitizer, insertionService, coordinatorLogger);

        // 2. Windows UI & Tray
        _hud = new FloatingHudController();
        _tray = new TrayIconManager(IntPtr.Zero);
        _tray.Install("FLOW — Local AI Voice Dictation (Hold Right-Alt to speak)");

        // 3. Audio capture
        _capture = new WasapiAudioCapture(chunk =>
        {
            _coordinator.ProcessAudioChunk(chunk);
        });

        // 4. Global Push-to-Talk Hotkey (Default: Right Alt VK_RMENU)
        _hotkeyHook = new GlobalHotkeyHook(GlobalHotkeyHook.DefaultHotkeyVk);

        _hotkeyHook.HotkeyDown += () =>
        {
            _ = Task.Run(async () =>
            {
                await _coordinator.StartSessionAsync();
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

        _coordinator.AudioLevelChanged += rms =>
        {
            _hud.UpdateAudioLevel(rms);
        };

        _coordinator.FinalTextInserted += text =>
        {
            logger.LogInformation("Text inserted successfully (Length: {Length})", text.Length);
        };

        // Start hook
        _hotkeyHook.Start();

        logger.LogInformation("FLOW Voice Core initialized and listening. Push-to-talk: Hold [Right Alt].");

        // Windows standard message pump
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
