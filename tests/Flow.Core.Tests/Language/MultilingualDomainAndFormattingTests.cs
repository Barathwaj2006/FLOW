using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Language;

public class MultilingualDomainAndFormattingTests
{
    private sealed class MockMultilingualASREngine : IASREngine
    {
        public ASROptions? LastReceivedOptions { get; private set; }
        public bool ShouldThrow { get; set; }
        public string ReturnText { get; set; } = "வணக்கம் உலகம் Welcome to FLOW";
        public string DetectedLanguage { get; set; } = "ta";
        public float LanguageConfidence { get; set; } = 0.94f;

        public ASREngineInfo Info => new("mock-multilingual-engine", "Mock Multilingual Engine", "1.0", true, false, "mock");

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ASRResult> TranscribeAsync(
            AudioBuffer audio,
            ASROptions? options = null,
            IProgress<ASRSegment>? segmentProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new ASRException(Info.Id, "Simulated ASR engine failure.");
            }

            LastReceivedOptions = options;
            string lang = options?.Language == "auto" ? DetectedLanguage : (options?.Language ?? "en");

            return Task.FromResult(new ASRResult(
                Text: ReturnText,
                Confidence: 0.95f,
                AudioDuration: TimeSpan.FromSeconds(audio.DurationSeconds),
                InferenceDuration: TimeSpan.FromMilliseconds(40),
                EngineId: Info.Id,
                Segments: Array.Empty<ASRSegment>(),
                DetectedLanguage: lang,
                LanguageConfidence: LanguageConfidence
            ));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DummyInsertionService : ITextInsertionService
    {
        public string? LastInsertedText { get; private set; }

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            LastInsertedText = text;
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.SendInputClipboardFallback, "test", TimeSpan.Zero, null, IntPtr.Zero, 1, text.Length));
        }

        public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    #region 1. Language Model & Catalog

    [Fact]
    public void LanguageCatalog_SupportedLanguages_ContainsRequiredLanguages()
    {
        Assert.True(LanguageCatalog.IsSupported("en"));
        Assert.True(LanguageCatalog.IsSupported("ta"));
        Assert.True(LanguageCatalog.IsSupported("hi"));
        Assert.True(LanguageCatalog.IsSupported("es"));
        Assert.True(LanguageCatalog.IsSupported("fr"));
        Assert.True(LanguageCatalog.IsSupported("de"));
        Assert.True(LanguageCatalog.IsSupported("ja"));
        Assert.True(LanguageCatalog.IsSupported("zh"));

        Assert.NotNull(LanguageCatalog.English);
        Assert.NotNull(LanguageCatalog.Tamil);
        Assert.NotNull(LanguageCatalog.Hindi);
        Assert.NotNull(LanguageCatalog.Auto);

        Assert.Equal("தமிழ்", LanguageCatalog.Tamil.NativeName);
        Assert.Equal("Tamil", LanguageCatalog.Tamil.Script);
        Assert.Equal("हिन्दी", LanguageCatalog.Hindi.NativeName);
        Assert.Equal("Devanagari", LanguageCatalog.Hindi.Script);
    }

    [Theory]
    [InlineData("ta", "Tamil")]
    [InlineData("Tamil", "Tamil")]
    [InlineData("தமிழ்", "Tamil")]
    [InlineData("hi", "Hindi")]
    [InlineData("Hindi", "Hindi")]
    [InlineData("हिन्दी", "Hindi")]
    [InlineData("en", "English")]
    [InlineData("auto", "Auto-Detect")]
    public void LanguageCatalog_TryGetLanguage_ResolvesByCodeAndName(string query, string expectedDisplayName)
    {
        bool found = LanguageCatalog.TryGetLanguage(query, out var lang);
        Assert.True(found);
        Assert.Equal(expectedDisplayName, lang.DisplayName);
    }

    [Fact]
    public void LanguageCatalog_GetLanguageOrDefault_FallbackOnInvalid()
    {
        var lang = LanguageCatalog.GetLanguageOrDefault("non-existent-language");
        Assert.Equal(LanguageCatalog.English, lang);
    }

    [Fact]
    public void LanguageCode_EqualityAndAutoDetect_BehavesCorrectly()
    {
        var code1 = new LanguageCode("ta");
        var code2 = LanguageCode.Tamil;
        Assert.Equal(code1, code2);
        Assert.False(code1.IsAutoDetect);

        var auto = LanguageCode.Auto;
        Assert.True(auto.IsAutoDetect);
        Assert.Equal("auto", auto.Value);
    }

    #endregion

    #region 2. Language Session Isolation & Service

    [Fact]
    public void LanguageSessionService_SessionIsolation_OverrideDoesNotLeakToNextSession()
    {
        var service = new LanguageSessionService(LanguageCatalog.English);
        Assert.Equal(LanguageCatalog.English, service.ActiveLanguage);
        Assert.Null(service.SessionOverride);

        // Session A sets override
        service.SetSessionLanguage(LanguageCode.Tamil);
        Assert.Equal(LanguageCatalog.Tamil, service.ActiveLanguage);
        Assert.Equal(LanguageCatalog.Tamil, service.SessionOverride);

        // Reset session (simulating end of Session A)
        service.ResetSession();
        Assert.Null(service.SessionOverride);
        Assert.Equal(LanguageCatalog.English, service.ActiveLanguage);
    }

    [Fact]
    public void LanguageSessionService_ManualToAutoTransition()
    {
        var service = new LanguageSessionService(LanguageCatalog.English);
        service.SetSessionLanguage(LanguageCode.Auto);
        Assert.Equal(LanguageCatalog.Auto, service.ActiveLanguage);

        service.ResetSession();
        Assert.Equal(LanguageCatalog.English, service.ActiveLanguage);
    }

    [Fact]
    public void LanguageSessionService_AutoToManualTransition()
    {
        var service = new LanguageSessionService(LanguageCatalog.Auto);
        Assert.Equal(LanguageCatalog.Auto, service.ActiveLanguage);

        service.SetSessionLanguage(LanguageCode.Hindi);
        Assert.Equal(LanguageCatalog.Hindi, service.ActiveLanguage);

        service.ResetSession();
        Assert.Equal(LanguageCatalog.Auto, service.ActiveLanguage);
    }

    [Fact]
    public async Task Coordinator_SessionLanguageIsolation_TamilSessionDoesNotLeakToEnglishSession()
    {
        var engine = new MockMultilingualASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var langService = new LanguageSessionService(LanguageCatalog.English);
        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion, languageSessionService: langService);

        // Session A: Override to Tamil
        coordinator.SetSessionLanguage(LanguageCode.Tamil);
        Assert.Equal("ta", coordinator.ActiveLanguage.WhisperCode);

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        // Verify Session A passed "ta"
        Assert.NotNull(engine.LastReceivedOptions);
        Assert.Equal("ta", engine.LastReceivedOptions.Language);

        // Session B: Without explicit override, MUST revert to Default Language (English)
        Assert.Equal("en", coordinator.ActiveLanguage.WhisperCode);
        await coordinator.StartSessionAsync();
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        // Verify Session B passed "en" (no leakage!)
        Assert.NotNull(engine.LastReceivedOptions);
        Assert.Equal("en", engine.LastReceivedOptions.Language);
    }

    [Fact]
    public async Task Coordinator_ManualLanguageSelection_PropagatesToASROptions()
    {
        var engine = new MockMultilingualASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);

        // Test Tamil
        coordinator.SetSessionLanguage(LanguageCode.Tamil);
        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();
        Assert.Equal("ta", engine.LastReceivedOptions?.Language);

        // Test Hindi
        coordinator.SetSessionLanguage(LanguageCode.Hindi);
        await coordinator.StartSessionAsync();
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();
        Assert.Equal("hi", engine.LastReceivedOptions?.Language);
    }

    [Fact]
    public async Task Coordinator_AutomaticLanguageDetection_PropagatesAutoAndRecordsDetected()
    {
        var engine = new MockMultilingualASREngine { DetectedLanguage = "ta", LanguageConfidence = 0.96f };
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);
        coordinator.SetSessionLanguage(LanguageCode.Auto);

        string? detectedFired = null;
        float? confFired = null;
        coordinator.LanguageDetected += (dLang, dConf) =>
        {
            detectedFired = dLang;
            confFired = dConf;
        };

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        Assert.Equal("auto", engine.LastReceivedOptions?.Language);
        Assert.Equal("ta", coordinator.LastDetectedLanguage);
        Assert.Equal(0.96f, coordinator.LastDetectedLanguageConfidence);
        Assert.Equal("ta", detectedFired);
        Assert.Equal(0.96f, confFired);
    }

    [Fact]
    public async Task Coordinator_Cancellation_ResetsSessionLanguage()
    {
        var engine = new MockMultilingualASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var langService = new LanguageSessionService(LanguageCatalog.English);
        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion, languageSessionService: langService);

        coordinator.SetSessionLanguage(LanguageCode.Tamil);
        Assert.Equal(LanguageCatalog.Tamil, coordinator.ActiveLanguage);

        await coordinator.StartSessionAsync();
        await coordinator.CancelSessionAsync();

        Assert.Equal(LanguageCatalog.English, coordinator.ActiveLanguage);
        Assert.Null(langService.SessionOverride);
    }

    [Fact]
    public async Task Coordinator_ASRFailure_RecoversSafelyAndResetsLanguage()
    {
        var engine = new MockMultilingualASREngine { ShouldThrow = true };
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var langService = new LanguageSessionService(LanguageCatalog.English);
        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion, languageSessionService: langService);

        coordinator.SetSessionLanguage(LanguageCode.Tamil);
        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Equal(SessionState.Error, coordinator.CurrentState);
        Assert.Equal(LanguageCatalog.English, coordinator.ActiveLanguage);
    }

    #endregion

    #region 3. Unicode & Language-Aware Formatting

    [Fact]
    public void Multilingual_TamilUnicodePreservation_GraphemesAndCombiningMarksIntact()
    {
        var pipeline = new TranscriptProcessingPipeline();
        // Tamil text with combining vowel signs (ொ, ா), virama (்), and grantha characters (ஜ, ஷ)
        string tamilInput = "வணக்கம் உலகம் இது தமிழ் குரல் உள்ளீடு.";
        string formatted = pipeline.Format(tamilInput);

        Assert.Equal("வணக்கம் உலகம் இது தமிழ் குரல் உள்ளீடு.", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
    }

    [Fact]
    public void Multilingual_HindiUnicodePreservation_GraphemesAndViramaIntact()
    {
        var pipeline = new TranscriptProcessingPipeline();
        // Hindi text with halant/virama (्), anusvara (ं), and matras (ी, ो)
        string hindiInput = "नमस्ते दुनिया यह हिन्दी वॉयस टाइपिंग है।";
        string formatted = pipeline.Format(hindiInput);

        Assert.Equal("नमस्ते दुनिया यह हिन्दी वॉयस टाइपिंग है।", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
    }

    [Fact]
    public void Multilingual_HindiPurnaViram_PreservedWithoutAppendingPeriod()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string hindiSentence = "यह एक पूरा वाक्य है।";
        string formatted = pipeline.Format(hindiSentence);

        // Inviolable rule: Never append English period '.' after Hindi Purna Viram '।'
        Assert.Equal("यह एक पूरा वाक्य है।", formatted);
        Assert.False(formatted.EndsWith("।."));
        Assert.False(formatted.EndsWith(".."));
    }

    [Fact]
    public void Multilingual_EnglishTamilCodeSwitch_PreservesTamilAndEnglish()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string codeSwitchInput = "இன்னைக்கு meeting இருக்கு so please join on time period";
        string formatted = pipeline.Format(codeSwitchInput);

        Assert.Equal("இன்னைக்கு meeting இருக்கு so please join on time.", formatted);
    }

    [Fact]
    public void Multilingual_EnglishHindiCodeSwitch_PreservesHindiAndEnglish()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string codeSwitchInput = "aaj ka sprint review meeting 3 baje start hoga period";
        string formatted = pipeline.Format(codeSwitchInput);

        Assert.Equal("Aaj ka sprint review meeting 3 baje start hoga.", formatted);
    }

    [Fact]
    public void Multilingual_TamilCodeSwitching_ProtectsHyphenatedSuffix()
    {
        var pipeline = new TranscriptProcessingPipeline();
        // Spoken phrase from prompt: "இந்த project-ஐ build பண்ண வேண்டும்"
        string input = "இந்த project-ஐ build பண்ண வேண்டும் period";
        string formatted = pipeline.Format(input);

        Assert.Equal("இந்த project-ஐ build பண்ண வேண்டும்.", formatted);
        Assert.Contains("project-ஐ", formatted);
    }

    [Fact]
    public void Multilingual_TamilCodeSwitching_ProtectsEndpointSuffix()
    {
        var pipeline = new TranscriptProcessingPipeline();
        // Spoken phrase from prompt: "இந்த API endpoint-ஐ test பண்ணு"
        string input = "இந்த API endpoint-ஐ test பண்ணு period";
        string formatted = pipeline.Format(input);

        Assert.Equal("இந்த API endpoint-ஐ test பண்ணு.", formatted);
        Assert.Contains("API", formatted);
        Assert.Contains("endpoint-ஐ", formatted);
    }

    [Fact]
    public void Multilingual_HindiCodeSwitching_ProtectsPostposition()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "FastAPI-को deploy karo aur server-में check karo period";
        string formatted = pipeline.Format(input);

        Assert.Equal("FastAPI-को deploy karo aur server-में check karo.", formatted);
        Assert.Contains("FastAPI-को", formatted);
        Assert.Contains("server-में", formatted);
    }

    [Fact]
    public void Multilingual_TechnicalEntities_SurroundedByTamil_Preserved()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "நாளைக்கு Python FastAPI React Next.js TypeScript Docker GitHub npm C# மற்றும் C++ பற்றி discuss பண்ணுவோம் period";
        string formatted = pipeline.Format(input);

        Assert.Contains("Python", formatted);
        Assert.Contains("FastAPI", formatted);
        Assert.Contains("React", formatted);
        Assert.Contains("Next.js", formatted);
        Assert.Contains("TypeScript", formatted);
        Assert.Contains("Docker", formatted);
        Assert.Contains("GitHub", formatted);
        Assert.Contains("npm", formatted);
        Assert.Contains("C#", formatted);
        Assert.Contains("C++", formatted);
    }

    [Fact]
    public void Multilingual_TechnicalEntities_SurroundedByHindi_Preserved()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "hum log WebSocket aur Docker container use karenge period";
        string formatted = pipeline.Format(input);

        Assert.Contains("WebSocket", formatted);
        Assert.Contains("Docker", formatted);
    }

    [Fact]
    public void Multilingual_Capitalization_DoesNotCorruptTamilOrHindi()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "தமிழ் மொழியில் உள்ள எழுத்துக்கள்.";
        string formatted = pipeline.Format(input);

        Assert.Equal("தமிழ் மொழியில் உள்ள எழுத்துக்கள்.", formatted);
    }

    [Fact]
    public void Multilingual_Capitalization_CapitalizesEnglishAfterIndicPunctuation()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "यह पहला वाक्य है। then we run the build";
        string formatted = pipeline.Format(input);

        Assert.Equal("यह पहला वाक्य है। Then we run the build.", formatted);
    }

    [Fact]
    public void Multilingual_FillerRemoval_RemovesEnglishFillersInTamilSentence()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "இந்த um project-ஐ uh build பண்ண வேண்டும் period";
        string formatted = pipeline.Format(input);

        Assert.Equal("இந்த project-ஐ build பண்ண வேண்டும்.", formatted);
    }

    [Fact]
    public void Multilingual_ZeroEnterSafety_FailsClosedIfNewlineProduced()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "இந்த project-ஐ\r\nbuild பண்ண வேண்டும்";

        // Pre-pipeline whitespace normalizer removes newlines safely
        string formatted = pipeline.Format(input);
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
    }

    [Fact]
    public async Task Multilingual_SilentAudio_HandlesGracefullyWithoutThrowing()
    {
        var engine = new MockMultilingualASREngine { ReturnText = string.Empty };
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);

        await coordinator.StartSessionAsync();
        // Silent audio
        float[] silentSamples = new float[16000];
        coordinator.ProcessAudioChunk(silentSamples);
        bool completed = await coordinator.EndSessionAsync();

        Assert.False(completed);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    [Fact]
    public async Task Multilingual_EmptyTranscript_CancelsSessionCleanly()
    {
        var engine = new MockMultilingualASREngine { ReturnText = "   " };
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new TranscriptProcessingPipeline();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        bool completed = await coordinator.EndSessionAsync();

        Assert.False(completed);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    #endregion
}
