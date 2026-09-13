using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Flow.Core.Commands;
using Flow.Core.Session;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.UI;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

public class Phase2FPhysicalValidationTests
{
    private readonly ITestOutputHelper _output;

    public Phase2FPhysicalValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region WF-036: Command Mode Shortcut Wiring

    [Fact]
    public void WF_036_PhysicalValidation_CommandModeShortcut_EventsWireable()
    {
        using var hook = new GlobalHotkeyHook();
        bool keyDownFired = false;
        bool keyUpFired = false;

        hook.CommandModeHotkeyDown += () => keyDownFired = true;
        hook.CommandModeHotkeyUp += () => keyUpFired = true;

        _output.WriteLine("[WF-036] GlobalHotkeyHook command mode events wired successfully.");
        Assert.False(keyDownFired);
        Assert.False(keyUpFired);
    }

    #endregion

    #region WF-037A: Real WPF Selection Extraction & Transformation

    [Fact]
    public void WF_037A_PhysicalValidation_RealWpfTextBox_ExtractSelectionAndTransformToBullets()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Command Mode Selection Test",
                    Width = 400,
                    Height = 250,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var txtBox = new TextBox
                {
                    Name = "CommandTestTextBox",
                    Text = "milk, eggs, bread",
                    AcceptsReturn = true
                };

                window.Content = txtBox;
                window.Show();
                txtBox.Focus();

                // Select the text in the TextBox
                txtBox.Select(0, txtBox.Text.Length);
                string selection = txtBox.SelectedText;
                _output.WriteLine($"[WF-037A] Selected text from WPF TextBox: '{selection}'");
                Assert.Equal("milk, eggs, bread", selection);

                // Run deterministic transform engine
                var engine = new DeterministicTextTransformEngine();
                string transformed = engine.Transform(selection, TransformType.BulletList);
                _output.WriteLine($"[WF-037A] Transformed bullet list: '{transformed}'");
                Assert.Equal("• Milk • Eggs • Bread", transformed);

                // Safe text replacement
                txtBox.SelectedText = transformed;
                Assert.Equal("• Milk • Eggs • Bread", txtBox.Text);

                // Verify Zero-Enter invariant: no \r or \n
                Assert.DoesNotContain("\r", txtBox.Text);
                Assert.DoesNotContain("\n", txtBox.Text);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null)
        {
            throw new Exception("STA Thread failed in real WPF selection test", threadEx);
        }
    }

    [Fact]
    public void WF_037A_PhysicalValidation_RealWpfTextBox_TransformCasing()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Casing Transform Test",
                    Width = 350,
                    Height = 200,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var txtBox = new TextBox
                {
                    Name = "CasingTextBox",
                    Text = "user profile manager"
                };

                window.Content = txtBox;
                window.Show();
                txtBox.Focus();
                txtBox.Select(0, txtBox.Text.Length);

                var engine = new DeterministicTextTransformEngine();
                string camel = engine.Transform(txtBox.SelectedText, TransformType.CamelCase);
                _output.WriteLine($"[WF-037A] CamelCase: {camel}");
                Assert.Equal("userProfileManager", camel);

                string snake = engine.Transform(txtBox.SelectedText, TransformType.SnakeCase);
                _output.WriteLine($"[WF-037A] SnakeCase: {snake}");
                Assert.Equal("user_profile_manager", snake);

                string pascal = engine.Transform(txtBox.SelectedText, TransformType.PascalCase);
                _output.WriteLine($"[WF-037A] PascalCase: {pascal}");
                Assert.Equal("UserProfileManager", pascal);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null)
        {
            throw new Exception("STA Thread failed in casing transform test", threadEx);
        }
    }

    [Fact]
    public void WF_037A_PhysicalValidation_PasswordField_SelectionExtractionBlocked()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Password Box Selection Blocked Test",
                    Width = 300,
                    Height = 200,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var pwdBox = new PasswordBox
                {
                    Name = "TestPwdBox",
                    Password = "Secret123Password"
                };

                window.Content = pwdBox;
                window.Show();
                pwdBox.Focus();

                var contextService = new WindowsUIAutomationContextService();
                var pwdElement = AutomationElement.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);

                bool isPassword = contextService.IsFocusInPasswordField(pwdElement);
                string selected = contextService.GetSelectedText(1000, pwdElement);

                _output.WriteLine($"[WF-037A] Password element detected: {isPassword}, selection extracted: '{selected}'");

                // Inviolable safety: Never extract selection from password field
                Assert.Empty(selected);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null)
        {
            throw new Exception("STA Thread failed in password box test", threadEx);
        }
    }

    #endregion

    #region WF-037B: Floating HUD Transforms Widget

    [Fact]
    public void WF_037B_PhysicalValidation_FloatingHudController_CommandModeTransitions()
    {
        using var hud = new FloatingHudController();

        // 1. Enter Command Mode Listening
        hud.UpdateState(SessionState.Recording, isCommandMode: true);
        Assert.True(hud.IsCommandMode);
        Assert.Contains("🪄", hud.StatusText);
        Assert.Contains("Command", hud.StatusText);
        _output.WriteLine($"[WF-037B] Command Recording HUD: {hud.StatusText}");

        // 2. Transforming state
        hud.UpdateState(SessionState.Processing, isCommandMode: true);
        Assert.True(hud.IsCommandMode);
        Assert.Contains("🪄", hud.StatusText);
        Assert.Contains("Transforming", hud.StatusText);
        _output.WriteLine($"[WF-037B] Command Processing HUD: {hud.StatusText}");

        // 3. Completed state
        hud.UpdateState(SessionState.Completed, isCommandMode: true);
        Assert.Equal("🪄 Transformed", hud.StatusText);
        _output.WriteLine($"[WF-037B] Command Completed HUD: {hud.StatusText}");

        // 4. Return to Idle
        hud.UpdateState(SessionState.Idle);
        Assert.False(hud.IsCommandMode);
        Assert.Equal("Ready", hud.StatusText);
        _output.WriteLine($"[WF-037B] Reset to Idle HUD: {hud.StatusText}");
    }

    #endregion

    #region WF-038: Zero-Destructive Execution Safety

    [Theory]
    [InlineData("shutdown /s /t 0")]
    [InlineData("Remove-Item -Recurse C:\\Windows")]
    [InlineData("del /f /q C:\\Users\\*")]
    [InlineData("format C:")]
    [InlineData("powershell.exe -Command Stop-Computer")]
    [InlineData("cmd.exe /c rd /s /q C:\\")]
    public void WF_038_PhysicalValidation_DangerousShellCommands_PermanentlyBlocked(string shellCommand)
    {
        var safetyPolicy = new DeterministicCommandSafetyPolicy();
        var result = safetyPolicy.EvaluateTranscript(shellCommand);

        _output.WriteLine($"[WF-038] Command '{shellCommand}' safety verdict: {result.Verdict} ({result.Reason})");

        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
        Assert.Contains("strictly blocked", result.Reason);
    }

    #endregion
}
