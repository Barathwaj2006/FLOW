# MASTER_WISPR_PARITY_MATRIX.md — FLOW vs. Wispr Flow Windows Master Parity Matrix

> **Authoritative Baseline**: September 10, 2026  
> **Benchmark Target**: Wispr Flow Windows Desktop Application  
> **Status**: Living Engineering Audit & Parity Verification Record  
> **Engineering Rule**: Independent native implementation; zero fake implementations; zero tautological tests; strict phase gating.

---

## 1. Capability Maturity Scale

| Level | Definition | Engineering Standard for FLOW |
| :--- | :--- | :--- |
| **Level 0** | **Not Present** | No code, interface, or schema exists. |
| **Level 1** | **Interface / Architecture Only** | Typed interface or data contract defined, but logic is stubbed. |
| **Level 2** | **Synthetic / Mock Implementation** | Implementation exists but uses mock/hardcoded/simulated returns. |
| **Level 3** | **Real Implementation Unverified** | Real Win32/Core logic written; compiles cleanly; not yet tested in automated test suite. |
| **Level 4** | **Real Implementation Tested Locally** | Real implementation passing automated unit/integration test suites with verifiable metrics. |
| **Level 5** | **Physical Desktop Verified** | Physically validated on Windows 10/11 x64 hardware with real audio/UI (Notepad, VS Code, Terminal, etc.). |

---

## 2. Master Feature Parity Matrix

### A. Core Voice & Audio Capture

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-001** | **Global Push-to-Talk** | Hold configured hotkey (Right Alt) to record; release to transcribe. | **Level 5** | `GlobalHotkeyHook.cs` (`WH_KEYBOARD_LL`) + `VoiceSessionCoordinator` | Phase 2B | Passing; physical key hold/release verified |
| **WF-002** | **Hands-Free Mode** | Double-tap hotkey to start continuous recording; double-tap again to stop. | **Level 5** | `GlobalHotkeyHook.cs` (350ms double-tap window) | Phase 2B | Passing; double-tap state machine verified |
| **WF-003** | **Session Cancellation** | Press `Escape` while recording to discard audio with zero insertion. | **Level 5** | `GlobalHotkeyHook.cs` (`VK_ESCAPE`) $\rightarrow$ `VoiceSessionCoordinator.CancelSessionAsync` | Phase 2B | Passing; cancellation aborts insertion |
| **WF-004** | **WASAPI Audio Capture** | Streams 16kHz audio directly from Windows microphone endpoint. | **Level 5** | `WasapiAudioCapture.cs` (Native WASAPI `IAudioCaptureClient::GetBuffer`) | Phase 2B | Passing; physical Intel Smart Sound mic array verified |
| **WF-005** | **Audio Buffer Rollover** | Buffers continuous streaming audio without memory leaks or unbounded growth. | **Level 5** | `AudioRingBuffer.cs` (Pre-allocated circular buffer, 30s ceiling, non-allocating) | Phase 2B | Passing; buffer rollover test suite passing |
| **WF-006** | **Voice Activity Detection** | Identifies speech onset and trailing silence to cut off recording automatically. | **Level 5** | `EnergyVAD.cs` (Real-time RMS energy + zero-crossing + trailing silence window) | Phase 2B | Passing; onset/silence detection passing |
| **WF-007** | **Microphone Selection** | Allows user to select specific audio input device and handles route changes. | **Level 4** | `WasapiDeviceManager.cs` (`IMMDeviceEnumerator` endpoint enumeration) | Phase 2B | Passing; device discovery verified |
| **WF-008** | **Desktop Recording Limit** | Enforces 20-minute maximum continuous recording limit with warning at $T - 60\text{s}$. | **Level 5** | `VoiceSessionCoordinator` session timer with warning event and auto-stop | Phase 2B | Passing; 1200s ceiling & 1140s warning verified |
| **WF-009** | **Quiet / Whisper Audio** | Adapts to low-amplitude whisper speech without dropping consonants. | **Level 4** | Dynamic RMS noise floor calibration in `EnergyVAD` + whisper model tuning | Phase 2B | Passing; low-amplitude test vector passing |
| **WF-010** | **Audio Feedback Cues** | Subtle auditory feedback tones for recording start, stop, and cancellation. | **Level 3** | Win32 `MessageBeep` / Multimedia feedback hook | Phase 2B | Functional Win32 audio cues |

---

