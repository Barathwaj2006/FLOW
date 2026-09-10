# ROADMAP.md — Phased Engineering Roadmap

> **Status**: Active Baseline  
> **Rule**: Strict phase gating. Do not begin Phase $N+1$ until Phase $N$ meets 100% of defined acceptance criteria.  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  

---

## Roadmap Overview

```
[Phase 0: Foundation] ──► [Phase 1: Voice Core] ──► [Phase 2: Smart Dictation]
                                                            │
┌───────────────────────────────────────────────────────────┘
▼
[Phase 3: Content Lock] ──► [Phase 4: AI Productivity] ──► [Phase 5: Screen AI]
                                                                    │
┌───────────────────────────────────────────────────────────────────┘
▼
[Phase 6: Developer Mode] ──► [Phase 7: Personal Intel] ──► [Phase 8: Context Intel]
                                                                    │
┌───────────────────────────────────────────────────────────────────┘
▼
[Phase 9: AWS Hackathon Build] ──► [Phase 10: Public MVP] ──► [Phase 11: Commercial Scale]
```

---

## Phase Breakdown

### Phase 0: Foundation
* **Goal**: Establish the engineering repository, governance rules, system architecture, test harnesses, and dependency provenance.
* **Key Deliverables**:
  * Complete architecture & security specifications (`AGENTS.md`, `PROJECT_PROFILE.md`, `ARCHITECTURE.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`).
  * Testing and dependency strategy documents (`TESTING_STRATEGY.md`, `DEPENDENCIES.md`, `ACCEPTANCE_CRITERIA.md`).
  * Multi-package Swift repository layout (`packages/FlowCore`, `packages/FlowMacOS`).
  * Continuous Integration (CI) configuration for native macOS runners.
* **Exit Gate**: All specification documents approved; directory structure initialized; CI pipeline passing.
* **Current Status**: **IN PROGRESS** (Completing now).

---

### Phase 1: Voice Core
* **Goal**: The fundamental loop: Global Hotkey $\longrightarrow$ Microphone Capture $\longrightarrow$ VAD $\longrightarrow$ Local ASR $\longrightarrow$ Text Insertion.
* **Key Deliverables**:
  * Global hotkey daemon using macOS `CGEventTap`.
  * Real-time 16kHz audio capture via `AVAudioEngine` with noise gate.
  * Silero VAD integration (speech detection and silence termination).
  * Modular `ASREngineProtocol` with local `WhisperKit` / `whisper.cpp` driver.
  * Universal cursor text insertion engine (`AXUIElement` with safe clipboard fallback).
  * Minimal non-activating Floating HUD indicating recording state.
* **Exit Gate**: Press hotkey, speak a sentence, and raw text appears at the cursor in TextEdit and VS Code with $< 450\text{ ms}$ latency. Zero cloud network requests.

---

### Phase 2: Smart Dictation & Multilingual
* **Goal**: Convert raw phonemes into clean, well-formatted prose; support Tamil speech and translation.
* **Key Deliverables**:
  * Spoken punctuation engine (`"period"` $\rightarrow$ `.`, `"new line"` $\rightarrow$ `\n`).
  * Filler word suppressor (`"um"`, `"uh"`, stutter removal).
  * Backtracking intent resolver (`"send it tomorrow actually Friday"`).
  * Tamil Language Identification (LID) and multilingual Whisper model loader.
  * Spoken translation pipeline (Tamil $\longrightarrow$ English).
* **Exit Gate**: 95%+ accuracy on spoken punctuation test suite; successful cleanup of backtracking sentences; accurate Tamil $\rightarrow$ English translation without dropped numbers or names.

---

### Phase 3: Content Lock
* **Goal**: Implement the core differentiator: verify that AI transformations never alter or drop critical user intent, code, or constraints.
* **Key Deliverables**:
  * Entity extraction engine (URLs, numbers, dates, programming identifiers, commands).
  * Negative constraint parser (`"do not use Firebase"`, `"never"`).
  * Bi-directional verification matrix comparing input entities against transformed output.
  * The No-Invention Rule validator (detects hallucinated frameworks or tech stacks).
  * Visual Prompt Diff modal highlighting added/removed tokens with Apply/Cancel controls.
* **Exit Gate**: 100% detection rate on intentionally inverted constraints and dropped entities in benchmark dataset; zero unintended insertions when validation fails.

---

