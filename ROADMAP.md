# ROADMAP.md — Phased Engineering Roadmap (Windows Native)

> **Status**: Windows Realignment Baseline  
> **Rule**: Strict phase gating. Do not begin Phase $N+1$ until Phase $N$ meets 100% of defined acceptance criteria and human approval is obtained.  
> **Project**: FLOW — AI Voice Productivity Platform for Windows  

---

## Roadmap Overview

```
[Phase 0: Windows Realignment] ──► [Phase 1: Voice Core] ──► [Phase 2: Smart Dictation]
                                                                     │
┌────────────────────────────────────────────────────────────────────┘
▼
[Phase 3: Content Lock] ──► [Phase 4: AI Productivity] ──► [Phase 5: Screen AI]
                                                                     │
┌────────────────────────────────────────────────────────────────────┘
▼
[Phase 6: Developer Mode] ──► [Phase 7: Personal Intel] ──► [Phase 8: Context Intel]
                                                                     │
┌────────────────────────────────────────────────────────────────────┘
▼
[Phase 9: AWS Hackathon Build] ──► [Phase 10: Public MVP] ──► [Phase 11: Commercial Scale]
```

---

## Phase Breakdown

### Phase 0: Foundation / Windows Architecture Realignment
* **Goal**: Realignment of repository foundation, architectural governance, technical specifications, and migration classification for Windows 10/11 x64.
* **Key Deliverables**:
  * Realignment audit and classification matrix (`MIGRATION_MATRIX.md`).
  * Windows-native specifications (`AGENTS.md`, `PROJECT_PROFILE.md`, `ARCHITECTURE.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`).
  * Realigned testing and dependency strategies (`TESTING_STRATEGY.md`, `DEPENDENCIES.md`, `ACCEPTANCE_CRITERIA.md`).
  * Evaluation of Windows architecture candidates (Option A, Option B, Option C).
* **Exit Gate**: All Windows specifications reviewed and committed; clear architectural consensus reached; zero premature Phase 1 code written.
* **Current Status**: **AUDIT & SPECIFICATION COMPLETE** (Phase 0 Complete — Stop Condition Active).

---

### Phase 1: Voice Core (Windows Native)
* **Goal**: Fundamental local dictation loop on Windows:
  $$\text{Global Hotkey} \longrightarrow \text{WASAPI Capture} \longrightarrow \text{Ring Buffer} \longrightarrow \text{VAD} \longrightarrow \text{Local ASR} \longrightarrow \text{Sanitizer} \longrightarrow \text{UIA Cursor Insertion}$$
* **Key Deliverables**:
  * Win32 Global Push-to-Talk monitoring (`SetWindowsHookEx(WH_KEYBOARD_LL)` or `RegisterHotKey`).
  * Real-time 16kHz audio capture via WASAPI (`IAudioClient3` / `IAudioCaptureClient`).
  * Voice Activity Detection (VAD) with silence threshold gating.
  * Extensible `IASREngine` interface with DirectML / whisper.cpp local backends and mock engine.
  * Deterministic text sanitizer (safe capitalization, safe punctuation, whitespace collapse).
  * Windows text insertion engine (`IUIAutomation` with safe `SendInput` clipboard fallback and 150ms restore).
  * Non-activating floating HUD window (`WS_EX_NOACTIVATE | WS_EX_TOPMOST`).
* **Exit Gate**: Dictation working reliably into Windows Notepad, VS Code, and Chrome without network calls and with 0 instances of simulated `VK_RETURN` (Enter).
* **Status**: **NOT STARTED — AUTHORIZATION PENDING**.

---

### Phase 2: Smart Dictation & Multilingual
* **Goal**: Transform raw speech into clean, punctuated prose; handle Tamil speech and translation.
* **Key Deliverables**:
  * Spoken punctuation engine (`"comma"`, `"period"`, `"question mark"`, `"new line"`).
  * Filler word suppressor (`"um"`, `"uh"`, stutter collapse).
  * Intent-aware backtracking resolver (`"send it tomorrow actually Friday"`).
  * Tamil Language Identification (LID) and multilingual Whisper model integration.
  * Spoken translation pipeline (Tamil $\longrightarrow$ English).
* **Exit Gate**: 95%+ precision on spoken punctuation test matrix; accurate Tamil $\rightarrow$ English translation without dropped entities.

---

### Phase 3: Content Lock
* **Goal**: Implement the core differentiator: verify that AI transformations never alter or drop critical user intent, code, or constraints.
* **Key Deliverables**:
  * Entity extraction engine (URLs, numbers, dates, programming identifiers, commands).
  * Negative constraint parser (`"do not use Firebase"`, `"never"`).
  * Bi-directional verification matrix comparing input entities against transformed output.
  * The No-Invention Rule validator (detects hallucinated frameworks or tech stacks).
  * Visual Prompt Diff modal highlighting added/removed tokens with Apply/Cancel controls.
