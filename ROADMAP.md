# ROADMAP.md — Phased Engineering Roadmap (Windows Native)

> **Status**: Active Mandate — Revised Master Roadmap (Wispr Flow Windows Parity & Beyond)  
> **Rule**: Strict phase gating. Do not begin Phase $N+1$ until Phase $N$ meets 100% of defined acceptance criteria and human approval is obtained.  
> **Project**: FLOW — Windows-Native AI Voice Productivity Platform  

---

## Strategic Product Trajectory

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PHASE 0: FOUNDATION (Completed)                 │
│  - Architecture, Windows migration, governance, project structure      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│                    PHASE 1: VOICE CORE (Implemented)                   │
│  - Hotkey → WASAPI capture → Ring Buffer → VAD → ASR → Safe Insertion  │
│  - Unit tests & microbenchmarks passing (now ready for physical test)  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│              PHASE 2: WISPR FLOW WINDOWS PARITY (Major Build)          │
│  2A: Core Dictation (Push-to-talk, hands-free, multi-language, limits) │
│  2B: Intelligent Dictation (Punctuation, backtrack, numbers, lists)    │
│  2C: Personalization (Dictionary, custom corrections, snippets, styles)│
│  2D: Developer Mode (Case transforms, identifiers, code syntax, IDEs)  │
│  2E: Command Mode (Voice editing/search, strictly ZERO accidental send)│
│  2F: History & Productivity (History, search, statistics, scratchpad)  │
│  2G: Windows Integration (Tray, startup, focus, shortcuts, monitors)   │
│  2H: Hub / Settings (Full non-vibecoded management application)        │
│  2I: Validation (Rigorous physical cross-application testing)          │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│                 DIFFERENTIATION PHASES (Competitive Advantage)         │
│  Phase 3: Prompt Enhancement (Prompt Engineer, Prompt Diff, Lock)      │
│  Phase 4: Multilingual Translation (Tamil/Tanglish/Code-switch → EN)   │
│  Phase 5: Contextual Reply Generation (Screen/text context + Voice)    │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Detailed Phase Breakdown

### Phase 0: Foundation / Architecture Realignment (Completed)
* **Goal**: Full realignment of repository foundation, architectural governance, technical specifications, and migration classification for Windows 10/11 x64.
* **Status**: **COMPLETE**.

---

### Phase 1: Voice Core (Implemented — Verification Gate)
* **Goal**: Fundamental native Windows voice-to-text pipeline:
  $$\text{Global Hotkey} \longrightarrow \text{WASAPI Capture} \longrightarrow \text{Ring Buffer} \longrightarrow \text{VAD} \longrightarrow \text{Local ASR} \longrightarrow \text{Sanitizer} \longrightarrow \text{Safe Cursor Insertion}$$
* **Status**: **IMPLEMENTED & COMPILED**. 27/27 tests passing, P50 latency 15.43ms recorded. Ready for real-world physical verification.

---

### Phase 2: Wispr Flow Parity (Major Build Phase)

The objective is to implement the complete set of applicable capabilities currently available in Wispr Flow for Windows, independently and natively in FLOW.

#### 2A — Core Dictation
* Global push-to-talk (hold-to-dictate).
* Double-press hands-free mode (toggle dictation).
* Microphone device management and hot-swapping.
* Real-time recording state and floating feedback.
* Local transcription engine with configurable models.
* Multi-language architecture & automatic language selection.
* Whispering / low-amplitude speech adaptation.
* Desktop recording limits, safe cancellation (`Esc`), and crash recovery.

#### 2B — Intelligent Dictation
* Automatic punctuation and capitalization.
* Deterministic spoken punctuation parser (`"comma"`, `"period"`, `"question mark"`).
* Filler word suppression (`"um"`, `"uh"`).
* Sentence cleanup and backtracking resolution (`"actually Friday"`).
* Spoken numbered lists and structured formatting.
* Context-aware proper nouns and vocabulary adaptation.

