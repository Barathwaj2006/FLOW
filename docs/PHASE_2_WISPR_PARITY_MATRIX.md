# PHASE_2_WISPR_PARITY_MATRIX.md — FLOW vs. Wispr Flow Windows Parity Matrix

> **Baseline Snapshot**: September 10, 2026  
> **Benchmark Target**: Wispr Flow Windows Desktop Application  
> **Engineering Rule**: Independent native implementation; no fake features; strict phase gating.

---

## 1. Capability Maturity Scale

| Level | Definition | Standard for FLOW |
| :--- | :--- | :--- |
| **Level 0** | **Not Present** | No code, interface, or schema exists. |
| **Level 1** | **Interface / Architecture Only** | Typed interface or data contract defined, but logic is stubbed. |
| **Level 2** | **Synthetic / Mock Implementation** | Implementation exists but uses mock/hardcoded/simulated returns. |
| **Level 3** | **Real Implementation Unverified** | Real Win32/Core logic written; compiles cleanly; not yet tested in automated test suite. |
| **Level 4** | **Real Implementation Tested Locally** | Real implementation passing automated unit/integration test suites with verifiable metrics. |
| **Level 5** | **End-to-End Cross-App Verified** | Physically validated across target Windows desktop applications (Notepad, VS Code, Word, Chrome, Terminal). |

---

## 2. Master Feature Parity Matrix

### A. Core Voice & Audio Capture

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-001** | **Global Push-to-Talk** | Hold configured hotkey (e.g. Right Alt) to record; release to transcribe. | **Level 4** | `GlobalHotkeyHook.cs` (`WH_KEYBOARD_LL`) triggering `VoiceSessionCoordinator` | **P0** | Win32 Hook | Unit test + hook event test |
| **WF-002** | **Hands-Free Mode** | Double-tap hotkey to start continuous recording; double-tap again to stop. | **Level 0** | Extend `GlobalHotkeyHook` with 300ms double-tap state machine | **P0** | `WF-001` | Rapid keypress interval test |
| **WF-003** | **Session Cancellation** | Press `Escape` while recording to discard audio with zero insertion. | **Level 4** | `GlobalHotkeyHook.cs` listening for `VK_ESCAPE` $\rightarrow$ `VoiceSessionCoordinator.CancelSessionAsync` | **P0** | `WF-001` | Session cancellation unit test |
| **WF-004** | **WASAPI Audio Capture** | Streams 16kHz audio directly from Windows microphone endpoint. | **Level 2** | `WasapiAudioCapture.cs` (Thread loops; needs native `IAudioCaptureClient::GetBuffer` wiring) | **P0** | Windows Core Audio | Physical mic loopback test |
| **WF-005** | **Audio Buffer Rollover** | Buffers continuous streaming audio without memory leaks or unbounded growth. | **Level 4** | `AudioRingBuffer.cs` (Pre-allocated circular buffer, 30s ceiling, non-allocating) | **P0** | None | Capacity & overflow unit tests |
| **WF-006** | **Voice Activity Detection** | Identifies speech onset and trailing silence to cut off recording automatically. | **Level 4** | `EnergyVAD.cs` (Real-time RMS energy + zero-crossing + trailing silence window) | **P0** | `WF-005` | VAD onset & silence unit tests |
| **WF-007** | **Microphone Selection** | Allows user to select specific audio input device and handles route changes. | **Level 0** | `WasapiDeviceManager.cs` using `IMMDeviceEnumerator` to list and hot-swap capture endpoints | **P1** | `WF-004` | Device unplug/reconnect test |
| **WF-008** | **Desktop Recording Limit** | Enforces 20-minute maximum continuous recording limit with warning at $T - 60\text{s}$. | **Level 0** | `VoiceSessionCoordinator` session timer with warning event and auto-stop at 20m | **P1** | `WF-005` | Long-duration timeout test |
| **WF-009** | **Quiet / Whisper Audio** | Adapts to low-amplitude whisper speech without dropping consonants. | **Level 1** | Dynamic RMS noise floor calibration in `EnergyVAD` + whisper model tuning | **P2** | `WF-006` | Low-amplitude audio test vector |
| **WF-010** | **Audio Feedback Cues** | Subtle auditory feedback tones for recording start, stop, and cancellation. | **Level 0** | DirectSound / Win32 `PlaySound` cues (can be disabled in Settings) | **P2** | Windows Multimedia | Audio playback test |

---