* **Exit Gate**: 100% detection rate on intentionally inverted constraints and dropped entities in benchmark dataset.

---

### Phase 4: AI Productivity (AWS Bedrock)
* **Goal**: Advanced generative operations powered by Amazon Bedrock.
* **Key Deliverables**:
  * Serverless AWS stack (API Gateway, Lambda proxy, Amazon Bedrock runtime).
  * REST / SigV4 client with Claude 3.5 Sonnet and Amazon Nova drivers.
  * Prompt Engineer module (turns rough ideas into structured engineering prompts).
  * Reply Generator module (drafts professional replies from provided text context).
  * Reusable Transform Engine (rewrite, summarize, make shorter, change tone).
* **Exit Gate**: Structured prompt generated without hallucination; 0 automated message dispatches.

---

### Phase 5: Screen AI
* **Goal**: Visual intelligence allowing users to select screen regions and reason over them.
* **Key Deliverables**:
  * Interactive crosshair selection overlay using Windows Graphics Capture (`Direct3D11CaptureFramePool`).
  * Local OCR pipeline using Windows native OCR (`Windows.Media.Ocr`).
  * Multimodal payload generation for Amazon Bedrock.
  * Screenshot $\longrightarrow$ Reply workflow (select WhatsApp/email snippet $\rightarrow$ dictate instruction $\rightarrow$ generate reply).
* **Exit Gate**: Region capture executed in $< 50\text{ ms}$; zero desktop capture outside user crop.

---

### Phase 6: Developer Mode
* **Goal**: Code-aware voice productivity for Windows software engineers.
* **Key Deliverables**:
  * Voice casing state machine (`camelCase`, `snake_case`, `kebab-case`, `PascalCase`, `SCREAMING_SNAKE_CASE`).
  * Terminal command and syntax formatting (Git commands, PowerShell, SQL, JSON, YAML).
  * Automatic active application detection (enables Developer Mode when in VS Code, Visual Studio, Windows Terminal).
* **Exit Gate**: 100% precision on casing transformation test vectors; proper quoting and escaping of shell paths.

---

### Phase 7: Personal Intelligence
* **Goal**: User-approved local customization and memory.
* **Key Deliverables**:
  * Embedded SQLite database via `Microsoft.Data.Sqlite` with DPAPI encryption.
  * Custom user phonetic dictionary (e.g., `"fast api"` $\rightarrow$ `FastAPI`).
  * Snippet expansion engine (`"my calendar link"` $\rightarrow$ URL).
* **Exit Gate**: Custom vocabulary overrides default ASR output in $< 5\text{ ms}$; local data fully exportable and purgeable.

---

### Phase 8: Context Intelligence
* **Goal**: Privacy-preserving awareness of the active desktop context.
* **Key Deliverables**:
  * UI Automation context scraper (inspects focused text field, selection, and application title).
  * Minimum necessary context filter (collects only focused element text).
  * Application-specific profiles.
* **Exit Gate**: Active app detected accurately across top 50 Windows desktop applications.

---

### Phase 9: AWS Hackathon Build
* **Goal**: Deliver a battle-tested, high-impact demonstration showcasing local-first voice with AWS Bedrock on Windows.
* **Key Deliverables**:
  * Infrastructure-as-Code (AWS SAM / CloudFormation) for one-click deployment.
  * Polished demo scenarios: Prompt Engineering with Content Lock, Screen AI reply, and Developer Mode coding session.
  * Performance and latency benchmarking telemetry dashboard.
* **Exit Gate**: Flawless live execution of demo flows on Windows 11 hardware.

---

### Phase 10: Public MVP
* **Goal**: First production-ready public release for Windows users.
* **Key Deliverables**:
  * Windows App SDK / WinUI 3 standalone or MSIX installer.
  * Windows Code Signing certificate integration.
  * Clean onboarding wizard explaining Windows microphone permissions and hotkeys.
  * Privacy Dashboard and Local-Only mode toggle.
* **Exit Gate**: Installer installs cleanly without SmartScreen blocks; crash-free session rate $> 99.5\%$.

---

### Phase 11: Commercial Scale
* **Goal**: Multi-user accounts, cloud synchronization of personal dictionaries, enterprise administration, and billing.
* **Key Deliverables**:
  * Cloud sync for snippets and dictionary (E2E encrypted).
  * Enterprise license management.
* **Exit Gate**: Enterprise SOC-2 readiness, zero data leaks, multi-tenant compliance.
