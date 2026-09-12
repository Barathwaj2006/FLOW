# PHASE_2_IMPLEMENTATION_ROADMAP.md — FLOW Wispr Parity Master Roadmap

> **Target Platform**: Windows 10/11 x64 Native Desktop (.NET 9.0)  
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
│ Phase 2D: Personalization Engine (Backend SQLite Complete)[COMPLETE]   │
│           (Personal Dictionary, Corrections, Snippets, Styles)         │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Parity Reconciliation Gate: Definitive Windows Spec Audit [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2E: Developer Mode & IDE Context Awareness          [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2F: Safe Voice Command Mode & Transforms            [COMPLETE]   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2G: History, Search, Statistics & Desktop Scratchpad[NEXT]       │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2H: Native Windows Hub Window & HUD Polish          [PENDING]    │
│           (WinUI 3 GUI for Dictionary, Snippets, Styles,               │
│            History, Settings, and HUD Audio Waveform)                  │
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

## 2. Phase-by-Phase Capability Allocation (75 Capabilities)

### Phase 2A — Parity Audit & Architecture Specification
* **Status**: `COMPLETE`
* **Deliverables**: Comprehensive feature breakdown, dependency graphs, test methodology, and capability mapping.

### Phase 2B — Physical Core Dictation Parity
* **Status**: `COMPLETE` (26 Capabilities Verified on Windows 10/11 hardware)
* **Capabilities Covered**:
  - Core Voice & Audio: `WF-001` (PTT), `WF-002` (Hands-Free), `WF-003` (Cancel), `WF-004` (WASAPI capture), `WF-005` (Ring buffer), `WF-006` (VAD), `WF-007` (Mic selection), `WF-008` (20m ceiling), `WF-009` (Whisper audio), `WF-010A` (Audio feedback cues).
  - Transcription: `WF-011` (Local WhisperNet ASR), `WF-012` (ASR fallback), `WF-017` (Zero-Enter safety).
  - Context & Insertion: `WF-031B` (Contextual routing), `WF-035` (IDE & terminal compat), `WF-038` (Zero-destructive safety), `WF-049` (Dual-tier insertion).
  - Desktop UX: `WF-044A` (Floating HUD window), `WF-044C` (HUD state transitions), `WF-045` (System tray).
  - Windows Hygiene & Privacy: `WF-050` (PerMonitorV2 DPI), `WF-052` (Graceful shutdown), `WF-053A` (Audio endpoint recovery), `WF-053B` (RDP safety), `WF-054` (100% local RAM audio), `WF-056` (Zero telemetry leaks).

### Phase 2C — Smart Formatting, Backtrack & Spoken Lists
* **Status**: `COMPLETE` (10 Capabilities Verified on Windows 10/11 hardware)
* **Capabilities Covered**:
  - `WF-013` (Automatic capitalization), `WF-014` (Terminal punctuation), `WF-015` (Spoken punctuation), `WF-016` (Filler word removal), `WF-018` (Backtracking self-correction), `WF-019` (Numbered & bulleted lists), `WF-020A` (Number/date formatting).
  - Context & Developer: `WF-029` (Active app detection), `WF-032A` (Programmatic casing), `WF-033` (Technical token shield).

### Phase 2D — Personalization Engine (Backend)
* **Status**: `COMPLETE` (4 Capabilities Verified via SQLite Persistence & Pipeline Stages)
* **Capabilities Covered**:
  - `WF-024A` (Personal Dictionary Engine: SQLite storage, regex matching, casing preservation, starred priority, import/export).
  - `WF-025A` (Custom Corrections Engine: Phonetic replacement rules).
  - `WF-026A` (Voice Snippets Engine: 4k character templates, trigger matching, Zero-Enter enforcement).
  - `WF-027A` (Writing Styles Engine: Profiles, contraction policy, formality substitutions, app mapping).

### Phase 2E — Developer Mode & IDE Context Awareness
* **Status**: `COMPLETE` (7 Capabilities Implemented & Tested + 4 Regression Validated)
* **Implement**:
  - `WF-030`: Password Field Exclusion (`CurrentIsPassword` check to refuse recording/insertion).
  - `WF-031A`: Nearby Context Read (UIA `TextPattern` reading preceding text to bias formatting).
  - `WF-032B`: Spoken Casing Triggers (Voice command parser for *"camel case [text]"*, etc.).
  - `WF-034`: Voice File Tagging (Voice filter for *"at filename dot ts"* $\rightarrow$ `@filename.ts`).
  - `WF-021`: Multi-Language Selection (Manual selection parameter).
  - `WF-022`: Auto Language Detection (Whisper LID first-chunk token extraction).
  - `WF-023`: Code-Switching (Vocabulary adaptation via prompt biasing).
* **Regression Validate**:
  - `WF-029` (Active App Detection), `WF-032A` (Programmatic Casing), `WF-033` (Technical Token Shield), `WF-035` (IDE & Terminal Compatibility).

### Phase 2F — Safe Voice Command Mode & Transforms
* **Status**: `COMPLETE` (3 Capabilities)
* **Capabilities Covered**:
  - `WF-036`: Command Mode Global Shortcut Trigger (Level 5).
  - `WF-037A`: Selection-Aware Voice Editing (Level 5).
  - `WF-037B`: Flow Bar Transforms Widget (Level 5).
* **Regression Validate**:
  - `WF-038`: Zero-Destructive Execution Safety (Level 5).

### Phase 2G — History, Search, Statistics & Desktop Scratchpad
* **Status**: `PENDING` (7 Capabilities)
* **Capabilities Covered**:
  - `WF-028`: Auto-Learned Vocabulary (Optional/Beta).
  - `WF-039A`: History SQLite Storage & FTS5 Search.
  - `WF-039B`: Dismissed / Cancelled Dictation Recovery.
  - `WF-041`: Paste-Last-Transcript Global Shortcut.
  - `WF-042`: Productivity Statistics (WPM, words, streaks).
  - `WF-043A`: Desktop Scratchpad Floating Window.
  - `WF-043B`: Scratchpad Direct Voice Dictation & Markdown.

### Phase 2H — Native Windows Hub Window & HUD Polish
* **Status**: `PENDING` (12 Capabilities)
* **Capabilities Covered**:
  - Hub Subsystem UI: `WF-024B` (Dictionary Hub GUI), `WF-025B` (Corrections Hub GUI), `WF-026B` (Snippets Hub GUI), `WF-027B` (Styles Hub GUI), `WF-040` (History Hub GUI), `WF-046` (Native WinUI 3 Hub window).
  - Settings & Controls: `WF-010B` (Audio feedback toggle), `WF-020B` (Auto cleanup levels slider), `WF-020C` (Undo AI edit action), `WF-047` (Shortcut rebinding UI), `WF-048` (Audio diagnostics VU meter).
  - HUD Polish: `WF-044B` (HUD audio waveform meter).

### Phase 2I — Privacy, DPAPI Security & Fault Recovery
* **Status**: `PENDING` (2 Capabilities)
* **Capabilities Covered**:
  - `WF-051`: Run on Windows Startup (`HKCU\...\Run` manager).
  - `WF-055`: Local DPAPI Encryption of SQLite databases.

### Phase 2J — Full Parity Physical Application Validation & Benchmarks
* **Status**: `FINAL GATE`
* **Objective**: Full 12-application physical desktop matrix validation, empirical latency, CPU, and RAM profiling.

### Out of Scope
* `WF-OS-01`: Wispr Flow AI Notetaker.
* `WF-OS-02`: Mobile Virtual Keyboards.
* `WF-OS-03`: Team Cloud Synchronization.
* `WF-OS-04`: Cloud Telemetry & Analytics.