### B. Transcription & Formatting

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-011** | **Local ASR Execution** | Offline speech-to-text inference with low latency and zero network transmission. | **Level 2** | `LocalWhisperEngine.cs` & `DirectMlWhisperInference.cs` (Need ONNX Runtime session initialization & weight loading) | **P0** | ONNX Runtime / DirectML | End-to-end inference test |
| **WF-012** | **ASR Fallback Chain** | Gracefully switches to secondary/CPU engine if GPU or preferred backend fails. | **Level 4** | `ASREngineRegistry.cs` with priority-ordered fallback escalation | **P0** | `WF-011` | Simulated failure fallback test |
| **WF-013** | **Automatic Capitalization** | Capitalizes start of sentences and proper nouns. | **Level 4** | `DeterministicTextSanitizer.cs` & `CasingTransformer.cs` | **P0** | None | Grammar unit test |
| **WF-014** | **Terminal Punctuation** | Automatically ends declarative sentences with periods, questions with `?`. | **Level 4** | `DeterministicTextSanitizer.cs` (Validates and appends terminal period) | **P0** | None | Punctuation unit test |
| **WF-015** | **Spoken Punctuation** | Maps voice commands (*"comma"*, *"period"*, *"question mark"*, *"colon"*) to symbols. | **Level 4** | `DeterministicTextSanitizer.cs` regex map | **P0** | None | Spoken command unit test |
| **WF-016** | **Filler Word Removal** | Automatically cleans up filler hesitation tokens (*"um"*, *"uh"*, *"erm"*). | **Level 4** | `DeterministicTextSanitizer.cs` filler suppressor regex | **P0** | None | Filler removal unit test |
| **WF-017** | **Zero-Enter Safety** | Strictly prevents all simulated Enter keys (`0x0D`, `VK_RETURN`, `\r\n`). | **Level 4** | Dual-tier filter in `DeterministicTextSanitizer` and `WindowsTextInsertionService` | **P0** | Insertion | Keycode blacklist assertion test |
| **WF-018** | **Backtracking Corrections** | Automatically detects mid-speech correction (e.g., *"Friday actually next Monday"*). | **Level 0** | `BacktrackingResolver.cs` analyzing marker words (*"actually"*, *"I mean"*, *"scratch that"*) | **P1** | `WF-013` | Backtrack corpus test |
| **WF-019** | **Numbered Lists** | Formats voice lists (*"one ... two ... three ..."*) into structured 1. 2. 3. format. | **Level 0** | `ListFormattingEngine.cs` regex pattern matcher | **P2** | `WF-013` | List transformation test |
| **WF-020** | **Number & Date Formatting**| Converts spoken numbers (*"five thousand dollars"*, *"March twelfth"*) to `$5,000` and `March 12th`. | **Level 1** | `SpokenEntityNormalizer.cs` | **P1** | `WF-013` | Number & currency test suite |

---

### C. Languages & Multilingual

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-021** | **Multi-Language Selection** | User can choose primary transcription language in Settings from 100+ languages. | **Level 1** | `ASROptions.Language` parameter passed to `IASREngine` | **P1** | `WF-011` | Multi-language audio test |
| **WF-022** | **Automatic Language Detection**| Automatically detects spoken language without manual pre-selection. | **Level 0** | Whisper encoder first-chunk Language Identification (LID) token extraction | **P2** | `WF-011` | Multilingual audio test |
| **WF-023** | **Code-Switching** | Accurately handles bilingual transitions (e.g. English + regional words). | **Level 0** | Vocabulary adaptation via Whisper prompt biasing (`ASROptions.Prompt`) | **P2** | `WF-021` | Code-switched audio test |

---

### D. Personalization

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-024** | **Personal Dictionary** | User adds specialized terms, names, and team jargon to prevent misrecognition. | **Level 0** | SQLite table `UserDictionary` injected as initial prompt tokens into Whisper | **P0** | Storage | Word substitution test |
| **WF-025** | **Custom Corrections** | Exact mapping rules: replaces misrecognized word A with preferred word B. | **Level 0** | SQLite table `TextCorrections` evaluated deterministically in `DeterministicTextSanitizer` | **P1** | Storage | Exact replacement test |
| **WF-026** | **Voice Snippets** | Voice cue (up to 60 chars) expands to rich text snippet (up to 4,000 chars). | **Level 0** | `SnippetEngine.cs` checking spoken transcript against `Snippets` table | **P0** | Storage | Snippet expansion test |
| **WF-027** | **Styles System** | Categories (Personal, Work, Email, Other); Tones (Formal, Casual, Excited). | **Level 1** | `FormattingOptions` mapped to active application profiles | **P1** | `WF-013` | Style formatting test |
| **WF-028** | **Auto-Learned Vocabulary** | Automatically identifies and suggests frequently corrected proper nouns. | **Level 0** | Vocabulary frequency tracker in local SQLite | **P3** | `WF-024` | Learning persistence test |