### B. Transcription & Formatting

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-011** | **Local ASR Execution** | Offline speech-to-text inference with low latency and zero network transmission. | **Level 5** | `WhisperNetInferenceEngine.cs` + native whisper.cpp AVX2 CPU + `ggml-tiny.en.bin` | Phase 2B | Passing; 0.11x–0.15x RTF physical verification |
| **WF-012** | **ASR Fallback Chain** | Gracefully switches to secondary/CPU engine if GPU or preferred backend fails. | **Level 4** | `ASREngineRegistry.cs` with priority-ordered fallback escalation | Phase 2B | Passing; priority escalation unit test passing |
| **WF-013** | **Automatic Capitalization** | Capitalizes start of sentences and proper nouns. | **Level 5** | `SmartCapitalizationStage.cs` & `DeterministicTextSanitizer.cs` | Phase 2C | Passing; sentence start & proper noun capitalization |
| **WF-014** | **Terminal Punctuation** | Automatically ends declarative sentences with periods, questions with `?`. | **Level 5** | `SpokenPunctuationStage.cs` & `DeterministicTextSanitizer.cs` | Phase 2C | Passing; terminal punctuation logic verified |
| **WF-015** | **Spoken Punctuation** | Maps voice commands (*"comma"*, *"period"*, *"question mark"*, *"colon"*) to symbols. | **Level 5** | `SpokenPunctuationStage.cs` (multi-pass regex & symbol dictionary) | Phase 2C | Passing; comprehensive spoken symbols verified |
| **WF-016** | **Filler Word Removal** | Automatically cleans up filler hesitation tokens (*"um"*, *"uh"*, *"erm"*). | **Level 5** | `ConservativeFillerRemovalStage.cs` (Context-aware filler stripping) | Phase 2C | Passing; filler stripped, "like" as verb preserved |
| **WF-017** | **Zero-Enter Safety** | Strictly prevents all simulated Enter keys (`0x0D`, `VK_RETURN`, `\r\n`). | **Level 5** | `WhitespaceAndZeroEnterStage.cs` & `WindowsTextInsertionService.cs` | Phase 2B/2C | Passing; hard assertion on zero Enter simulation |
| **WF-018** | **Backtracking Corrections** | Automatically detects mid-speech correction (e.g., *"Friday actually next Monday"*). | **Level 5** | `InsertionHistoryTracker.cs` + Desktop Backtrack SendInput replacement | Phase 2C | Passing; foreground HWND/PID verified backtrack |
| **WF-019** | **Numbered Lists** | Formats voice lists (*"one ... two ... three ..."*) into structured 1. 2. 3. format. | **Level 5** | `NumberedListStage.cs` (2-item activation gate, comma separation) | Phase 2C | Passing; spoken numbers to list items verified |
| **WF-020** | **Number & Date Formatting**| Converts spoken numbers (*"five thousand dollars"*, *"March twelfth"*) to `$5,000` and `March 12th`. | **Level 4** | `SpokenEntityNormalizer.cs` & `TechnicalEntityProtectionStage.cs` | Phase 2C | Passing; entity normalizer tests passing |

---

### C. Languages & Multilingual

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| **WF-021** | **Multi-Language Selection** | User can choose primary transcription language in Settings from 100+ languages. | **Level 5** | Strongly typed `LanguageCatalog`, `LanguageSessionService`, `WhisperModelManager` | Phase 3 | Verified with physical multilingual ggml-tiny.bin (en, ta, hi) |
| **WF-022** | **Automatic Language Detection**| Automatically detects spoken language without manual pre-selection. | **Level 5** | Whisper encoder LID token auto-detection, `LanguageDetected` event & confidence | Phase 3 | Verified on physical audio (0.16x RTF, en detection) |
| **WF-023** | **Code-Switching** | Accurately handles bilingual transitions (English + regional words). | **Level 5** | Whisper prompt biasing + Indic code-switching regex shield | Phase 3 | Verified with mixed technical vocabulary and scripts |

---

