# PHASE_2_IMPLEMENTATION_ROADMAP.md — Wispr Flow Parity Execution Plan

> **Scope**: Phase 2 (2A through 2J)  
> **Target Platform**: Windows 10/11 x64  
> **Methodology**: Sequential, phase-gated engineering work packages.

---

## Work Package Breakdown

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2A: Parity Audit & Architecture Specification (Current Task)     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2B: Core Dictation Parity (Real WASAPI & DirectML ASR Engine)    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2C: Smart Formatting, Backtracking & Spoken Lists                │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2D: Personalization Engine (Dictionary, Snippets, Styles)        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2E: Developer Mode & IDE Context Awareness                       │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2F: Safe Voice Command Mode                                      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2G: History, Search, Statistics & Desktop Scratchpad             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2H: Windows Native Hub & Settings Interface                      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2I: Privacy, DPAPI Security & Fault Recovery                     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2J: Full Parity Physical Application Validation & Benchmarks     │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Work Package 2A: Parity Audit & Architecture Specification
* **Objective**: Establish authoritative feature-parity benchmark, inspect repository implementation levels, and deliver detailed technical blueprints.
* **Scope**: Research Wispr Flow documentation, create capability matrix, identify synthetic/mock implementations, design test strategy and dependency graph.
* **Non-Scope**: Writing production feature code for Phase 2B+.
* **Components**: `docs/PHASE_2_WISPR_PARITY_MATRIX.md`, `docs/PHASE_2_IMPLEMENTATION_ROADMAP.md`, `docs/PHASE_2_DEPENDENCY_GRAPH.md`, `docs/PHASE_2_TEST_STRATEGY.md`, `docs/PHASE_2_RESEARCH_SOURCES.md`.
* **Exit Gate**: All parity documents written, solution builds with 0 errors/warnings, 27/27 tests passing, committed to git, stopped for review.

---

### Work Package 2B: Core Dictation Parity
* **Objective**: Elevate audio capture and ASR inference from Level 2 (synthetic) to Level 4/5 (real hardware execution).
* **Scope**:
  * Implement real WASAPI `IAudioCaptureClient::GetBuffer` reading 16kHz float32 audio.
  * Integrate ONNX Runtime DirectML session with real Whisper weights (`whisper-base.en` / `whisper-small.en`).
  * Add automatic CPU fallback when DirectX 12 / DirectML GPU is unavailable.
  * Add hands-free double-tap mode in `GlobalHotkeyHook`.
  * Enforce 20-minute continuous recording limit with $T - 60\text{s}$ warning.
  * Implement `WasapiDeviceManager` for microphone enumeration and hot-swapping.
* **Non-Scope**: Personal dictionary or snippets (Phase 2D).
* **Components**:
  * `Flow.Host.Windows/Native/WasapiAudioCapture.cs`
  * `Flow.Host.Windows/Native/WasapiDeviceManager.cs`
  * `Flow.Inference/DirectMlWhisperInference.cs`
  * `Flow.Core/ASR/LocalWhisperEngine.cs`
  * `Flow.Host.Windows/Native/GlobalHotkeyHook.cs`
* **Dependencies**: Phase 2A approval, ONNX Runtime DirectML NuGet.
* **Tests**: End-to-end microphone loopback capture test, ONNX DirectML execution test, CPU fallback test, double-tap hotkey test, 20-minute timer test.
* **Safety Criteria**: Absolute Zero-Enter verification on all inserted transcripts.
* **Exit Gate**: Real microphone audio transcribed by real local model into Notepad with zero cloud calls.

---

### Work Package 2C: Smart Formatting, Backtracking & Spoken Lists
* **Objective**: Replicate Wispr's intelligent dictation formatting and mid-speech corrections.
* **Scope**:
  * Backtracking correction engine: detect *"actually [replacement]"*, *"I mean [replacement]"*, *"scratch that"*.
  * Number & currency normalization (*"five hundred dollars"* $\rightarrow$ `\$500`, *"March third"* $\rightarrow$ `March 3rd`).
  * Spoken numbered lists (*"one ... two ... three ..."* $\rightarrow$ ordered markdown list).
  * Multi-language selection support in `ASROptions`.
