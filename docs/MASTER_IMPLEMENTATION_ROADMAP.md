# MASTER_IMPLEMENTATION_ROADMAP.md — FLOW Wispr Parity Master Roadmap

> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Benchmark Reference**: Wispr Flow Windows Desktop Application (v1.5.x+ 2026 Baseline)  
> **Engineering Execution Rule**: Strict sequential phase gating. Zero cloud audio. Zero unauthorized commits. Zero fake implementations.

---

## 1. Overarching Strategic Priorities

FLOW development is governed by four sequential strategic priorities:

```
┌────────────────────────────────────────────────────────────────────────┐
│ PRIORITY 1: Wispr Flow Windows Functional Parity                       │
│ - Implement all 69 Mandatory parity capabilities to at least Level 4.  │
│ - Strict parity finish line: No Phase 3 AI features until 69/69 pass.  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ PRIORITY 2: Physical Windows Verification                              │
│ - Validate real WASAPI mic → Whisper → UIA → SendInput across apps.    │
│ - Promote Level 4 capabilities to Level 5 on Windows 10/11 hardware.  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ PRIORITY 3: UX & Product Quality Polish                                │
│ - Windows 11 Fluent Design / Mica / Acrylic styling.                   │
│ - High-DPI PerMonitorV2 scaling, seamless tray integration, animations.│
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ PRIORITY 4: FLOW Differentiated AI Features (Phase 3)                  │
│ - Phase 3A: Prompt Enhancement (Structure, Tone, Context Expansion).   │
│ - Phase 3B: Autonomous Translation & Multilingual Synthesis.           │
│ - Phase 3C: Context-Aware Reply Generation & Conversational Drafting. │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Master Phase Sequence & Current Status

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
│ Phase 2D: Personalization Engines (Dict, Snippets, Style) [COMPLETE]   │
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
│ Phase 2G: History, Search, Statistics & Desktop Scratchpad[CURRENT]    │
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

## 3. Phase-by-Phase Detailed Specifications

### Phase 2A — Parity Audit & Architecture Specification
* **Status**: `COMPLETE`
* **Deliverables**: Comprehensive Wispr feature breakdown, dependency graphs, test methodology, and 75-capability inventory.

### Phase 2B — Physical Core Dictation Parity
* **Status**: `COMPLETE` (Level 5 verified on Windows 10/11 hardware)
* **Deliverables**: Real WASAPI capture from hardware mic array, local Whisper inference (AVX2 CPU + DirectML architecture), Push-to-Talk, Double-Tap Hands-Free mode, Escape session cancellation, 20-minute recording ceiling, safe text insertion, and zero-Enter invariant.

### Phase 2C — Smart Formatting, Backtrack & Spoken Lists
* **Status**: `COMPLETE` (Level 5 verified on Windows 10/11 hardware)
* **Deliverables**: Multi-pass deterministic pipeline, spoken punctuation mapping, spoken quotes/parentheses, conservative filler suppression ("like" protected as verb), numbered lists with 2-item activation gate, technical entity protection (`C:\...`, CLI commands, URLs), and foreground HWND/PID-validated Desktop Backtrack engine.

### Phase 2D — Personalization Engines (Dictionary, Snippets, Styles)
* **Status**: `COMPLETE` (Level 4/5 verified; pipeline integrated)
* **Deliverables**: SQLite repositories, `PersonalDictionaryStage`, `SnippetsExpansionStage`, `StyleFormattingStage`, 4,000-character snippet template expansion, collision detection, and Zero-Enter invariant on all expansions. (Management GUI deferred to Phase 2H Hub).

### Phase 2E — Developer Mode & IDE Context Awareness
* **Status**: `COMPLETE` (Conditional Pass: Level 4/5 verified; 196 tests passing)
* **Deliverables**: Password field exclusion (`WF-030`, Level 5), nearby context extraction (`WF-031A`, Level 5), spoken casing triggers (`WF-032B`, Level 4), voice file tagging (`WF-034`, Level 4), multilingual language selection (`WF-021`, Level 4), automatic language detection (`WF-022`, Level 4), and code-switching (`WF-023`, Level 4).

### Phase 2F — Safe Voice Command Mode & Transforms
* **Status**: `COMPLETE` (Conditional Pass: Level 4/5 verified; 360 tests passing)
* **Deliverables**: Dedicated secondary shortcut (`Ctrl + Right Alt`, `WF-036`), selection-aware voice text transformation (`WF-037A`), Flow Bar transforms indicator (`WF-037B`), and zero-destructive execution safety policy (`WF-038`, Level 5). Strict fail-closed policy against CLI, shell, and process spawning.

### Phase 2G — History, Search, Statistics & Desktop Scratchpad
* **Status**: `CURRENT TARGET` (Priority 1 Dependency Hub)
* **Deliverables**:
  1. `WF-039A`: Local transcript history SQLite storage with FTS5 full-text indexing (`flow_history.db`).
  2. `WF-039B`: Dismissed/cancelled dictation recovery cache.
  3. `WF-041`: Paste-last-transcript global shortcut replay.
  4. `WF-042`: Productivity statistics aggregator (WPM, total word counts, active streaks).
  5. `WF-043A`: Desktop scratchpad non-activating floating editor window.
  6. `WF-043B`: Direct scratchpad voice dictation with markdown support.

### Phase 2H — Windows Native Hub & Settings Interface
* **Status**: `PENDING`
* **Deliverables**: Native Windows desktop Hub window (`WF-046`), tab navigation (History viewer `WF-040`, Dictionary GUI `WF-024B`, Corrections GUI `WF-025B`, Snippets GUI `WF-026B`, Styles GUI `WF-027B`, Shortcuts Customization `WF-047`, Audio VU Device Meter `WF-048`, Sound Cues Toggle `WF-010B`, Auto Cleanup Levels `WF-020B`, and Undo AI Edit `WF-020C`).

### Phase 2I — Privacy, DPAPI Security & Fault Recovery
* **Status**: `PENDING`
* **Deliverables**: Windows DPAPI encryption of SQLite databases (`WF-055`), system startup registry integration (`WF-051`, `HKCU\...\Run`), and audio endpoint hot-swap fault recovery.

### Phase 2J — Full Parity Physical Application Validation & Benchmarks
* **Status**: `FINAL PARITY GATE`
* **Deliverables**: Cross-application verification across Notepad, VS Code, Cursor, Visual Studio, Windows Terminal, Slack, and Office; empirical latency, CPU, and RAM profiling; certification of 69/69 mandatory capabilities.

---

## 4. Post-Parity Phase 3: FLOW Differentiated AI Features

*Strictly gated behind 100% completion and verification of the 69 Mandatory Wispr Flow parity capabilities.*

* **Phase 3A**: Prompt Enhancement (Local/Cloud LLM prompt structuring, context expansion, role-based prompting).
* **Phase 3B**: Autonomous Voice Translation & Cross-Language Synthesis.
* **Phase 3C**: Context-Aware Reply Generation & Conversational Drafting.
