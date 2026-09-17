using System;
using System.Windows;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Unified abstraction for FLOW desktop host windows (FlowShellWindow and FlowHubWindow).
/// Enables seamless runtime switching between WebView2 Embedded UI and Native WPF Fallback.
/// </summary>
public interface IFlowMainWindow
{
    bool AllowRealClose { get; set; }
    void SelectTab(int targetTab);
    event Action? ExitApplicationRequested;
    void Show();
    void Hide();
    bool Activate();
    bool Focus();
    WindowState WindowState { get; set; }
    Visibility Visibility { get; set; }
    void Close();
}