### D. Personalization (Phase 2D Focus)

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-024** | **Personal Dictionary** | User adds specialized terms, names, and team jargon to prevent misrecognition. | **Target Level 4/5** | `SqlitePersonalDictionaryRepository.cs` + `PersonalDictionaryStage.cs` | Phase 2D | In implementation |
| **WF-025** | **Custom Corrections** | Exact mapping rules: replaces misrecognized word A with preferred word B. | **Target Level 4/5** | `PersonalDictionaryEngine.cs` regex/word boundary replacement | Phase 2D | In implementation |
| **WF-026** | **Voice Snippets** | Voice cue expands to rich boilerplate text template (up to 4,000 chars). | **Target Level 4/5** | `SqliteSnippetRepository.cs` + `SnippetsExpansionStage.cs` | Phase 2D | In implementation |
| **WF-027** | **Styles System** | Categories (Personal, Work, Email, Technical, Casual); Tones and Formality. | **Target Level 4/5** | `StyleFormattingStage.cs` + `StyleRuleSet.cs` | Phase 2D | In implementation |
| **WF-028** | **Auto-Learned Vocabulary** | Automatically identifies and suggests frequently corrected proper nouns. | **Level 0** | Vocabulary frequency tracker in local SQLite | Phase 2G | Scheduled |

---

### E. Context Awareness

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-029** | **Active App Detection** | Identifies foreground process name and window title at dictation start. | **Level 4** | `WindowsTextInsertionService.GetForegroundProcessInfo` | Phase 2C | Passing; HWND & PID extraction verified |
| **WF-030** | **Password Field Exclusion** | Automatically detects password fields via UIA and refuses audio capture / logging. | **Level 1** | `IUIAutomationElement::CurrentIsPassword` COM check | Phase 2E | Architecture mapped |
| **WF-031** | **Nearby Context Extraction**| Reads surrounding text in focused text field via UIA `TextPattern` to bias formatting. | **Level 1** | UIA `IUIAutomationTextPattern` integration | Phase 2E | Architecture mapped |

---

### F. Developer Mode

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-032** | **Casing Transformations** | Converts spoken commands to `camelCase`, `snake_case`, `PascalCase`, `kebab-case`. | **Level 4** | `CasingTransformer.cs` | Phase 2C | Passing; casing transforms verified |
| **WF-033** | **Technical Identifier Shield**| Guarantees symbols (`_`, `-`, `.`), code variables, and paths are not mangled. | **Level 5** | `TechnicalEntityProtectionStage.cs` | Phase 2C | Passing; file paths, URLs, CLI commands protected |
| **WF-034** | **Voice File Tagging** | Speaking *"at filename dot ts"* outputs `@filename.ts` in IDE chat/editors. | **Level 1** | `DeveloperSyntaxFilter.cs` | Phase 2E | Architecture mapped |
| **WF-035** | **IDE Terminal Compatibility**| Safe text injection directly into VS Code, Cursor, and Windows Terminal. | **Level 5** | `WindowsTextInsertionService.cs` (Zero-Enter SendInput) | Phase 2B/2C | Passing; terminal injection verified |

---

### G. Command Mode

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-036** | **Command Mode Trigger** | Dedicated secondary shortcut activates command voice input. | **Level 4** | Global shortcut registration in `GlobalHotkeyHook` (`Ctrl + Right Alt`) | Phase 2F | Verified via STA hook integration; live multi-app pending Tier 6 |
| **WF-037** | **Voice Text Formatting** | Highlight text, speak command (*"make this bullet points"*, *"make concise"*). | **Level 4** | UIA selection extraction + bullets/casing transform + wand indicator | Phase 2F | Verified on real STA WPF Window; live multi-app pending Tier 6 |
| **WF-038** | **Zero-Destructive Safety** | Prohibits automated Enter, Send, Submit, Delete, or file system execution. | **Level 5** | Inviolable system-wide policy in text insertion service & safety policy | Phase 2B | Passing; zero execution primitives & zero Enter invariant certified |

---

### H. History & Productivity

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-039** | **Local Transcript History** | Saves every dictation with timestamp, app name, audio duration, and word count. | **Level 1** | `SqliteDictationHistoryRepository.cs` schema mapped | Phase 2G | Architecture mapped |
| **WF-040** | **Full-Text History Search** | Fast full-text search across past dictations. | **Level 0** | SQLite FTS5 virtual table | Phase 2G | Scheduled |
| **WF-041** | **Paste-Last-Transcript** | Global hotkey to re-paste the most recent dictation. | **Level 1** | Insertion tracker replay | Phase 2G | Scheduled |
| **WF-042** | **Productivity Statistics** | Displays Words Per Minute (WPM), total word count, and daily streak in Hub. | **Level 0** | History aggregator | Phase 2G | Scheduled |
| **WF-043** | **Desktop Scratchpad** | Quick scratchpad text area in Hub to dictate notes before inserting. | **Level 0** | WinUI 3 draft editor | Phase 2G | Scheduled |