---

### E. Context Awareness

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-029** | **Active App Detection** | Identifies foreground process name and window title at dictation start. | **Level 3** | `WindowsTextInsertionService.GetProcessNameFromHwnd` | **P0** | Win32 API | Active process lookup test |
| **WF-030** | **Password Field Exclusion** | Automatically detects password fields via UIA and refuses audio capture / logging. | **Level 0** | `IUIAutomationElement::CurrentIsPassword` check before recording or insertion | **P0** | UIA COM | Password field exclusion test |
| **WF-031** | **Nearby Context Extraction**| Reads surrounding text in focused text field via UIA `TextPattern` to bias formatting. | **Level 1** | UIA `IUIAutomationTextPattern` / `TextPattern2` integration | **P2** | UIA COM | Surrounding text read test |

---

### F. Developer Mode

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-032** | **Casing Transformations** | Converts spoken commands to `camelCase`, `snake_case`, `PascalCase`, `kebab-case`. | **Level 1** | `CasingTransformer.cs` (TitleCase & SentenceCase exist; add camel/snake/pascal) | **P0** | None | Casing transform unit tests |
| **WF-033** | **Technical Identifier Shield**| Guarantees symbols (`_`, `-`, `.`), code variables, and paths are not mangled. | **Level 4** | Protected token patterns in `DeterministicTextSanitizer.cs` | **P0** | None | Content Lock unit test |
| **WF-034** | **Voice File Tagging** | Speaking *"at filename dot ts"* outputs `@filename.ts` in IDE chat/editors. | **Level 0** | `DeveloperSyntaxFilter.cs` regex map for `@filename` and file extensions | **P1** | `WF-032` | File tagging test |
| **WF-035** | **IDE Terminal Compatibility**| Safe text injection directly into VS Code, Cursor, and Windows Terminal. | **Level 4** | SendInput `Ctrl+V` with clipboard restore; strictly no `Enter` simulation | **P0** | Insertion | Terminal injection test |

---

### G. Command Mode

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-036** | **Command Mode Trigger** | Dedicated secondary shortcut activates command voice input. | **Level 0** | Second hotkey binding in `GlobalHotkeyHook` dispatching to `CommandCoordinator` | **P1** | `WF-001` | Command hotkey test |
| **WF-037** | **Voice Text Formatting** | Highlight text, speak command (*"make this bullet points"*, *"make concise"*). | **Level 0** | Reads selection via UIA/clipboard, applies deterministic transforms, pastes back | **P1** | `WF-029` | Selection transform test |
| **WF-038** | **Zero-Destructive Command Safety** | Prohibits automated Enter, Send, Submit, Delete, or file system execution. | **Level 4** | Global command safety filter in `WindowsTextInsertionService` | **P0** | `WF-017` | Safety rejection assertion |

---

### H. History & Productivity

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-039** | **Local Transcript History** | Saves every dictation with timestamp, app name, audio duration, and word count. | **Level 0** | SQLite `DictationHistory` table via `Microsoft.Data.Sqlite` | **P0** | Storage | CRUD database tests |
| **WF-040** | **Full-Text History Search** | Fast full-text search across past dictations. | **Level 0** | SQLite FTS5 virtual table indexing transcript text | **P1** | `WF-039` | FTS search test |
| **WF-041** | **Paste-Last-Transcript** | Global hotkey to re-paste the most recent dictation. | **Level 0** | Hotkey binding calling `InsertTextAsync` with latest history record | **P1** | `WF-039` | Re-paste test |
| **WF-042** | **Productivity Statistics** | Displays Words Per Minute (WPM), total word count, and daily streak in Hub. | **Level 0** | Metric aggregator over `DictationHistory` records | **P2** | `WF-039` | Calculation unit test |
| **WF-043** | **Desktop Scratchpad** | Quick scratchpad text area in Hub to dictate notes before inserting. | **Level 0** | Lightweight XAML text view bound to local SQLite drafts | **P2** | Hub | Scratchpad edit test |

---

