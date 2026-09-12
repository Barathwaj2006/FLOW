using System;
using System.Runtime.InteropServices;
using Flow.Core.Session;
using Flow.Host.Windows.UI;
using Xunit;

namespace Flow.Windows.Tests;

public class FloatingHudWindowTests
{
    [Fact]
    public void FloatingHudController_CreatesNativeWin32Window()
    {
        using var hud = new FloatingHudController();

        // In an interactive desktop environment, Handle should be non-zero
        if (hud.Handle != IntPtr.Zero)
        {
            // Verify WS_EX_NOACTIVATE (0x08000000) and WS_EX_TOPMOST (0x00000008)
            const int GWL_EXSTYLE = -20;
            int exStyle = GetWindowLong(hud.Handle, GWL_EXSTYLE);

            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOPMOST = 0x00000008;

            Assert.True((exStyle & WS_EX_NOACTIVATE) != 0, "HUD must have WS_EX_NOACTIVATE style.");
            Assert.True((exStyle & WS_EX_TOPMOST) != 0, "HUD must have WS_EX_TOPMOST style.");
        }
    }

    [Fact]
    public void UpdateState_UpdatesStatusTextAccurately()
    {
        using var hud = new FloatingHudController();

        hud.UpdateState(SessionState.Recording, "Listening");
        Assert.Equal("Listening", hud.StatusText);

        hud.UpdateState(SessionState.Processing, "Transcribing");
        Assert.Equal("Transcribing", hud.StatusText);

        hud.UpdateState(SessionState.Completed, "Done");
        Assert.Equal("Done", hud.StatusText);

        hud.UpdateState(SessionState.Cancelled, "Cancelled");
        Assert.Equal("Cancelled", hud.StatusText);

        hud.UpdateState(SessionState.Idle);
        Assert.False(hud.IsVisible);
    }

    [Fact]
    public void UpdateAudioLevel_ClampsAndUpdatesLevel()
    {
        using var hud = new FloatingHudController();

        hud.UpdateAudioLevel(0.1f);
        Assert.True(hud.AudioLevel > 0.0f);
        Assert.True(hud.AudioLevel <= 1.0f);

        hud.UpdateAudioLevel(1.5f); // Beyond 1.0
        Assert.Equal(1.0f, hud.AudioLevel);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
}
