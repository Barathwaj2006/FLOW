# MASTER_IMPLEMENTATION_ROADMAP.md — FLOW Wispr Parity Master Roadmap

> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Benchmark Reference**: Wispr Flow Windows Desktop Application  
> **Engineering Execution Rule**: Strict sequential phase gating. Zero cloud audio. Zero unauthorized commits. Zero fake implementations.

---

## 1. Master Phase Sequence

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2A: Parity Audit & Architecture Specification       [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2B: Physical Core Dictation Parity (WASAPI + ASR)   [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2C: Smart Formatting, Backtrack & Spoken Lists      [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2D: Personalization Engine (Dictionary, Snippets)   [CURRENT]    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2E: Developer Mode & IDE Context Awareness          [NEXT]       │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2F: Safe Voice Command Mode                         [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2G: History, Search, Statistics & Desktop Scratchpad[PENDING]    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2H: Windows Native Hub & Settings Interface         [PENDING]    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2I: Privacy, DPAPI Security & Fault Recovery        [PENDING]    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2J: Full Parity Physical Application Validation     [FINAL]      │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Phase-by-Phase Detailed Specifications

### Phase 2A — Parity Audit & Architecture Specification
* **Status**: `COMPLETE`
* **Deliverables**: Comprehensive Wispr feature breakdown, dependency graphs, test methodology, and capability mapping.

### Phase 2B — Physical Core Dictation Parity
* **Status**: `COMPLETE` (Verified on Windows 10/11 hardware)
* **Deliverables**: Real WASAPI capture from hardware mic array, local Whisper inference (AVX2 CPU + DirectML architecture), Push-to-Talk, Double-Tap Hands-Free mode, Escape session cancellation, 20-minute recording ceiling, safe text insertion, and zero-Enter invariant.

### Phase 2C — Smart Formatting, Backtrack & Spoken Lists
* **Status**: `COMPLETE` (Verified on Windows 10/11 hardware)
* **Deliverables**: Multi-pass deterministic pipeline, spoken punctuation mapping, spoken quotes/parentheses, conservative filler suppression ("like" protected as verb), numbered lists with 2-item activation gate, technical entity protection (`C:\...`, CLI commands, URLs), and foreground HWND/PID-validated Desktop Backtrack engine.

### Phase 2D — Personalization Engine (Dictionary, Snippets, Styles)
* **Status**: `IN PROGRESS` (Current Target)
* **Objective**: Deliver a production-grade, local SQLite-backed personalization subsystem providing user-defined custom vocabulary, misrecognition correction mappings, voice snippets (text expansion), and tone/style formatting profiles.
* **Core Capabilities**:
  1. **Personal Dictionary (`WF-024`)**:
     * Custom technical terms, acronyms, company names, and foreign loanwords.
     * Starred entries for high-priority matching.
     * Persistent storage via `Microsoft.Data.Sqlite` in `%LOCALAPPDATA%\FLOW\flow_personalization.db`.
     * CRUD repository with JSON/CSV import & export.
  2. **Custom Misrecognition Corrections (`WF-025`)**:
     * Exact spoken-to-written mapping rules (e.g., *"anti gravity"* $\rightarrow$ *"Antigravity"*, *"neuro sim"* $\rightarrow$ *"NeuroSim"*, *"react native"* $\rightarrow$ *"React Native"*).
     * Word-boundary aware deterministic string and regex replacement.
  3. **Voice Snippets / Text Expansion (`WF-026`)**:
     * Spoken trigger phrase matching (e.g., *"my email"*, *"zoom link"*, *"daily standup update"*).
     * Expansion into rich boilerplate templates (up to 4,000 characters).
     * Trigger collision resolution (longest matching trigger takes precedence; duplicate warning detection).
     * Strict Zero-Enter invariant preservation on all expansions.
  4. **Styles System (`WF-027`)**:
     * Style profiles: `Default`, `Personal`, `Work` / `Professional`, `Email`, `Technical`, `Casual`.
     * Rules for contractions handling (Preserve, Expand, Contract), formality level, and bulleted formatting.
     * Per-application style assignment support (e.g. VS Code $\rightarrow$ Technical, Slack $\rightarrow$ Casual, Outlook $\rightarrow$ Work/Email).
  5. **Pipeline Integration**:
     * `PersonalDictionaryStage`: Injected into `TranscriptProcessingPipeline` to apply custom corrections and casing.
     * `SnippetsExpansionStage`: Injected to expand spoken voice cues into boilerplate templates.
     * `StyleFormattingStage`: Injected to adapt tone, formality, and contractions per active style profile.
* **Verification Gates**:
  * Unit tests covering Dictionary CRUD, case-sensitivity, starred priority, Snippets expansion, collision detection, and Style transformations.
  * Physical validation test verifying real SQLite database file creation on Windows disk, schema migration, multi-entry persistence, and end-to-end transcript pipeline integration.
  * Zero-Enter invariant hard assertion on all snippet expansions and dictionary outputs.

### Phase 2E — Developer Mode & IDE Context Awareness
* **Status**: `PENDING` (Blocked until Phase 2D verification gate passes)
* **Scope**: Automated active application context detection (IDE vs browser vs word processor), programmatic casing transformations (`camelCase`, `snake_case`, `PascalCase`, `kebab-case`), voice file tagging (`@filename.ts`), and password field exclusion via UI Automation.

### Phase 2F — Safe Voice Command Mode
* **Status**: `COMPLETE`
* **Scope**: Dedicated secondary shortcut trigger (`Ctrl + Right Alt`), selection-aware voice text manipulation (*"make this bullet points"*, *"summarize selection"*, casing transforms), and zero-destructive execution safety. All 3 capabilities physically verified at Level 5.

### Phase 2G — History, Search, Statistics & Desktop Scratchpad
* **Status**: `PENDING`
* **Scope**: SQLite dictation history with FTS5 full-text indexing, paste-last-transcript hotkey, WPM / productivity metric aggregation, and desktop scratchpad.

### Phase 2H — Windows Native Hub & Settings Interface
* **Status**: `PENDING`
* **Scope**: WinUI 3 desktop window, navigation tabs (History, Dictionary, Snippets, Styles, Shortcuts), interactive audio level meter, and shortcut customization.

### Phase 2I — Privacy, DPAPI Security & Fault Recovery
* **Status**: `PENDING`
* **Scope**: Windows DPAPI encryption of SQLite databases, system startup registry integration, and fault recovery.

### Phase 2J — Full Parity Physical Application Validation & Benchmarks
* **Status**: `PENDING`
* **Scope**: Cross-application verification across Notepad, VS Code, Slack, Word, and Windows Terminal; empirical latency, CPU, and RAM profiling.