* **Non-Scope**: Translation (Phase 4).
* **Components**:
  * `Flow.Core/Language/BacktrackingResolver.cs`
  * `Flow.Core/Language/SpokenEntityNormalizer.cs`
  * `Flow.Core/Language/ListFormattingEngine.cs`
  * `Flow.Core/Language/DeterministicTextSanitizer.cs`
* **Dependencies**: Phase 2B.
* **Tests**: Unit test suite for backtracking sentences, list formatting, currency, and numbers.
* **Safety Criteria**: Sanitizer strictly drops all `\r` and `\n` characters unless within an explicit structured list block.

---

### Work Package 2D: Personalization Engine
* **Objective**: Build local equivalents of Personal Dictionary, Snippets, and Styles.
* **Scope**:
  * Local SQLite storage for user dictionary terms, custom corrections, and snippets.
  * Whisper initial prompt token biasing using dictionary entries.
  * Voice snippets matching: voice trigger (up to 60 chars) $\rightarrow$ expansion (up to 4,000 chars).
  * Styles profiles: Formal, Casual, Very Casual, Excited mapped to target apps.
* **Non-Scope**: Team cloud snippet sharing.
* **Components**:
  * `Flow.Core/Storage/FlowDatabase.cs` (`Microsoft.Data.Sqlite`)
  * `Flow.Core/Personalization/DictionaryManager.cs`
  * `Flow.Core/Personalization/SnippetEngine.cs`
  * `Flow.Core/Personalization/StyleManager.cs`
* **Dependencies**: Phase 2C.
* **Tests**: SQLite CRUD tests, snippet trigger match tests, style application tests.
* **Exit Gate**: Speaking snippet cue instantly outputs expansion block into active text box.

---

### Work Package 2E: Developer Mode & IDE Context Awareness
* **Objective**: Enable seamless voice productivity inside code editors and terminals.
* **Scope**:
  * Voice casing conversions: `camelCase`, `snake_case`, `PascalCase`, `kebab-case`.
  * Technical token protection: variables, functions, CLI flags, file paths, URLs.
  * Voice file tagging: *"at index dot ts"* $\rightarrow$ `@index.ts`.
  * IDE context integration: VS Code, Cursor, Windsurf, and Windows Terminal.
* **Non-Scope**: Full screen OCR (Phase 5).
* **Components**:
  * `Flow.Core/Language/CasingTransformer.cs`
  * `Flow.Core/Developer/DeveloperSyntaxEngine.cs`
  * `Flow.Host.Windows/Native/IdeContextDetector.cs`
* **Dependencies**: Phase 2D.
* **Tests**: Unit tests for all casing styles, code identifiers, file tagging regexes.
* **Safety Criteria**: Zero `Enter` simulation in terminal contexts.

---

### Work Package 2F: Safe Voice Command Mode
* **Objective**: Implement voice-driven text manipulation with strict irreversible-action prevention.
* **Scope**:
  * Secondary global hotkey activating Command Mode.
  * Selection capture via UI Automation / Clipboard.
  * Voice editing transforms: *"make bullet points"*, *"summarize"*, *"fix spelling"*, *"capitalize"*.
  * Inviolable safety filter: Strictly rejects commands requesting Enter, Send, Submit, Delete, Confirm, or Shell Execution.
* **Non-Scope**: Web search / cloud generative actions.
* **Components**:
  * `Flow.Host.Windows/Commands/VoiceCommandCoordinator.cs`
  * `Flow.Host.Windows/Commands/CommandSafetyFilter.cs`
  * `Flow.Core/Commands/TextTransformationEngine.cs`
