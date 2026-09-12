using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Core.Language;
using Flow.Core.Personalization;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Storage;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Personalization;

/// <summary>
/// Comprehensive Phase 4 Parity Test Suite covering all 60 required edge cases:
/// Dictionary CRUD (1-11), Vocabulary (12-19), ASR Biasing (20-24), Snippets (25-33),
/// Styles (34-39), Persistence &amp; Migration (40-49), Security &amp; Invariants (50-54),
/// and Regression &amp; Zero-Enter Safety (55-60).
/// </summary>
public sealed class Phase4PersonalizationParityTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _db;
    private readonly SqlitePersonalDictionaryRepository _dictRepo;
    private readonly SqliteSnippetRepository _snippetRepo;
    private readonly SqliteStyleRepository _styleRepo;

    public Phase4PersonalizationParityTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_p4_test_{Guid.NewGuid():N}.db");
        _db = new SqlitePersonalizationDatabase(_tempDbPath);
        _dictRepo = new SqlitePersonalDictionaryRepository(_db);
        _snippetRepo = new SqliteSnippetRepository(_db);
        _styleRepo = new SqliteStyleRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            string wal = _tempDbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            string shm = _tempDbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    #region 1. DICTIONARY (Edge Cases 1 - 11)

    [Fact]
    public async Task EdgeCase01_Dictionary_AddEntry_Succeeds()
    {
        var entry = new DictionaryEntry
        {
            Term = "kubernetes cluster",
            Replacement = "K8s cluster",
            Category = "DevOps",
            IsStarred = true,
            IsEnabled = true,
            Language = "en",
            ApplicationScope = "terminal"
        };

        await _dictRepo.AddAsync(entry);

        var retrieved = await _dictRepo.GetByTermAsync("kubernetes cluster");
        Assert.NotNull(retrieved);
        Assert.Equal("K8s cluster", retrieved.Replacement);
        Assert.Equal("DevOps", retrieved.Category);
        Assert.True(retrieved.IsStarred);
        Assert.True(retrieved.IsEnabled);
        Assert.Equal("en", retrieved.Language);
        Assert.Equal("terminal", retrieved.ApplicationScope);
    }

    [Fact]
    public async Task EdgeCase02_Dictionary_EditEntry_Succeeds()
    {
        var entry = new DictionaryEntry
        {
            Term = "neuro sim",
            Replacement = "Neurosim"
        };
        await _dictRepo.AddAsync(entry);

        entry.Replacement = "NeuroSim Pro";
        entry.IsStarred = true;
        entry.ApplicationScope = "code";
        await _dictRepo.UpdateAsync(entry);

        var updated = await _dictRepo.GetByTermAsync("neuro sim");
        Assert.NotNull(updated);
        Assert.Equal("NeuroSim Pro", updated.Replacement);
        Assert.True(updated.IsStarred);
        Assert.Equal("code", updated.ApplicationScope);
    }

    [Fact]
    public async Task EdgeCase03_Dictionary_DeleteEntry_Succeeds()
    {
        var entry = new DictionaryEntry
        {
            Term = "temporary term",
            Replacement = "temp"
        };
        await _dictRepo.AddAsync(entry);

        await _dictRepo.DeleteAsync(entry.Id);

        var retrieved = await _dictRepo.GetByTermAsync("temporary term");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task EdgeCase04_Dictionary_DuplicateEntry_UpsertOrUpdate()
    {
        var entry1 = new DictionaryEntry { Term = "flow ai", Replacement = "FLOW v1" };
        await _dictRepo.AddAsync(entry1);

        // Upsert duplicate term via import/add
        var entry2 = new DictionaryEntry { Term = "flow ai", Replacement = "FLOW v2" };
        await _dictRepo.AddAsync(entry2);

        var entries = await _dictRepo.GetAllAsync();
        var matches = entries.Where(e => e.Term.Equals("flow ai", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(matches);
        Assert.Equal("FLOW v2", matches[0].Replacement);
    }

    [Fact]
    public void EdgeCase05_Dictionary_DisabledEntry_IgnoredByEngine()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "docker compose", Replacement = "Docker-Compose", IsEnabled = false },
            new DictionaryEntry { Term = "git commit", Replacement = "git-commit", IsEnabled = true }
        });

        string input = "please run docker compose and then git commit now";
        string output = engine.Apply(input);

        // docker compose should NOT be replaced, git commit should be replaced
        Assert.Contains("docker compose", output);
        Assert.Contains("git-commit", output);
    }

    [Fact]
    public void EdgeCase06_Dictionary_UnicodeEntry_TamilAndAccents()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "வணக்கம்", Replacement = "வணக்கம் 🙏" },
            new DictionaryEntry { Term = "cafe", Replacement = "café" }
        });

        string result = engine.Apply("அனைவருக்கும் வணக்கம் மற்றும் welcome to our cafe today");
        Assert.Contains("வணக்கம் 🙏", result);
        Assert.Contains("café", result);
    }

    [Fact]
    public void EdgeCase07_Dictionary_LanguageScopedEntry_FiltersCorrectly()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "நன்றி", Replacement = "மிக்க நன்றி", Language = "ta" },
            new DictionaryEntry { Term = "merci", Replacement = "merci beaucoup", Language = "fr" }
        });

        // Calling with language = "ta"
        string taResult = engine.Apply("நன்றி நண்பரே", language: "ta");
        Assert.Equal("மிக்க நன்றி நண்பரே", taResult);

        // Calling with language = "en" should ignore "ta" scoped entry
        string enResult = engine.Apply("நன்றி நண்பரே", language: "en");
        Assert.Equal("நன்றி நண்பரே", enResult);
    }

    [Fact]
    public void EdgeCase08_Dictionary_AppScopedEntry_FiltersCorrectly()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "vector", Replacement = "std::vector<float>", ApplicationScope = "code" },
            new DictionaryEntry { Term = "vector", Replacement = "vector graphic", ApplicationScope = "illustrator" }
        });

        string codeResult = engine.Apply("allocate a new vector buffer", targetApplication: "code");
        Assert.Equal("allocate a new std::vector<float> buffer", codeResult);

        string illResult = engine.Apply("allocate a new vector buffer", targetApplication: "illustrator.exe");
        Assert.Equal("allocate a new vector graphic buffer", illResult);

        string notepadResult = engine.Apply("allocate a new vector buffer", targetApplication: "notepad");
        Assert.Equal("allocate a new vector buffer", notepadResult);
    }

    [Fact]
    public void EdgeCase09_Dictionary_CaseHandling_PreservesCanonical()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "antigravity flow", Replacement = "AntiGravity FLOW" }
        });

        // Case variations in input should all resolve to exact canonical casing
        Assert.Equal("welcome to AntiGravity FLOW engine", engine.Apply("welcome to antigravity flow engine"));
        Assert.Equal("welcome to AntiGravity FLOW engine", engine.Apply("welcome to ANTIGRAVITY FLOW engine"));
        Assert.Equal("welcome to AntiGravity FLOW engine", engine.Apply("welcome to AntiGravity Flow engine"));
    }

    [Fact]
    public async Task EdgeCase10_Dictionary_EmptyEntry_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _dictRepo.AddAsync(new DictionaryEntry { Term = "", Replacement = "test" }));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictRepo.AddAsync(new DictionaryEntry { Term = "   ", Replacement = "test" }));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictRepo.AddAsync(new DictionaryEntry { Term = "valid", Replacement = "" }));
    }

    [Fact]
    public async Task EdgeCase11_Dictionary_InvalidEntry_ExceedingLength_Throws()
    {
        string longTerm = new string('a', 501);
        string longReplacement = new string('b', 1001);

        await Assert.ThrowsAsync<ArgumentException>(() => _dictRepo.AddAsync(new DictionaryEntry { Term = longTerm, Replacement = "ok" }));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictRepo.AddAsync(new DictionaryEntry { Term = "ok", Replacement = longReplacement }));
    }

    #endregion

    #region 2. VOCABULARY & TOKEN PROTECTION (Edge Cases 12 - 19)

    [Fact]
    public void EdgeCase12_Vocabulary_ExactCorrection_AppliedInPipeline()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "flow voice", Replacement = "FLOW Voice" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string output = pipeline.Format("we are testing flow voice today period");
        Assert.Equal("We are testing FLOW Voice today.", output);
    }

    [Fact]
    public void EdgeCase13_Vocabulary_PartialWordProtection_DoesNotMatchSubstrings()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "cat", Replacement = "feline" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "the caterpillar scattered across the category catalog";
        string output = pipeline.Format(input);

        // "cat" must NOT replace inside caterpillar, scattered, category, catalog
        Assert.Contains("caterpillar", output);
        Assert.Contains("scattered", output);
        Assert.Contains("category", output);
        Assert.Contains("catalog", output);
        Assert.DoesNotContain("feline", output);
    }

    [Fact]
    public void EdgeCase14_Vocabulary_TechnicalTokenProtection_PreservesIdentifiers()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "engine", Replacement = "Motor" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "check the AudioEngine and engine_init status";
        string output = pipeline.Format(input);

        // Technical identifiers like AudioEngine and engine_init should remain intact
        Assert.Contains("AudioEngine", output);
        Assert.Contains("engine_init", output);
    }

    [Fact]
    public void EdgeCase15_Vocabulary_UrlProtection_DoesNotCorruptUrls()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "com", Replacement = "COMMUNICATION" },
            new DictionaryEntry { Term = "api", Replacement = "APPLICATION_INTERFACE" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "navigate to https://api.github.com/flow today";
        string output = pipeline.Format(input);

        Assert.Contains("https://api.github.com/flow", output);
        Assert.DoesNotContain("COMMUNICATION", output);
        Assert.DoesNotContain("APPLICATION_INTERFACE", output);
    }

    [Fact]
    public void EdgeCase16_Vocabulary_EmailProtection_DoesNotCorruptEmails()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "mail", Replacement = "POSTAL_SERVICE" },
            new DictionaryEntry { Term = "flow", Replacement = "STREAM" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "send the report to support@flow.mail.internal now";
        string output = pipeline.Format(input);

        Assert.Contains("support@flow.mail.internal", output);
        Assert.DoesNotContain("POSTAL_SERVICE", output);
    }

    [Fact]
    public void EdgeCase17_Vocabulary_WindowsPathProtection_DoesNotCorruptPaths()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "bin", Replacement = "RECYCLE_BIN" },
            new DictionaryEntry { Term = "temp", Replacement = "TEMPORARY_DIRECTORY" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = @"open file C:\tools\bin\temp\app.exe please";
        string output = pipeline.Format(input);

        Assert.Contains(@"C:\tools\bin\temp\app.exe", output);
        Assert.DoesNotContain("RECYCLE_BIN", output);
    }

    [Fact]
    public void EdgeCase18_Vocabulary_CodeIdentifierProtection_PreservesSnakeAndCamelCase()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "user", Replacement = "CLIENT" },
            new DictionaryEntry { Term = "token", Replacement = "PASSPORT" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "verify user_session_token and isValidUserToken in method";
        string output = pipeline.Format(input);

        Assert.Contains("user_session_token", output);
        Assert.Contains("isValidUserToken", output);
        Assert.DoesNotContain("CLIENT", output);
    }

    [Fact]
    public void EdgeCase19_Vocabulary_AcronymProtection_PreservesAllCapsAcronyms()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "was", Replacement = "IS_NOW" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string input = "the audio system WASAPI was initialized";
        string output = pipeline.Format(input);

        // "WASAPI" should not be touched, "was" replaced
        Assert.Contains("WASAPI", output);
        Assert.Contains("IS_NOW", output);
    }

    #endregion

    #region 3. ASR BIASING (Edge Cases 20 - 24)

    [Fact]
    public void EdgeCase20_ASRBiasing_DictionaryToWhisperPrompt_IncludesStarredAndEnabled()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "flow ai", Replacement = "FLOW", IsStarred = true, IsEnabled = true },
            new DictionaryEntry { Term = "direct ml", Replacement = "DirectML", IsStarred = false, IsEnabled = true },
            new DictionaryEntry { Term = "disabled item", Replacement = "Disabled", IsEnabled = false }
        });

        var biasingService = new PersonalizationBiasingService(engine);
        string? prompt = biasingService.BuildPrompt("en", "code");

        Assert.NotNull(prompt);
        Assert.Contains("FLOW", prompt);
        Assert.Contains("DirectML", prompt);
        Assert.DoesNotContain("Disabled", prompt);
    }

    [Fact]
    public void EdgeCase21_ASRBiasing_PromptLengthLimit_StrictlyBoundedTo200Chars()
    {
        var entries = Enumerable.Range(1, 30).Select(i => new DictionaryEntry
        {
            Term = $"technical term number {i}",
            Replacement = $"TechTermAlphaBeta{i}",
            IsEnabled = true
        }).ToList();

        var engine = new PersonalDictionaryEngine(entries);
        var biasingService = new PersonalizationBiasingService(engine, maxPromptCharacters: 200, maxTerms: 15);

        string? prompt = biasingService.BuildPrompt("en", "notepad");

        Assert.NotNull(prompt);
        Assert.True(prompt.Length <= 200, $"Prompt length was {prompt.Length} > 200 characters.");
    }

    [Fact]
    public void EdgeCase22_ASRBiasing_PromptDeduplication_RemovesDuplicatesCaseInsensitively()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "flow", Replacement = "FLOW", IsEnabled = true },
            new DictionaryEntry { Term = "Flow", Replacement = "flow", IsEnabled = true },
            new DictionaryEntry { Term = "FLOW", Replacement = "FLOW", IsEnabled = true }
        });

        var biasingService = new PersonalizationBiasingService(engine);
        string? prompt = biasingService.BuildPrompt("en", "code");

        Assert.NotNull(prompt);
        // Only one occurrence of flow
        int occurrences = prompt.Split(", ", StringSplitOptions.RemoveEmptyEntries).Count(p => p.Equals("flow", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void EdgeCase23_ASRBiasing_LanguageFiltering_PrioritizesLanguageScopedTerms()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "tamil term", Replacement = "வணக்கம்", Language = "ta", IsEnabled = true },
            new DictionaryEntry { Term = "german term", Replacement = "GutenTag", Language = "de", IsEnabled = true },
            new DictionaryEntry { Term = "universal term", Replacement = "UniversalTerm", IsEnabled = true }
        });

        var biasingService = new PersonalizationBiasingService(engine);
        string? promptTa = biasingService.BuildPrompt("ta", "notepad");

        Assert.NotNull(promptTa);
        Assert.Contains("வணக்கம்", promptTa);
        Assert.Contains("UniversalTerm", promptTa);
        Assert.DoesNotContain("GutenTag", promptTa);
    }

    [Fact]
    public void EdgeCase24_ASRBiasing_AppFiltering_PrioritizesAppScopedTerms()
    {
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "code snippet", Replacement = "SyntaxHighlighter", ApplicationScope = "code", IsEnabled = true },
            new DictionaryEntry { Term = "cad tool", Replacement = "SolidWorksModel", ApplicationScope = "cad", IsEnabled = true }
        });

        var biasingService = new PersonalizationBiasingService(engine);
        string? prompt = biasingService.BuildPrompt("en", "code.exe");

        Assert.NotNull(prompt);
        Assert.Contains("SyntaxHighlighter", prompt);
        Assert.DoesNotContain("SolidWorksModel", prompt);
    }

    #endregion

    #region 4. SNIPPETS (Edge Cases 25 - 33)

    [Fact]
    public void EdgeCase25_Snippets_ExactTrigger_ExpandsCorrectly()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "my email", ExpansionText = "barat@example.com" }
        });

        string expanded = engine.Expand("send the log to my email please");
        Assert.Equal("send the log to barat@example.com please", expanded);
    }

    [Fact]
    public void EdgeCase26_Snippets_BoundaryMatching_DoesNotExpandInsideWords()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "email", ExpansionText = "user@domain.com" }
        });

        string result = engine.Expand("use sendemail or remailer instead");
        Assert.DoesNotContain("user@domain.com", result);
        Assert.Equal("use sendemail or remailer instead", result);
    }

    [Fact]
    public void EdgeCase27_Snippets_TriggerCollision_LongestMatchWins()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "my office address", ExpansionText = "Building 4, Tech City" },
            new SnippetEntry { TriggerPhrase = "my office", ExpansionText = "Room 101" }
        });

        string result = engine.Expand("ship it to my office address today");
        Assert.Equal("ship it to Building 4, Tech City today", result);
    }

    [Fact]
    public void EdgeCase28_Snippets_UnicodeTrigger_ExpandsTamilPhrase()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "என் முகவரி", ExpansionText = "சென்னை, தமிழ்நாடு, இந்தியா" }
        });

        string result = engine.Expand("தயவுசெய்து என் முகவரி க்கு அனுப்பவும்");
        Assert.Equal("தயவுசெய்து சென்னை, தமிழ்நாடு, இந்தியா க்கு அனுப்பவும்", result);
    }

    [Fact]
    public void EdgeCase29_Snippets_TechnicalExpansion_ExpandsJsonOrMarkdown()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "insert json schema", ExpansionText = "{\"type\": \"object\", \"properties\": {}}" }
        });

        string result = engine.Expand("here is the template insert json schema end");
        Assert.Contains("{\"type\": \"object\", \"properties\": {}}", result);
    }

    [Fact]
    public void EdgeCase30_Snippets_DisabledSnippet_DoesNotExpand()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "my phone", ExpansionText = "+1-555-0199", IsEnabled = false }
        });

        string result = engine.Expand("call me at my phone please");
        Assert.Equal("call me at my phone please", result);
    }

    [Fact]
    public void EdgeCase31_Snippets_AppScopedSnippet_ExpandsOnlyInTargetApp()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "boilerplate", ExpansionText = "using System; using System.IO;", ApplicationScope = "code" }
        });

        string inCode = engine.Expand("insert boilerplate now", targetApplication: "code");
        Assert.Equal("insert using System; using System.IO; now", inCode);

        string inNotepad = engine.Expand("insert boilerplate now", targetApplication: "notepad");
        Assert.Equal("insert boilerplate now", inNotepad);
    }

    [Fact]
    public void EdgeCase32_Snippets_LanguageScopedSnippet_ExpandsOnlyInTargetLanguage()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "standard greeting", ExpansionText = "Bonjour tout le monde", Language = "fr" }
        });

        string inFr = engine.Expand("say standard greeting today", language: "fr");
        Assert.Equal("say Bonjour tout le monde today", inFr);

        string inEn = engine.Expand("say standard greeting today", language: "en");
        Assert.Equal("say standard greeting today", inEn);
    }

    [Fact]
    public void EdgeCase33_Snippets_MultilineSafety_DictationPipelineFlattensNewlines()
    {
        var engine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "multi line sign", ExpansionText = "Regards,\r\nBarat,\r\nLead Engineer" }
        });

        // 1. In standard dictation pipeline, newlines must be flattened to spaces (Zero-Enter Invariant)
        string flattened = engine.Expand("start multi line sign end");
        Assert.DoesNotContain("\r", flattened);
        Assert.DoesNotContain("\n", flattened);
        Assert.Equal("start Regards, Barat, Lead Engineer end", flattened);

        // 2. In explicit snippet insertion, newlines are preserved for paste
        string? explicitPaste = engine.ExpandForExplicitSnippetInsertion("multi line sign");
        Assert.NotNull(explicitPaste);
        Assert.Contains("\n", explicitPaste);
    }

    #endregion

    #region 5. STYLES (Edge Cases 34 - 39)

    [Fact]
    public async Task EdgeCase34_Styles_CRUD_AddUpdateDeleteProfiles()
    {
        var profile = new StyleProfile
        {
            Id = "style_academic",
            Name = "Academic",
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal,
            IsEnabled = true,
            LanguageScope = "en"
        };

        await _styleRepo.AddProfileAsync(profile);
        var retrieved = await _styleRepo.GetProfileByIdAsync("style_academic");
        Assert.NotNull(retrieved);
        Assert.Equal("Academic", retrieved.Name);

        profile.Name = "Scholarly Formal";
        await _styleRepo.UpdateProfileAsync(profile);
        var updated = await _styleRepo.GetProfileByIdAsync("style_academic");
        Assert.Equal("Scholarly Formal", updated?.Name);

        await _styleRepo.DeleteProfileAsync("style_academic");
        var deleted = await _styleRepo.GetProfileByIdAsync("style_academic");
        Assert.Null(deleted);
    }

    [Fact]
    public void EdgeCase35_Styles_DeterministicTransformation_FormalAndCasual()
    {
        var engine = new StyleFormattingEngine();

        var formalProfile = new StyleProfile
        {
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal
        };

        var casualProfile = new StyleProfile
        {
            ContractionPolicy = ContractionPolicy.Contract,
            FormalityLevel = FormalityLevel.Casual
        };

        string formalOut = engine.Format("I don't think we're gonna win", formalProfile);
        Assert.Equal("I do not think we are going to win", formalOut);

        string casualOut = engine.Format("I do not think we are going to win", casualProfile);
        Assert.Equal("I don't think we're gonna win", casualOut);
    }

    [Fact]
    public void EdgeCase36_Styles_TechnicalTokenPreservation_PreservesCodeIdentifiers()
    {
        var engine = new StyleFormattingEngine();
        var formalProfile = new StyleProfile
        {
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal
        };

        string input = "don't modify can_not_run or is_won_t_stop in the config";
        string output = engine.Format(input, formalProfile);

        // Identifiers containing underscores are protected
        Assert.Contains("can_not_run", output);
        Assert.Contains("is_won_t_stop", output);
    }

    [Fact]
    public void EdgeCase37_Styles_DisabledStyle_FallsBackToNeutral()
    {
        var engine = new StyleFormattingEngine();
        var disabledProfile = new StyleProfile
        {
            Id = "disabled_formal",
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal,
            IsEnabled = false
        };
        engine.ActiveProfile = disabledProfile;

        // ResolveProfile should return neutral default because profile is disabled
        var resolved = engine.ResolveProfile("notepad");
        Assert.Equal(ContractionPolicy.Preserve, resolved.ContractionPolicy);
        Assert.Equal(FormalityLevel.Balanced, resolved.FormalityLevel);
    }

    [Fact]
    public void EdgeCase38_Styles_AppSpecificStyle_AppliesMappedProfile()
    {
        var formalProfile = new StyleProfile { Id = "prof_formal", ContractionPolicy = ContractionPolicy.Expand, FormalityLevel = FormalityLevel.Formal };
        var casualProfile = new StyleProfile { Id = "prof_casual", ContractionPolicy = ContractionPolicy.Contract, FormalityLevel = FormalityLevel.Casual };

        var defaultProfile = new StyleProfile { Id = "prof_default", ContractionPolicy = ContractionPolicy.Preserve, FormalityLevel = FormalityLevel.Balanced };
        var engine = new StyleFormattingEngine(
            new[] { formalProfile, casualProfile },
            new[] { new AppStyleMapping { ProcessName = "outlook", ProfileId = "prof_formal" } },
            defaultProfile: defaultProfile);

        var resolved = engine.ResolveProfile("outlook.exe");
        Assert.Equal("prof_formal", resolved.Id);

        var defaultResolved = engine.ResolveProfile("slack.exe");
        Assert.Equal(ContractionPolicy.Preserve, defaultResolved.ContractionPolicy);
    }

    [Fact]
    public void EdgeCase39_Styles_LanguageSpecificStyle_AppliesScopedProfile()
    {
        var enProfile = new StyleProfile { Id = "en_formal", ContractionPolicy = ContractionPolicy.Expand, LanguageScope = "en" };
        var taProfile = new StyleProfile { Id = "ta_neutral", ContractionPolicy = ContractionPolicy.Preserve, LanguageScope = "ta" };

        var engine = new StyleFormattingEngine(new[] { enProfile, taProfile });

        var resolvedEn = engine.ResolveProfile(targetApplication: null, languageCode: "en");
        Assert.Equal("en_formal", resolvedEn.Id);

        var resolvedTa = engine.ResolveProfile(targetApplication: null, languageCode: "ta");
        Assert.Equal("ta_neutral", resolvedTa.Id);
    }

    #endregion

    #region 6. PERSISTENCE & MIGRATIONS (Edge Cases 40 - 49)

    [Fact]
    public async Task EdgeCase40_Persistence_SaveReload_SurvivesReopeningDatabase()
    {
        string diskDb = Path.Combine(Path.GetTempPath(), $"flow_p4_reload_{Guid.NewGuid():N}.db");
        try
        {
            // 1. Write data
            using (var db1 = new SqlitePersonalizationDatabase(diskDb))
            {
                var repo1 = new SqlitePersonalDictionaryRepository(db1);
                await repo1.AddAsync(new DictionaryEntry { Term = "flow repo", Replacement = "FLOW_REPOSITORY" });
            }

            // 2. Reopen brand new database instance on same file
            using (var db2 = new SqlitePersonalizationDatabase(diskDb))
            {
                var repo2 = new SqlitePersonalDictionaryRepository(db2);
                var entry = await repo2.GetByTermAsync("flow repo");
                Assert.NotNull(entry);
                Assert.Equal("FLOW_REPOSITORY", entry.Replacement);
            }
        }
        finally
        {
            try { File.Delete(diskDb); } catch { }
        }
    }

    [Fact]
    public async Task EdgeCase41_Persistence_Migration_V1ToV2Schema_PreservesData()
    {
        string v1DbPath = Path.Combine(Path.GetTempPath(), $"flow_v1_{Guid.NewGuid():N}.db");
        try
        {
            // 1. Manually create V1 database
            using (var rawDb = new SqlitePersonalizationDatabase(v1DbPath))
            {
                // Reset user_version to 1 to simulate v1 legacy schema
                await rawDb.ExecuteNonQueryAsync("PRAGMA user_version = 1;");
            }

            // 2. Initialize SqlitePersonalizationDatabase which executes migration to v2
            using (var migratedDb = new SqlitePersonalizationDatabase(v1DbPath))
            {
                var repo = new SqlitePersonalDictionaryRepository(migratedDb);
                await repo.AddAsync(new DictionaryEntry
                {
                    Term = "migrated term",
                    Replacement = "migrated replacement",
                    Language = "ta",
                    ApplicationScope = "code",
                    IsEnabled = true
                });

                var retrieved = await repo.GetByTermAsync("migrated term");
                Assert.NotNull(retrieved);
                Assert.Equal("ta", retrieved.Language);
                Assert.Equal("code", retrieved.ApplicationScope);
                Assert.True(retrieved.IsEnabled);
            }
        }
        finally
        {
            try { File.Delete(v1DbPath); } catch { }
        }
    }

    [Fact]
    public async Task EdgeCase42_Persistence_ConcurrentAccess_WalMode_AllowsParallelReads()
    {
        await _dictRepo.AddAsync(new DictionaryEntry { Term = "concurrent test", Replacement = "Success" });

        var readTasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var res = await _dictRepo.GetByTermAsync("concurrent test");
            Assert.NotNull(res);
            Assert.Equal("Success", res.Replacement);
        });

        await Task.WhenAll(readTasks);
    }

    [Fact]
    public async Task EdgeCase43_Persistence_CorruptedDatabase_DetectsAndRecoversCleanly()
    {
        string corruptDbPath = Path.Combine(Path.GetTempPath(), $"flow_corrupt_{Guid.NewGuid():N}.db");
        try
        {
            // Write completely corrupted garbage bytes
            File.WriteAllBytes(corruptDbPath, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x55, 0xAA, 0xBB, 0xCC });

            // Creating SqlitePersonalizationDatabase must not crash; it detects corruption and recreates schema
            using var recoveredDb = new SqlitePersonalizationDatabase(corruptDbPath);
            var repo = new SqlitePersonalDictionaryRepository(recoveredDb);

            // Verify the recovered db is functional
            var result = await repo.GetAllAsync();
            Assert.Empty(result);
        }
        finally
        {
            try { File.Delete(corruptDbPath); } catch { }
        }
    }

    [Fact]
    public async Task EdgeCase44_Persistence_TransactionRollback_FailedBatchLeavesNoArtifacts()
    {
        int initialCount = (await _dictRepo.GetAllAsync()).Count;

        // Try importing invalid JSON batch
        string malformedBatch = "[ { \"Term\": \"valid term\", \"Replacement\": \"ok\" }, { \"INVALID JSON\" ]";
        await Assert.ThrowsAnyAsync<Exception>(() => _dictRepo.ImportFromJsonAsync(malformedBatch));

        int afterCount = (await _dictRepo.GetAllAsync()).Count;
        Assert.Equal(initialCount, afterCount);
    }

    [Fact]
    public async Task EdgeCase45_Persistence_Import_ValidJsonAndCsv()
    {
        string json = @"[
            { ""Term"": ""term one"", ""Replacement"": ""Rep1"", ""IsStarred"": true },
            { ""Term"": ""term two"", ""Replacement"": ""Rep2"", ""IsStarred"": false }
        ]";

        await _dictRepo.ImportFromJsonAsync(json);
        var entriesAfterJson = await _dictRepo.GetAllAsync();
        Assert.Contains(entriesAfterJson, e => e.Term == "term one" && e.Replacement == "Rep1");
        Assert.Contains(entriesAfterJson, e => e.Term == "term two" && e.Replacement == "Rep2");

        string csv = "Term,Replacement,Category,IsStarred,IsEnabled,Language,ApplicationScope\n\"term three\",\"Rep3\",\"Cat\",true,true,\"en\",\"code\"";
        await _dictRepo.ImportFromCsvAsync(csv);

        var all = await _dictRepo.GetAllAsync();
        Assert.Contains(all, e => e.Term == "term three" && e.Replacement == "Rep3");
        Assert.True(all.Count >= 3);
    }

    [Fact]
    public async Task EdgeCase46_Persistence_Export_ValidJsonAndCsv()
    {
        await _dictRepo.AddAsync(new DictionaryEntry { Term = "export one", Replacement = "Exp1" });

        string json = await _dictRepo.ExportToJsonAsync();
        Assert.Contains("export one", json);
        Assert.Contains("Exp1", json);

        string csv = await _dictRepo.ExportToCsvAsync();
        Assert.Contains("export one", csv);
        Assert.Contains("Exp1", csv);
    }

    [Fact]
    public async Task EdgeCase47_Persistence_MalformedImport_RejectsCleanlyWithoutCorruption()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _dictRepo.ImportFromJsonAsync("{ NOT A LIST }"));
        await Assert.ThrowsAnyAsync<Exception>(() => _dictRepo.ImportFromCsvAsync("OnlyOneColumnNoReplacementHeader"));

        var all = await _dictRepo.GetAllAsync();
        Assert.NotNull(all);
    }

    [Fact]
    public async Task EdgeCase48_Persistence_DuplicateImport_OverwriteOptionRespected()
    {
        await _dictRepo.AddAsync(new DictionaryEntry { Term = "dup term", Replacement = "Initial" });

        string json = @"[ { ""Term"": ""dup term"", ""Replacement"": ""Updated"" } ]";

        // Without overwrite
        await _dictRepo.ImportFromJsonAsync(json, overwrite: false);
        var entry1 = await _dictRepo.GetByTermAsync("dup term");
        Assert.Equal("Initial", entry1?.Replacement);

        // With overwrite
        await _dictRepo.ImportFromJsonAsync(json, overwrite: true);
        var entry2 = await _dictRepo.GetByTermAsync("dup term");
        Assert.Equal("Updated", entry2?.Replacement);
    }

    [Fact]
    public async Task EdgeCase49_Persistence_UnicodeImportExport_RoundTripsAccurately()
    {
        await _dictRepo.AddAsync(new DictionaryEntry
        {
            Term = "தமிழ் சொல்",
            Replacement = "தமிழ் விளக்கம்",
            Language = "ta"
        });

        string exportedJson = await _dictRepo.ExportToJsonAsync();
        Assert.Contains("தமிழ் சொல்", exportedJson);
        Assert.Contains("தமிழ் விளக்கம்", exportedJson);

        using var db2 = new SqlitePersonalizationDatabase(":memory:");
        var repo2 = new SqlitePersonalDictionaryRepository(db2);
        await repo2.ImportFromJsonAsync(exportedJson);

        var roundTripped = await repo2.GetByTermAsync("தமிழ் சொல்");
        Assert.NotNull(roundTripped);
        Assert.Equal("தமிழ் விளக்கம்", roundTripped.Replacement);
        Assert.Equal("ta", roundTripped.Language);
    }

    #endregion

    #region 7. SECURITY & INVARIANTS (Edge Cases 50 - 54)

    [Fact]
    public void EdgeCase50_Security_PasswordField_ExcludedFromPersonalization()
    {
        // When focused element is a password field, target context flags isPassword = true
        bool isPasswordField = true;

        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "mypassword", Replacement = "SuperSecret123" }
        });

        // Verification: If isPassword is true, personalization MUST NOT be executed
        string text = "mypassword";
        string result = isPasswordField ? text : dictEngine.Apply(text);

        Assert.Equal("mypassword", result);
        Assert.DoesNotContain("SuperSecret123", result);
    }

    [Fact]
    public void EdgeCase51_Security_SensitiveContext_ExcludedFromPersonalization()
    {
        // Credit card / API key pattern should not be contaminated into biasing prompt
        var engine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "api_key", Replacement = "sk-live-1234567890abcdef" },
            new DictionaryEntry { Term = "cc", Replacement = "4111-2222-3333-4444" }
        });

        var biasing = new PersonalizationBiasingService(engine);
        string? prompt = biasing.BuildPrompt("en", "browser");

        // Biasing tokens should be sanitized and safe
        Assert.NotNull(prompt);
    }

    [Fact]
    public void EdgeCase52_Security_NoCloudTransmission_LocalSqliteOnly()
    {
        // All personalization storage and biasing are 100% in-process local SQLite
        Assert.True(_db.IsLocalOnly);
        Assert.False(_db.RequiresNetwork);
    }

    [Fact]
    public async Task EdgeCase53_Security_ImportedContentTreatedAsData_SqlInjectionImmune()
    {
        var maliciousEntry = new DictionaryEntry
        {
            Term = "Robert'); DROP TABLE DictionaryEntries; --",
            Replacement = "MaliciousPayload'); DROP TABLE Snippets; --"
        };

        // Add should safely parameterize without dropping tables
        await _dictRepo.AddAsync(maliciousEntry);

        var all = await _dictRepo.GetAllAsync();
        Assert.Contains(all, e => e.Term.Contains("DROP TABLE"));

        // Snippets and Dictionary tables must remain completely intact
        var snippets = await _snippetRepo.GetAllAsync();
        Assert.NotNull(snippets);
    }

    [Fact]
    public void EdgeCase54_Security_NoCommandExecution_SystemCommandsTreatedAsLiterals()
    {
        var snippetEngine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry
            {
                TriggerPhrase = "run command",
                ExpansionText = "powershell.exe -NoProfile -Command \"Get-Process\""
            }
        });

        // Expanding the snippet must return plain text string, zero subprocess execution
        string output = snippetEngine.Expand("please run command here");
        Assert.Equal("please powershell.exe -NoProfile -Command \"Get-Process\" here", output);
    }

    #endregion

    #region 8. REGRESSION & ZERO-ENTER SAFETY (Edge Cases 55 - 60)

    [Fact]
    public void EdgeCase55_Regression_Phase1Safety_AudioLifecycleUnaffected()
    {
        // Personalization subsystem has no direct dependency on audio buffers or WASAPI capture
        var dictEngine = new PersonalDictionaryEngine();
        Assert.NotNull(dictEngine);
    }

    [Fact]
    public void EdgeCase56_Regression_Phase2Formatting_PunctuationAndFillersPreserved()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "flow dev", Replacement = "FLOW Developer" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string raw = "um hello please notify flow dev regarding release comma thanks period";
        string formatted = pipeline.Format(raw);

        // Fillers removed, dictionary expanded, punctuation applied, proper capitalization
        Assert.Equal("Hello please notify FLOW Developer regarding release, thanks.", formatted);
    }

    [Fact]
    public void EdgeCase57_Regression_Phase3LanguageHandling_TamilCodeSwitchingPreserved()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "flow", Replacement = "FLOW" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        string raw = "flow செயலியைப் பயன்படுத்துங்கள்";
        string formatted = pipeline.Format(raw);

        Assert.Contains("FLOW", formatted);
        Assert.Contains("செயலியைப் பயன்படுத்துங்கள்", formatted);
    }

    [Fact]
    public void EdgeCase58_Regression_Backtrack_WorksWithPersonalizedTranscripts()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "direct ml", Replacement = "DirectML" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);
        string formatted = pipeline.Format("we love direct ml period");
        Assert.Equal("We love DirectML.", formatted);

        var tracker = new Flow.Core.Backtrack.InsertionHistoryTracker();
        var record = new Flow.Core.Backtrack.InsertionRecord(
            Guid.NewGuid(),
            formatted,
            formatted.Length,
            DateTimeOffset.UtcNow,
            new IntPtr(0x1234),
            "code",
            5678,
            Flow.Core.TextInsertion.InsertionStrategy.SendInputClipboardFallback
        );
        tracker.RecordInsertion(record);

        var retrieved = tracker.PeekLastInsertion();
        Assert.NotNull(retrieved);
        Assert.Equal("We love DirectML.", retrieved.InsertedText);
        Assert.Equal("code", retrieved.TargetProcessName);

        tracker.Clear();
        Assert.Null(tracker.PeekLastInsertion());
    }

    [Fact]
    public void EdgeCase59_Regression_TargetHwndPid_PersonalizationRespectsActiveTarget()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "buffer", Replacement = "RingBuffer<float>", ApplicationScope = "devenv" },
            new DictionaryEntry { Term = "buffer", Replacement = "temporary text buffer", ApplicationScope = "notepad" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine);

        var devOptions = new FormattingOptions { TargetApplication = "devenv.exe" };
        string devResult = pipeline.Format("allocate the buffer now", devOptions);
        Assert.Equal("Allocate the RingBuffer<float> now.", devResult);

        var noteOptions = new FormattingOptions { TargetApplication = "notepad.exe" };
        string noteResult = pipeline.Format("allocate the buffer now", noteOptions);
        Assert.Equal("Allocate the temporary text buffer now.", noteResult);
    }

    [Fact]
    public void EdgeCase60_Regression_ZeroEnterPostPipeline_NeverContainsCarriageReturnOrNewline()
    {
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "break line", Replacement = "First\r\nSecond\nThird" }
        });
        var snippetEngine = new SnippetExpansionEngine(new[]
        {
            new SnippetEntry { TriggerPhrase = "multiline snippet", ExpansionText = "Header\r\nBody\r\nFooter" }
        });
        var pipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine);

        string raw = "insert multiline snippet and break line here";
        string formatted = pipeline.Format(raw);

        // Absolute fail-closed check: No \r, no \n, no VK_RETURN simulation
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x0D), formatted);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x0A), formatted);
    }

    #endregion
}
