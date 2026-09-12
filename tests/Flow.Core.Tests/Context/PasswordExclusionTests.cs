using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Context;

public class PasswordExclusionTests
{
    private sealed class TrackingInsertionService : ITextInsertionService
    {
        public bool InsertCalled { get; private set; }
        public string? LastInsertedText { get; private set; }

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertCalled = true;
            LastInsertedText = text;
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.SendInputClipboardFallback, "notepad", TimeSpan.FromMilliseconds(5), null, IntPtr.Zero, 1, text.Length));
        }

        public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task StartSessionAsync_WhenFocusedInPasswordField_RefusesRecordingAndCancelsSession()
    {
        // Arrange: mock context service returning true for password field
        var passwordContext = new NullUIContextService(isPasswordField: true);
        var ringBuffer = new AudioRingBuffer(10.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var asr = new ASREngineRegistry();
        var lang = new DeterministicTextSanitizer();
        var insertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asr,
            lang,
            insertion,
            contextService: passwordContext
        );

        string? warningMessage = null;
        coordinator.SessionWarning += msg => warningMessage = msg;

        // Act
        await coordinator.StartSessionAsync();

        // Assert
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.NotNull(warningMessage);
        Assert.Contains("Password field", warningMessage);

        // Feed audio chunks - should be ignored since not in Recording state
        coordinator.ProcessAudioChunk(new float[1600]);
        Assert.Equal(0, ringBuffer.AvailableSamples);

        // End session - should fail closed and never insert text
        bool ended = await coordinator.EndSessionAsync();
        Assert.False(ended);
        Assert.False(insertion.InsertCalled);
    }

    [Fact]
    public async Task StartSessionAsync_WhenFocusedInPasswordField_DoesNotExtractPasswordTextAsContext()
    {
        // Context service contains sensitive text but is flagged as password field
        var passwordContext = new NullUIContextService(isPasswordField: true, nearbyContext: "secret_password_123");
        var ringBuffer = new AudioRingBuffer(10.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var asr = new ASREngineRegistry();
        var lang = new DeterministicTextSanitizer();
        var insertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asr,
            lang,
            insertion,
            contextService: passwordContext
        );

        await coordinator.StartSessionAsync();

        // Context MUST be empty or null; never expose password characters
        Assert.True(string.IsNullOrEmpty(coordinator.ActiveNearbyContext), "Password text was leaked into ActiveNearbyContext!");
    }

    [Fact]
    public async Task EndSessionAsync_WhenFocusShiftsToPasswordField_BlocksInsertion()
    {
        // Start in normal field, but switch to password field before insertion
        bool isPassword = false;
        var dynamicContext = new DynamicPasswordContext(() => isPassword);
        var ringBuffer = new AudioRingBuffer(10.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var asr = new ASREngineRegistry();
        asr.Register(new MockASREngine("whisper-mock", "test transcript"), isDefault: true);
        var lang = new DeterministicTextSanitizer();
        var insertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asr,
            lang,
            insertion,
            contextService: dynamicContext
        );

        // Start session in safe text control
        await coordinator.StartSessionAsync();
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        // Feed some speech
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        // Focus switches to password field before user finishes dictation
        isPassword = true;

        // End session
        bool ended = await coordinator.EndSessionAsync();

        // Assert fail-closed protection
        Assert.False(ended);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.False(insertion.InsertCalled, "Text was inserted into a password field!");
    }

    [Fact]
    public void NullUIContextService_NearbyContext_RespectsBoundedMaximum()
    {
        string longContext = new string('A', 500);
        var contextService = new NullUIContextService(isPasswordField: false, nearbyContext: longContext);

        string extracted = contextService.GetNearbyContext(200);

        Assert.Equal(200, extracted.Length);
        Assert.Equal(new string('A', 200), extracted);
    }

    private sealed class DynamicPasswordContext : IUIContextService
    {
        private readonly Func<bool> _isPasswordFunc;

        public DynamicPasswordContext(Func<bool> isPasswordFunc)
        {
            _isPasswordFunc = isPasswordFunc;
        }

        public bool IsFocusInPasswordField() => _isPasswordFunc();

        public string GetNearbyContext(int maxCharacters = 200)
        {
            return _isPasswordFunc() ? string.Empty : "safe context";
        }

        public string GetSelectedText(int maxCharacters = 10000)
        {
            return _isPasswordFunc() ? string.Empty : "safe selection";
        }

        public bool HasSelectedText()
        {
            return !_isPasswordFunc();
        }
    }
}