### Phase 4: AI Productivity (Bedrock Integration)
* **Goal**: Advanced generative operations powered by Amazon Bedrock.
* **Key Deliverables**:
  * Serverless AWS stack (API Gateway, Lambda proxy, Amazon Bedrock runtime).
  * SigV4 client in `FlowCore` with Claude 3.5 Sonnet / Haiku drivers.
  * Prompt Engineer module (turns rough ideas into structured engineering prompts).
  * Reply Generator module (drafts professional replies from provided text context).
  * Reusable Transform Engine (rewrite, summarize, make shorter, change tone).
* **Exit Gate**: Spoken prompt successfully transformed into structured prompt; Prompt Diff displayed; zero auto-submission of messages.

---

### Phase 5: Screen AI
* **Goal**: Visual intelligence allowing users to select screen regions and reason over them.
* **Key Deliverables**:
  * Interactive crosshair selection overlay using `ScreenCaptureKit`.
  * Local OCR pipeline using Apple Vision `VNRecognizeTextRequest`.
  * Multimodal payload generation for Amazon Bedrock.
  * Screenshot $\longrightarrow$ Reply workflow (select WhatsApp/email snippet $\rightarrow$ dictate instruction $\rightarrow$ generate reply).
* **Exit Gate**: Region capture executed in $< 50\text{ ms}$; OCR accuracy $> 98\%$ on standard text; Bedrock multimodal reasoning returns valid reply inserted into reply field.

---

### Phase 6: Developer Mode
* **Goal**: Code-aware voice productivity for software engineers.
* **Key Deliverables**:
  * Voice casing state machine (`camelCase`, `snake_case`, `kebab-case`, `SCREAMING_SNAKE_CASE`).
  * Terminal command and syntax formatting (Git commands, SQL, JSON, YAML).
  * Automatic active application detection (enables Developer Mode when in VS Code, Cursor, iTerm2).
* **Exit Gate**: 100% precision on casing transformation test vectors; proper quoting and escaping of shell paths and arguments.

---

### Phase 7: Personal Intelligence
* **Goal**: User-approved local customization and memory.
* **Key Deliverables**:
  * Embedded SQLite database via `GRDB.swift` with schema migrations.
  * Custom user phonetic dictionary (e.g., `"fast api"` $\rightarrow$ `FastAPI`).
  * Snippet expansion engine (`"my calendar link"` $\rightarrow$ URL).
  * Secure local preferences and history storage with encryption at rest.
* **Exit Gate**: Custom vocabulary overrides default ASR output in $< 5\text{ ms}$; snippets expand accurately; local data fully exportable and purgeable.

---

### Phase 8: Context Intelligence
* **Goal**: Deep, privacy-preserving awareness of the active desktop context.
* **Key Deliverables**:
  * Accessibility context scraper (`AXUIElement` inspection of active text field, selection, and application title).
  * Strict data minimization filter (collects only the focused element text, never background screens).
  * Application-specific profiles (e.g., formal tone in Mail, technical in IDE).
* **Exit Gate**: Active app detected accurately across 50 top macOS applications; zero context scraping outside the focused application window.

---

### Phase 9: AWS Hackathon Build
* **Goal**: Deliver a battle-tested, high-impact demonstration showcasing the synergy of local-first voice with AWS Bedrock cloud intelligence.
* **Key Deliverables**:
  * Infrastructure-as-Code (AWS CDK or CloudFormation template) for one-click deployment.
  * Polished demo scenarios: Prompt Engineering with Content Lock, Screen AI WhatsApp reply, and Developer Mode coding session.
  * Performance and latency benchmarking telemetry dashboard.
* **Exit Gate**: Flawless live execution of end-to-end demo flows; sub-second Bedrock response times; compelling presentation assets.

---

### Phase 10: Public MVP
* **Goal**: The first production-ready public release for external macOS users.
* **Key Deliverables**:
  * Apple Developer ID code signing and Notarization pipeline.
  * Sparkle 2 framework for secure auto-updates.
  * Clean onboarding wizard explaining Accessibility and Microphone permissions.
  * Privacy Dashboard and Local-Only mode toggle.
* **Exit Gate**: Signed and notarized `.dmg` installer passes Gatekeeper on macOS 14 and 15 without warnings; crash-free session rate $> 99.5\%$.

---

### Phase 11: Commercial Scale
* **Goal**: Multi-user accounts, cloud synchronization of personal dictionaries, enterprise administration, and billing.
* **Key Deliverables**:
  * Cloud sync for snippets and dictionary (E2E encrypted with user keys).
  * Team and enterprise license management.
  * Stripe billing integration and quota management.
* **Exit Gate**: Enterprise SOC-2 readiness, zero data leaks, multi-tenant compliance.