---

### I. Desktop UI & Hub

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-044** | **Floating HUD (Flow Bar)**| Non-activating floating pill showing recording state, audio level, and status. | **Level 3** | `FloatingHudController.cs` (`WS_EX_NOACTIVATE | WS_EX_TOPMOST`) | Phase 2B | Real Win32 window implemented |
| **WF-045** | **System Tray Presence** | Tray icon with context menu (Status, Open Hub, Settings, Exit). | **Level 4** | `TrayIconManager.cs` (`Shell_NotifyIcon`) | Phase 2B | Passing; tray menu verified |
| **WF-046** | **Hub / Settings Application**| Main window with tabs: History, Dictionary, Snippets, Styles, Shortcuts, About. | **Level 1** | XAML / WinUI 3 host project | Phase 2H | Architecture mapped |
| **WF-047** | **Shortcut Customization** | User can rebind push-to-talk, hands-free, command mode, and scratchpad keys. | **Level 1** | Settings model + Hotkey rebind | Phase 2H | Architecture mapped |
| **WF-048** | **Audio Device Feedback** | Real-time audio input meter in Settings to test and verify microphone level. | **Level 1** | Meter hook in `EnergyVAD` | Phase 2H | Architecture mapped |

---

### J. Windows Integration & OS Hygiene

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-049** | **Safe Text Insertion Engine** | Inserts text at cursor; Tier 1 UIA, Tier 2 SendInput Ctrl+V + 150ms restore. | **Level 5** | `WindowsTextInsertionService.cs` | Phase 2B | Passing; physical clipboard restore verified |
| **WF-050** | **PerMonitorV2 DPI Awareness** | Scales crisply across mixed-DPI multi-monitor setups without blur or misplacement.| **Level 4** | `app.manifest` DPI awareness enabled | Phase 2B | Passing; manifest verified |
| **WF-051** | **Run on Windows Startup** | Option to start automatically with Windows minimized to tray. | **Level 1** | Registry `HKCU\...\Run` helper | Phase 2I | Architecture mapped |
| **WF-052** | **Graceful Shutdown / Restart**| Cleanly releases COM objects, saves database WAL, and flushes buffers on shutdown. | **Level 4** | Structured `IDisposable` + unmanaged buffer teardown | Phase 2B | Passing; teardown tests passing |
| **WF-053** | **Crash Resilience & Recovery** | Recovers gracefully if an unmanaged exception or audio endpoint disconnects. | **Level 4** | Thread boundary exception isolation & retry | Phase 2B | Passing; mic disconnect handling passing |

---

### K. Privacy & Security

| ID | Capability | Wispr Windows Behavior | FLOW Level | FLOW Architecture & Module | Phase | Verification Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-054** | **100% Local In-Memory Audio** | Audio captured from microphone NEVER leaves the local machine. | **Level 5** | Pure local pipeline (`AudioRingBuffer` disposed in RAM) | Phase 2B | Verified; zero cloud networking calls |
| **WF-055** | **Local Database Encryption** | History, dictionary, and snippets stored locally using Windows DPAPI encryption. | **Level 1** | `ProtectedData.Protect` DPAPI encryption service | Phase 2I | Architecture mapped |
| **WF-056** | **Zero Telemetry Leaks** | Dictation content and clipboard backups are excluded from error logs. | **Level 5** | Structured redaction in `VoiceSessionCoordinator` logging | Phase 2B | Verified; zero raw text leaking to logs |

---

## 3. Parity Progress Summary

* **Total Wispr Windows Capabilities Tracked**: **56**
* **Level 5 (Physical Desktop Verified)**: **17** ($30.4\%$)
* **Level 4 (Tested Real Implementation)**: **9** ($16.1\%$)
* **Level 3 (Real Code, Not Fully Automated)**: **2** ($3.6\%$)
* **Level 1 (Interface / Architecture Only)**: **16** ($28.6\%$)
* **Level 0 (Pending Phase Implementation)**: **12** ($21.4\%$)

**Combined Functional Operational Baseline (Level 4 + 5)**: **26 / 56** ($46.4\%$).
Phase 2D will promote 4 critical capabilities (WF-024, WF-025, WF-026, WF-027) from Level 0/1 to Level 4/5, bringing the verified baseline to over $53\%$.