#### 2C — Personalization
* **Personal Dictionary**: Custom words, team jargon, acronyms.
* **Auto-learning Vocabulary**: Learn user-approved terms over time.
* **Custom Corrections**: Explicit word/phrase replacement rules.
* **Snippets**: Voice trigger expansion (up to 60-char triggers, 4,000-char expansions).
* **Styles**: Persona/tone formatting (Personal, Work, Email, Other / Formal, Casual, Excited).
* **App-specific Behavior**: Contextual style switches per active process.

#### 2D — Developer Mode (Hardened & Certified — Phase 6 & Phase 6.5)
* Casing transformations: `camelCase`, `snake_case`, `PascalCase`, `kebab-case`, `SCREAMING_SNAKE_CASE` (formally idempotent, boundary-aware).
* Code syntax recognition: Acronyms, variables, functions, classes, CLI/PowerShell commands (scoped to code editors).
* Paths (`C:\...`, relative, directory spaces, `Program Files (x86)`), URLs, JSON, YAML, Markdown formatting.
* IDE integration: VS Code, Windows Terminal, PowerShell, CMD physically validated; voice file tagging (`@file.ext`).
* Production Certification: 1,929 solution tests passing (1,795 Core + 134 Windows); 511 adversarial developer cases (0 corruptions), 106 adversarial prose cases (0 false positives), 37 mathematical property tests, 2,000-iteration seeded fuzzing suite. Zero shell execution, zero Enter key injection.

#### 2E — Command Mode
* Dedicated command shortcut / state.
* Voice commands for: Edit, Select, Delete, Replace, Format, and Search.
* **Inviolable Safety Rule**: Never silently send, submit, execute, confirm, or perform irreversible destructive actions.

#### 2F — History & Productivity
* Transcript history log with full-text search.
* Copy, edit, delete, and re-insert past transcripts.
* Favorites / flags.
* Productivity statistics: WPM, word count, active streaks.
* Desktop Scratchpad & "Paste Last Transcript" global shortcut.

#### 2G — Windows Integration
* Windows notification area (System Tray) with status and quick controls.
* Run on startup (Registry / Startup task).
* Global shortcut customization.
* Target focus detection and robust UI Automation / Clipboard fallback.
* Per-monitor DPI awareness (PerMonitorV2) and multi-monitor positioning.
* Audio endpoint change handling and device failure resilience.
* Graceful Windows shutdown/restart handling and crash recovery.

#### 2H — Hub / Settings Application
* Production Windows management interface:
  $$\text{Home} \mid \text{History} \mid \text{Dictionary} \mid \text{Snippets} \mid \text{Styles} \mid \text{Developer} \mid \text{Shortcuts} \mid \text{Audio} \mid \text{Models} \mid \text{Privacy} \mid \text{Advanced} \mid \text{About}$$
* Zero fake controls: Every toggle, slider, and field maps directly to live subsystem settings.

#### 2I — Validation & Acceptance
* **Functional & Integration**: Every subsystem implemented and working cohesively.
* **Physical Testing**: Real microphone dictation into Notepad, Word, Chrome, Gmail, Google Docs, WhatsApp Web, VS Code, Cursor, and Windows Terminal.
* **Safety Invariants**: Zero accidental Enter, Send, Submit, Execute, Delete, or Confirmation.
* **Performance**: Empirical end-to-end latency, RAM, CPU, VRAM, and cold-start benchmarks.

---

## Differentiation Phases (Post-Parity)

### Phase 3: Prompt Enhancement
* Turn raw speech into structured engineering prompts.
* Intent extraction, requirements capture, constraint preservation.
* Prompt Diff review modal, Content Lock, and the No-Invention Rule.

### Phase 4: Multilingual Translation
* Spoken translation pipeline: Tamil $\longrightarrow$ English.
* Tanglish (Tamil + English code-switching) $\longrightarrow$ Clean English.
* Focus on intent preservation rather than word-for-word translation.

### Phase 5: Contextual Reply Generation
* Selected text and screen context combined with voice instruction.
* Draft professional responses across email and chat.
* Strictly requires explicit user review; zero automated sending.