### I. Desktop UI & Hub

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-044** | **Floating HUD (Flow Bar)**| Non-activating floating pill showing recording state, audio level, and status. | **Level 3** | `FloatingHudController.cs` (`WS_EX_NOACTIVATE | WS_EX_TOPMOST`) | **P0** | Win32 | Focus preservation test |
| **WF-045** | **System Tray Presence** | Tray icon with context menu (Status, Open Hub, Settings, Exit). | **Level 3** | `TrayIconManager.cs` (`Shell_NotifyIcon`) | **P0** | Win32 | Tray click & menu test |
| **WF-046** | **Hub / Settings Application**| Main window with tabs: History, Dictionary, Snippets, Styles, Shortcuts, About. | **Level 0** | WinUI 3 / XAML Desktop Window (anti-vibecode Segoe UI design) | **P1** | WinUI 3 | Window launch & nav test |
| **WF-047** | **Shortcut Customization** | User can rebind push-to-talk, hands-free, command mode, and scratchpad keys. | **Level 1** | Hotkey configuration stored in local `settings.json` and read by `GlobalHotkeyHook` | **P1** | `WF-001` | Hotkey rebind test |
| **WF-048** | **Audio Device Feedback** | Real-time audio input meter in Settings to test and verify microphone level. | **Level 0** | Visual VU-meter bound to `EnergyVAD.RmsEnergy` | **P2** | `WF-004` | Mic level meter test |

---

### J. Windows Integration & OS Hygiene

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-049** | **Safe Text Insertion Engine** | Inserts text at cursor; Tier 1 UIA, Tier 2 SendInput Ctrl+V + 150ms restore. | **Level 4** | `WindowsTextInsertionService.cs` (Tested locally with Win32 clipboard restore) | **P0** | Win32 API | Clipboard restore test |
| **WF-050** | **PerMonitorV2 DPI Awareness** | Scales crisply across mixed-DPI multi-monitor setups without blur or misplacement.| **Level 3** | `app.manifest` DPI awareness enabled | **P1** | Win32 | Multi-DPI placement test |
| **WF-051** | **Run on Windows Startup** | Option to start automatically with Windows minimized to tray. | **Level 0** | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` manager | **P1** | Win32 Registry | Startup registry test |
| **WF-052** | **Graceful Shutdown / Restart**| Cleanly releases COM objects, saves database WAL, and flushes buffers on shutdown. | **Level 3** | `IDisposable` pattern on all singletons + `WM_QUERYENDSESSION` handler | **P0** | OS Events | Shutdown handler test |
| **WF-053** | **Crash Resilience & Recovery** | Recovers gracefully if an unmanaged exception or audio endpoint disconnects. | **Level 3** | Structured try/catch and thread boundary error handling in coordinators | **P0** | Core | Fault injection test |

---

### K. Privacy & Security

| ID | Capability | Wispr Windows Behavior | FLOW Status | FLOW Architecture | Priority | Dependency | Verification Method |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-054** | **100% Local In-Memory Audio** | Audio captured from microphone NEVER leaves the local machine. | **Level 4** | In-memory `AudioRingBuffer` disposed after transcription; zero network calls | **P0** | Core | Network interface down test |
| **WF-055** | **Local Database Encryption** | History, dictionary, and snippets stored locally using Windows DPAPI encryption. | **Level 0** | `ProtectedData.Protect` for sensitive database fields / config | **P1** | DPAPI | DPAPI roundtrip test |
| **WF-056** | **Zero Telemetry Leaks** | Dictation content and clipboard backups are excluded from error logs. | **Level 4** | Structured logging in `VoiceSessionCoordinator` redacts raw text payloads | **P0** | Logging | Log inspection test |

---

## 3. Summary Statistics of Current FLOW Coverage

* **Total Verified Wispr Windows Capabilities**: **56**
* **FLOW Level 4 (Tested Real Implementation)**: **17** ($30.4\%$)
* **FLOW Level 3 (Real Code, Not Yet Fully Tested)**: **7** ($12.5\%$)
* **FLOW Level 2 (Synthetic / Mock Returns)**: **3** ($5.4\%$) — *WASAPI Capture, LocalWhisperEngine, DirectMlWhisperInference*
* **FLOW Level 1 (Interface / Architecture Only)**: **6** ($10.7\%$)
* **FLOW Level 0 (Missing Implementation)**: **23** ($41.0\%$)
* **Unverified / N/A (Excluded macOS-only like Notetaker)**: **0**

### Priority Breakdown
* **P0 Capabilities**: 23 total $\rightarrow$ **12 at Level 4/3** ($52.2\%$ baseline coverage)
* **P1 Capabilities**: 18 total $\rightarrow$ **3 at Level 4/3** ($16.7\%$ coverage)
* **P2/P3 Capabilities**: 15 total $\rightarrow$ **2 at Level 4/3** ($13.3\%$ coverage)
