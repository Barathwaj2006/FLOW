using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Core.Audio;
using Flow.Core.History;
using Flow.Core.Language;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Session;
using Flow.Core.Storage;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Updates;
using Flow.Inference;
using Microsoft.Web.WebView2.Core;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Production-grade host window embedding the precompiled React / Tailwind / Obsidian Amber UI
/// inside Microsoft.Web.WebView2 with virtual host folder mapping (https://app.flow.local/).
/// Operates 100% offline with zero local port allocation and bidirectional IPC.
/// </summary>
public partial class FlowShellWindow : Window, IFlowMainWindow
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly string[] s_tabNames =
    [
        "home",
        "history",
        "dictionary",
        "snippets",
        "styles",
        "scratchpad",
        "settings",
        "about"
    ];

    private readonly VoiceSessionCoordinator? _coordinator;
    private readonly WasapiAudioCapture? _capture;
    private readonly WasapiDeviceManager? _deviceManager;
    private readonly IHistoryService? _historyService;
    private readonly IScratchpadService? _scratchpadService;
    private readonly IPersonalDictionaryRepository? _dictRepo;
    private readonly PersonalDictionaryEngine? _dictEngine;
    private readonly ISnippetRepository? _snippetRepo;
    private readonly SnippetExpansionEngine? _snippetEngine;
    private readonly IStyleRepository? _styleRepo;
    private readonly StyleFormattingEngine? _styleEngine;
    private readonly ISettingsRepository? _settingsRepo;
    private readonly GlobalHotkeyHook? _hotkeyHook;
    private readonly WhisperModelManager? _modelManager;
    private readonly IFlowUpdateService? _updateService;

    private bool _isInitialized;
    private int _pendingTab = -1;

    public bool AllowRealClose { get; set; }
    public event Action? ExitApplicationRequested;
    public event Action<string, string, object?, bool>? MessageSent;
    public event Action<string, object?>? EventBroadcasted;

    public FlowShellWindow()
    {
        InitializeComponent();
        Loaded += async (s, e) => await InitializeWebViewAsync();
    }

    public FlowShellWindow(
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
        ISettingsRepository? settingsRepo = null,
        GlobalHotkeyHook? hotkeyHook = null,
        WhisperModelManager? modelManager = null,
        IFlowUpdateService? updateService = null) : this()
    {
        _coordinator = coordinator;
        _capture = capture;
        _deviceManager = deviceManager;
        _historyService = historyService;
        _scratchpadService = scratchpadService;
        _dictRepo = dictRepo;
        _dictEngine = dictEngine;
        _snippetRepo = snippetRepo;
        _snippetEngine = snippetEngine;
        _styleRepo = styleRepo;
        _styleEngine = styleEngine;
        _settingsRepo = settingsRepo;
        _hotkeyHook = hotkeyHook;
        _modelManager = modelManager;
        _updateService = updateService;

        WireCoordinatorEvents();
    }

    private void WireCoordinatorEvents()
    {
        if (_coordinator != null)
        {
            _coordinator.StateChanged += (state, detail) =>
            {
                BroadcastEvent("session-state", new { state = state.ToString(), detail });
            };

            _coordinator.AudioLevelChanged += (level) =>
            {
                BroadcastEvent("audio-level", new { level });
            };

            _coordinator.PartialTranscriptReceived += (text) =>
            {
                BroadcastEvent("partial-transcript", new { text });
            };

            _coordinator.FinalTextInserted += (text) =>
            {
                BroadcastEvent("final-transcript", new { text });
            };

            _coordinator.TranscriptCompleted += (text) =>
            {
                BroadcastEvent("session-completed", new { text });
            };
        }

        if (_modelManager != null)
        {
            _modelManager.ProgressUpdated += (prog) =>
            {
                BroadcastEvent("download-progress", prog);
            };
        }

        if (_updateService != null)
        {
            _updateService.StatusChanged += (info) =>
            {
                BroadcastEvent("update-status-changed", new
                {
                    status = info.Status.ToString(),
                    currentVersion = info.CurrentVersion,
                    availableVersion = info.AvailableVersion,
                    downloadProgressPercent = info.DownloadProgressPercent,
                    errorMessage = info.ErrorMessage,
                    lastCheckedUtc = info.LastCheckedUtc?.ToString("O")
                });
            };
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_isInitialized) return;

        try
        {
            string distPath = ResolveDistDirectory();
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FLOW",
                "WebView2Data"
            );
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await ShellWebView.EnsureCoreWebView2Async(env);

            ShellWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.flow.local",
                distPath,
                CoreWebView2HostResourceAccessKind.Allow
            );

            ShellWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            ShellWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            ShellWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            ShellWebView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                if (_pendingTab >= 0)
                {
                    SelectTab(_pendingTab);
                    _pendingTab = -1;
                }
            };

            _isInitialized = true;
            ShellWebView.CoreWebView2.Navigate("https://app.flow.local/index.html");
        }
        catch (Exception ex)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "webview2_init.log"), $"[{DateTime.UtcNow:O}] WebView2 Init Error:\n{ex}\n\n");
            }
            catch { }
        }
    }

    private static string ResolveDistDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate = Path.Combine(baseDir, "dist");
        if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
        {
            return candidate;
        }

        // Development fallback: Walk up to find FLOW\dist
        var cur = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && cur != null; i++)
        {
            string testDist = Path.Combine(cur.FullName, "dist");
            if (Directory.Exists(testDist) && File.Exists(Path.Combine(testDist, "index.html")))
            {
                return testDist;
            }
            cur = cur.Parent;
        }

        return candidate;
    }

    public void SelectTab(int targetTab)
    {
        if (targetTab >= 0 && targetTab < s_tabNames.Length)
        {
            SelectTab(s_tabNames[targetTab]);
        }
    }

    public void SelectTab(string tabName)
    {
        int index = Array.IndexOf(s_tabNames, tabName);
        if (index >= 0) _pendingTab = index;

        BroadcastEvent("navigate-tab", tabName);
    }

    public void BroadcastEvent(string eventName, object? payload)
    {
        EventBroadcasted?.Invoke(eventName, payload);
        Dispatcher.BeginInvoke(() =>
        {
            if (ShellWebView?.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new { @event = eventName, payload }, s_jsonOptions);
                ShellWebView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        });
    }

    private void SendResponse(string requestId, string action, object? payload)
    {
        MessageSent?.Invoke(requestId, action, payload, true);
        Dispatcher.BeginInvoke(() =>
        {
            if (ShellWebView?.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new
                {
                    requestId,
                    action,
                    payload,
                    success = true
                }, s_jsonOptions);
                ShellWebView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        });
    }

    private void SendError(string requestId, string action, string error)
    {
        MessageSent?.Invoke(requestId, action, error, false);
        Dispatcher.BeginInvoke(() =>
        {
            if (ShellWebView?.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new
                {
                    requestId,
                    action,
                    error,
                    success = false
                }, s_jsonOptions);
                ShellWebView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        });
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string rawJson = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(rawJson)) return;

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string requestId = root.TryGetProperty("requestId", out var r) ? r.GetString() ?? "" : "";
            string action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
            JsonElement payload = root.TryGetProperty("payload", out var p) ? p : default;

            await HandleActionAsync(requestId, action, payload);
        }
        catch (Exception ex)
        {
            try
            {
                SendError("", "", ex.Message);
            }
            catch { }
        }
    }

    public async Task HandleActionAsync(string requestId, string action, JsonElement payload)
    {
        switch (action.ToLowerInvariant())
        {
            case "get-status":
            {
                bool installed = _modelManager?.IsModelInstalledAndValid() ?? true;
                string modelName = _modelManager?.ActiveProfile.Name ?? "ggml-tiny.en.bin";

                SendResponse(requestId, action, new
                {
                    connected = true,
                    modelInstalled = installed,
                    modelName,
                    engine = "Whisper.net Native Local Engine (DirectML IPC)",
                    port = 0,
                    offlineSovereignty = "100% Offline (Zero Cloud Audio / Direct Host IPC)"
                });
                break;
            }

            case "get-hardware":
            {
                var profile = HardwareAccelerationDetector.Detect();
                SendResponse(requestId, action, new
                {
                    primaryGpuName = profile.PrimaryGpuName,
                    dedicatedVramMB = profile.DedicatedVramMB,
                    isDiscreteGpu = profile.IsDiscreteGpu,
                    directMLSupported = profile.DirectMLSupported,
                    recommendedBackend = profile.RecommendedBackend.ToString(),
                    optimalCpuThreads = profile.OptimalCpuThreads,
                    cpuArchitecture = profile.CpuArchitecture,
                    allDetectedGpus = profile.AllDetectedGpus
                });
                break;
            }

            case "get-models":
            {
                if (_modelManager != null)
                {
                    var statuses = _modelManager.GetAllModelStatuses();
                    SendResponse(requestId, action, statuses);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "download-model":
            {
                if (_modelManager == null)
                {
                    SendError(requestId, action, "Model manager not available");
                    return;
                }

                string modelName = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("modelName", out var mn)
                    ? mn.GetString() ?? ""
                    : "";

                var profile = WhisperModelProfile.FindProfile(modelName) ?? _modelManager.ActiveProfile;
                if (profile == null)
                {
                    SendError(requestId, action, $"Unknown model profile: {modelName}");
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _modelManager.EnsureModelAvailableAsync(null, CancellationToken.None, profile);
                    }
                    catch { }
                });

                SendResponse(requestId, action, new { success = true, modelName = profile.Name });
                break;
            }

            case "get-download-progress":
            {
                var prog = _modelManager?.CurrentProgress ?? new ModelDownloadProgress(
                    ModelName: _modelManager?.ActiveProfile.Name ?? "None",
                    BytesDownloaded: 0,
                    TotalBytes: 0,
                    Percent: 0,
                    SpeedMBps: 0,
                    Status: "Idle",
                    IsActive: false
                );
                SendResponse(requestId, action, prog);
                break;
            }

            case "cancel-download":
            {
                _modelManager?.CancelActiveDownload();
                SendResponse(requestId, action, new { success = true });
                break;
            }

            case "select-model":
            {
                if (_modelManager == null)
                {
                    SendError(requestId, action, "Model manager not available");
                    return;
                }

                string modelName = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("modelName", out var mn)
                    ? mn.GetString() ?? ""
                    : "";

                bool ok = _modelManager.SelectModel(modelName);
                SendResponse(requestId, action, new { success = ok });
                break;
            }

            case "get-history":
            {
                if (_historyService != null)
                {
                    var filter = new HistoryFilter(IncludeDeleted: false);
                    var page = await _historyService.Repository.GetPagedAsync(filter, pageIndex: 0, pageSize: 100);
                    var displayItems = page.Items.Select(e => new
                    {
                        id = e.Id,
                        sessionId = e.SessionId.ToString(),
                        createdAt = e.CreatedAt.ToString("h:mm tt"),
                        timeShort = e.CreatedAt.ToString("h:mm tt"),
                        durationMs = e.DurationMs,
                        durationText = $"{e.DurationMs / 1000.0:F1}s",
                        characterCount = e.CharacterCount,
                        wordCount = e.WordCount,
                        language = e.Language,
                        application = string.IsNullOrWhiteSpace(e.Application) ? "Desktop" : e.Application,
                        applicationCategory = e.ApplicationCategory,
                        target = "Focused Window",
                        mode = e.Mode,
                        state = e.State.ToString(),
                        isFavorite = e.IsFavorite,
                        text = e.Text ?? string.Empty,
                        latency = "Local Whisper • DirectML IPC",
                        engine = "FLOW Local Whisper Engine"
                    });
                    SendResponse(requestId, action, displayItems);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "get-dictionary":
            {
                if (_dictRepo != null)
                {
                    var entries = await _dictRepo.GetAllAsync();
                    SendResponse(requestId, action, entries);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "add-dictionary":
            {
                if (_dictRepo == null)
                {
                    SendError(requestId, action, "Dictionary repository not available");
                    return;
                }

                string term = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("term", out var t) ? t.GetString() ?? "" : "";
                string replacement = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("replacement", out var rep) ? rep.GetString() ?? "" : "";
                bool isStarred = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("isStarred", out var st) && st.GetBoolean();
                string category = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";

                var entry = new Flow.Core.Personalization.Dictionary.DictionaryEntry
                {
                    Term = term,
                    Replacement = replacement,
                    IsStarred = isStarred,
                    Category = category
                };
                await _dictRepo.AddAsync(entry);
                if (_dictEngine != null) await _dictEngine.ReloadAsync();

                SendResponse(requestId, action, new { success = true, entry });
                break;
            }

            case "delete-dictionary":
            {
                if (_dictRepo == null)
                {
                    SendError(requestId, action, "Dictionary repository not available");
                    return;
                }

                string id = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                await _dictRepo.DeleteAsync(id);
                if (_dictEngine != null) await _dictEngine.ReloadAsync();

                SendResponse(requestId, action, new { success = true });
                break;
            }

            case "get-snippets":
            {
                if (_snippetRepo != null)
                {
                    var snippets = await _snippetRepo.GetAllAsync();
                    SendResponse(requestId, action, snippets);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "get-styles":
            {
                if (_styleRepo != null)
                {
                    var styles = await _styleRepo.GetAllProfilesAsync();
                    SendResponse(requestId, action, styles);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "get-notes":
            {
                if (_scratchpadService != null)
                {
                    var notes = await _scratchpadService.GetRecentScratchpadsAsync(50);
                    SendResponse(requestId, action, notes);
                }
                else
                {
                    SendResponse(requestId, action, Array.Empty<object>());
                }
                break;
            }

            case "start-dictation":
            {
                if (_coordinator != null)
                {
                    bool ok = await _coordinator.StartSessionAsync(isHandsFree: true);
                    if (ok && _capture != null)
                    {
                        _capture.Start();
                    }
                    SendResponse(requestId, action, new { success = ok });
                }
                else
                {
                    SendError(requestId, action, "Coordinator not available");
                }
                break;
            }

            case "stop-dictation":
            {
                if (_coordinator != null)
                {
                    _capture?.Stop();
                    await _coordinator.EndSessionAsync();
                    SendResponse(requestId, action, new { success = true });
                }
                else
                {
                    SendError(requestId, action, "Coordinator not available");
                }
                break;
            }

            case "backtrack":
            {
                if (_coordinator != null)
                {
                    bool ok = await _coordinator.BacktrackAsync();
                    SendResponse(requestId, action, new { success = ok });
                }
                else
                {
                    SendError(requestId, action, "Coordinator not available");
                }
                break;
            }

            case "get-settings":
            {
                if (_settingsRepo != null)
                {
                    var settings = await _settingsRepo.LoadSettingsAsync();
                    SendResponse(requestId, action, settings);
                }
                else
                {
                    SendResponse(requestId, action, new FlowAppSettings());
                }
                break;
            }

            case "save-settings":
            {
                if (_settingsRepo != null && payload.ValueKind == JsonValueKind.Object)
                {
                    var settings = JsonSerializer.Deserialize<FlowAppSettings>(payload.GetRawText(), s_jsonOptions);
                    if (settings != null)
                    {
                        await _settingsRepo.SaveSettingsAsync(settings);
                        SendResponse(requestId, action, new { success = true });
                    }
                    else
                    {
                        SendError(requestId, action, "Failed to deserialize settings");
                    }
                }
                else
                {
                    SendError(requestId, action, "Settings repo not available or payload invalid");
                }
                break;
            }

            case "get-devices":
            {
                if (_deviceManager != null)
                {
                    var devices = _deviceManager.EnumerateCaptureDevices().Select(d => d.Name);
                    string? defaultDevId = _deviceManager.GetDefaultCaptureDeviceId();
                    SendResponse(requestId, action, new { devices, defaultDevice = defaultDevId ?? "" });
                }
                else
                {
                    SendResponse(requestId, action, new { devices = Array.Empty<string>(), defaultDevice = "" });
                }
                break;
            }

            case "exit-app":
            {
                ExitApplicationRequested?.Invoke();
                SendResponse(requestId, action, new { success = true });
                break;
            }

            case "get-update-status":
            {
                var status = _updateService?.CurrentStatus;
                SendResponse(requestId, action, new
                {
                    status = (status?.Status ?? Updates.UpdateStatus.Idle).ToString(),
                    currentVersion = status?.CurrentVersion ?? (_updateService?.CurrentVersion ?? "1.0.0"),
                    availableVersion = status?.AvailableVersion,
                    downloadProgressPercent = status?.DownloadProgressPercent ?? 0,
                    errorMessage = status?.ErrorMessage,
                    lastCheckedUtc = status?.LastCheckedUtc?.ToString("O"),
                    isInstalled = _updateService?.IsInstalled ?? false
                });
                break;
            }

            case "check-update":
            {
                if (_updateService == null)
                {
                    SendError(requestId, action, "Update service not available");
                    return;
                }

                try
                {
                    var info = await _updateService.CheckForUpdatesAsync();
                    SendResponse(requestId, action, new
                    {
                        status = info.Status.ToString(),
                        currentVersion = info.CurrentVersion,
                        availableVersion = info.AvailableVersion,
                        downloadProgressPercent = info.DownloadProgressPercent,
                        errorMessage = info.ErrorMessage,
                        lastCheckedUtc = info.LastCheckedUtc?.ToString("O"),
                        isInstalled = _updateService.IsInstalled
                    });
                }
                catch (Exception ex)
                {
                    SendError(requestId, action, ex.Message);
                }
                break;
            }

            case "download-update":
            {
                if (_updateService == null)
                {
                    SendError(requestId, action, "Update service not available");
                    return;
                }

                try
                {
                    bool ok = await _updateService.DownloadUpdatesAsync();
                    SendResponse(requestId, action, new { success = ok });
                }
                catch (Exception ex)
                {
                    SendError(requestId, action, ex.Message);
                }
                break;
            }

            case "apply-update":
            {
                if (_updateService == null)
                {
                    SendError(requestId, action, "Update service not available");
                    return;
                }

                try
                {
                    bool ok = await _updateService.ApplyUpdatesAndRestartAsync();
                    SendResponse(requestId, action, new { success = ok });
                }
                catch (Exception ex)
                {
                    SendError(requestId, action, ex.Message);
                }
                break;
            }

            default:
            {
                SendError(requestId, action, $"Unknown action: {action}");
                break;
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowRealClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }
}
