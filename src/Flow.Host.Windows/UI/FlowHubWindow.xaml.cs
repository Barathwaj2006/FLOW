using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Flow.Core.History;
using Flow.Core.Scratchpad;
using Flow.Core.Session;
using Flow.Host.Windows.History;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Scratchpad;

namespace Flow.Host.Windows.UI;

public partial class FlowHubWindow : Window
{
    private readonly VoiceSessionCoordinator? _coordinator;
    private readonly WasapiAudioCapture? _capture;
    private readonly WasapiDeviceManager? _deviceManager;
    private readonly IHistoryService? _historyService;
    private readonly IScratchpadService? _scratchpadService;

    public bool AllowRealClose { get; set; }
    public event Action? ExitApplicationRequested;
    public event Action<bool>? DeveloperModeToggled;

    public FlowHubWindow()
    {
        InitializeComponent();
    }

    public FlowHubWindow(
        VoiceSessionCoordinator? coordinator,
        WasapiAudioCapture? capture,
        WasapiDeviceManager? deviceManager,
        IHistoryService? historyService,
        IScratchpadService? scratchpadService) : this()
    {
        _coordinator = coordinator;
        _capture = capture;
        _deviceManager = deviceManager;
        _historyService = historyService;
        _scratchpadService = scratchpadService;

        Loaded += FlowHubWindow_Loaded;
    }

    private void FlowHubWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Enforce strict landing on Dashboard (index 0)
        if (NavListBox != null) NavListBox.SelectedIndex = 0;
        if (MainTabControl != null) MainTabControl.SelectedIndex = 0;

        PopulateAudioDevices();

        if (_coordinator != null)
        {
            _coordinator.AudioLevelChanged += Coordinator_AudioLevelChanged;
            _coordinator.StateChanged += Coordinator_StateChanged;
            _coordinator.FinalTextInserted += Coordinator_FinalTextInserted;
        }

        UpdateStatusBadge();
    }

    private void PopulateAudioDevices()
    {
        ComboAudioDevices.Items.Clear();
        if (_deviceManager == null) return;

        var devices = _deviceManager.EnumerateCaptureDevices();
        string? defaultId = _deviceManager.GetDefaultCaptureDeviceId();
        var defaultDev = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
        string defaultName = defaultDev?.Name ?? "Default Windows Microphone";

        TxtActiveDevice.Text = $"Active Device: {defaultName}";

        int selectIdx = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            var dev = devices[i];
            string label = dev.Name + (dev.IsDefault ? " (Default)" : "");
            ComboAudioDevices.Items.Add(new ComboBoxItem { Content = label, Tag = dev.Id });
            if (dev.IsDefault || dev.Id == defaultId)
            {
                selectIdx = i;
            }
        }

        if (ComboAudioDevices.Items.Count > 0)
        {
            ComboAudioDevices.SelectedIndex = selectIdx;
        }
        else
        {
            ComboAudioDevices.Items.Add(new ComboBoxItem { Content = "No active recording device detected", Tag = null });
            ComboAudioDevices.SelectedIndex = 0;
            TxtActiveDevice.Text = "Active Device: No physical microphone detected";
        }
    }

    private void Coordinator_AudioLevelChanged(float rms)
    {
        Dispatcher.BeginInvoke(() =>
        {
            int percent = Math.Clamp((int)(rms * 400.0f), 0, 100);
            ProgressBarAudioLevel.Value = percent;
            TxtAudioLevelPercent.Text = $"Level: {percent}%";
        });
    }

    private void Coordinator_StateChanged(SessionState state, string? detail)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateStatusBadge();
            if (state == SessionState.Recording)
            {
                BtnToggleDictation.Content = "Stop Dictation";
            }
            else
            {
                BtnToggleDictation.Content = "Toggle Dictation";
            }
        });
    }

    private void Coordinator_FinalTextInserted(string text)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (TxtTestDictation.IsFocused)
            {
                TxtTestDictation.AppendText(" " + text);
                TxtTestDictation.CaretIndex = TxtTestDictation.Text.Length;
            }
        });
    }

    private void UpdateStatusBadge()
    {
        if (_coordinator == null)
        {
            StatusBadgeText.Text = "FLOW Active & Ready";
            BtnToggleDictation.Content = "Toggle Dictation";
            return;
        }

        if (_coordinator.CurrentState == SessionState.Recording)
        {
            StatusBadgeText.Text = "Recording Audio...";
            BtnToggleDictation.Content = "Stop Dictation";
        }
        else if (_coordinator.CurrentState == SessionState.Processing)
        {
            StatusBadgeText.Text = "Transcribing Locally...";
            BtnToggleDictation.Content = "Toggle Dictation";
        }
        else
        {
            StatusBadgeText.Text = "FLOW Active & Ready";
            BtnToggleDictation.Content = "Toggle Dictation";
        }
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl == null || NavListBox == null) return;
        if (NavListBox.SelectedIndex >= 0 && NavListBox.SelectedIndex < MainTabControl.Items.Count)
        {
            MainTabControl.SelectedIndex = NavListBox.SelectedIndex;
        }
    }

    private void BtnToggleDictation_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator == null || _capture == null) return;

        if (_coordinator.CurrentState == SessionState.Recording)
        {
            _capture.Stop();
            _ = Task.Run(async () => await _coordinator.EndSessionAsync());
        }
        else
        {
            _ = Task.Run(async () =>
            {
                await _coordinator.StartSessionAsync(isHandsFree: true);
                _capture.Start();
            });
        }
    }

    private void BtnClearTest_Click(object sender, RoutedEventArgs e)
    {
        TxtTestDictation.Clear();
    }

    private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        PopulateAudioDevices();
    }

    private void BtnApplyDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_capture == null) return;

        if (ComboAudioDevices.SelectedItem is ComboBoxItem item && item.Tag is string deviceId)
        {
            bool wasCapturing = _capture.IsCapturing;
            if (wasCapturing) _capture.Stop();
            _capture.TargetDeviceId = deviceId;
            if (wasCapturing) _capture.Start();

            TxtActiveDevice.Text = $"Active Device: {item.Content}";
            MessageBox.Show(this, $"Microphone endpoint set to: {item.Content}", "FLOW Audio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ChkDevMode_Changed(object sender, RoutedEventArgs e)
    {
        bool isDev = ChkDevMode.IsChecked == true;
        DeveloperModeToggled?.Invoke(isDev);
    }

    private void BtnOpenScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (_scratchpadService != null)
        {
            ScratchpadWindowManager.ShowWindow(_scratchpadService);
        }
    }

    private void BtnOpenHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_historyService != null)
        {
            HistoryWindowManager.ShowWindow(_historyService);
        }
    }

    private void BtnMinimizeTray_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void BtnExitApp_Click(object sender, RoutedEventArgs e)
    {
        AllowRealClose = true;
        ExitApplicationRequested?.Invoke();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowRealClose)
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            if (_coordinator != null)
            {
                _coordinator.AudioLevelChanged -= Coordinator_AudioLevelChanged;
                _coordinator.StateChanged -= Coordinator_StateChanged;
                _coordinator.FinalTextInserted -= Coordinator_FinalTextInserted;
            }
            base.OnClosing(e);
        }
    }
}