* **Dependencies**: Phase 2E.
* **Tests**: Safety filter blacklist test vectors, text transformation unit tests.
* **Exit Gate**: Highlight text $\rightarrow$ speak command $\rightarrow$ text modified in-place; all destructive commands rejected.

---

### Work Package 2G: History, Search, Statistics & Scratchpad
* **Objective**: Persistent local transcript archive, search, and productivity metrics.
* **Scope**:
  * SQLite FTS5 full-text search over past dictations.
  * Re-insert past transcripts via global shortcut ("Paste Last Transcript").
  * Metrics calculation: Words Per Minute (WPM), word count, active streaks.
  * Desktop Scratchpad draft buffer.
* **Components**:
  * `Flow.Core/History/HistoryRepository.cs`
  * `Flow.Core/History/FtsSearchEngine.cs`
  * `Flow.Core/Productivity/StatisticsAggregator.cs`
  * `Flow.Core/Storage/ScratchpadRepository.cs`
* **Dependencies**: Phase 2D.
* **Tests**: FTS5 search queries, WPM calculation tests, Scratchpad draft persistence.

---

### Work Package 2H: Windows Native Hub & Settings Interface
* **Objective**: Professional, non-vibecoded management application.
* **Scope**:
  * WinUI 3 desktop Hub window adhering to `APP_DESIGN_GUIDELINES.md` (Segoe UI, subtle dark surface, 8pt grid).
  * Tabs: Home, History, Dictionary, Snippets, Styles, Developer, Shortcuts, Audio, Models, Privacy, About.
  * Real-time VU meter for microphone level test.
  * Run on Windows startup option (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
  * Shortcut rebinding interface.
* **Non-Scope**: Cloud account logins or subscriptions.
* **Components**:
  * `Flow.Host.Windows/UI/HubWindow.xaml(.cs)`
  * `Flow.Host.Windows/UI/ViewModels/...`
  * `Flow.Host.Windows/Settings/SettingsManager.cs`
* **Dependencies**: Phases 2B - 2G.
* **Tests**: UI binding tests, settings persistence tests, startup registry tests.

---

### Work Package 2I: Privacy, DPAPI Security & Fault Recovery
* **Objective**: Enterprise-grade security hardening and fault resilience.
* **Scope**:
  * Windows DPAPI encryption for local SQLite database and configuration.
  * UI Automation password field detection (`CurrentIsPassword == true`) to suppress recording.
  * Audio endpoint disconnect/reconnect recovery without deadlocks.
  * Clean OS shutdown (`WM_QUERYENDSESSION`) handling.
* **Components**:
  * `Flow.Core/Security/DpapiDataProtector.cs`
  * `Flow.Host.Windows/Native/PasswordDetector.cs`
  * `Flow.Host.Windows/Lifecycle/SystemSessionManager.cs`
* **Dependencies**: Phase 2H.
* **Tests**: DPAPI encrypt/decrypt roundtrip, password field exclusion test, endpoint fault injection test.

---

### Work Package 2J: Full Parity Physical Application Validation
* **Objective**: Rigorous real-world physical verification across the entire Windows application suite.
* **Scope**:
  * Physical testing with real microphone input against all 12 target applications: Notepad, Word, Chrome, Edge, Gmail, Google Docs, WhatsApp Web, Slack, Notion, VS Code, Cursor, PowerShell, Windows Terminal.
  * Empirical measurement of capture latency, inference latency, insertion latency, CPU %, RAM (RSS), and VRAM.
  * Verification of zero simulated enters across all apps.
* **Components**:
  * `tests/Flow.Windows.Tests/Physical/CrossAppValidationSuite.cs`
  * `docs/PHASE_2_PARITY_VERIFICATION_REPORT.md`
* **Dependencies**: All prior Phase 2 work packages (2B through 2I).
* **Exit Gate**: Formal sign-off on 100% of P0 capabilities and 90%+ of P1 capabilities.
