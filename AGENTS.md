# AGENTS.md — Autonomous Agent Operating Rules

> **Status**: Active Mandate  
> **Target Audience**: Antigravity, Autonomous Coding Agents, Subagents, and Human Contributors  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  

---

## 1. Mission & Engineering Philosophy

We are building a production-grade, system-wide **AI voice productivity platform for macOS**, positioned as a competitor to Wispr Flow.

This is **NOT a hackathon demo, hobby script, or toy speech-to-text wrapper**. Every line of code must be written with the rigor, reliability, and security required to ship a commercial native macOS application to millions of users.

### Core Engineering Hierarchy
When making trade-offs, strictly follow this priority order:
1. **Reliability** (Zero crashes, zero hung audio threads, deterministic behavior)
2. **Accuracy** (Faithful transcription; absolute zero hallucinated intent)
3. **Latency** (Sub-400ms end-to-end local dictation SLA)
4. **Privacy** (100% offline audio processing for core dictation)
5. **Security** (Strict sandbox, safe AXUIElement injection, secure Keychain storage)
6. **Maintainability** (Modular architecture, clean Swift 6 concurrency, typed interfaces)
7. **User Control** (Explicit opt-in for cloud AI; clear Prompt Diffs; undoable actions)
8. **Extensibility** (Clean protocol abstractions for ASR, LLM, and Storage)
9. **Product Quality & Polish** (Native macOS feel, unobtrusive floating HUD)

---

## 2. Inviolable Core Principles

### A. LOCAL CORE (Offline Sovereignty)
* **Zero Cloud Audio**: Audio captured from the microphone for dictation MUST NEVER be streamed, transmitted, or logged to any cloud service.
* Core Voice Activity Detection (VAD) and Automatic Speech Recognition (ASR) MUST run entirely on-device using local Apple Silicon hardware acceleration (Apple Neural Engine / Metal).
* An offline machine must be able to perform 100% of basic dictation and language formatting.

### B. FAITHFUL (Content Lock & The No-Invention Rule)
* **Content Lock**: Speech cleanup and transformations must NEVER alter what the user meant.
* You may clean up: filler words ("um", "uh"), stutter/repetition, spoken punctuation, capitalization, obvious slips of the tongue, and backtracking ("actually Friday").
* You MUST PROTECT:
  * Requirements & specifications
  * Negative constraints ("do not use Firebase", "never", "without")
  * Technical terminology, programming languages, libraries, and frameworks
  * Variable names, code snippets, file paths, URLs, and commands
  * Dates, numbers, currency, units, and proper nouns
* **The No-Invention Rule**: When enhancing prompts, NEVER invent unmentioned technologies, architectural choices, or arbitrary constraints. If information is missing, identify the gap—do not fill it with assumptions.

### C. NO UNINTENTIONAL SEND / SUBMIT
* The text insertion engine must **NEVER** simulate `Return` (`kVK_Return`, 0x24), Keypad Enter, or click any form submit / message send buttons.
* Injecting text must only place characters at the active cursor position or replace selected text. Sending messages or submitting forms remains strictly an explicit user action.

### D. PHASE GATING RULE
* Development proceeds strictly sequentially:
  $$\text{Specification} \longrightarrow \text{Architecture} \longrightarrow \text{Implementation} \longrightarrow \text{Unit Tests} \longrightarrow \text{Integration} \longrightarrow \text{Benchmark} \longrightarrow \text{Review} \longrightarrow \text{Next Phase}$$
* **DO NOT** begin implementing features from Phase $N+1$ until Phase $N$ has met 100% of its defined acceptance criteria and has been verified.
* Agents must not jump ahead to build flashy cloud features while local core voice foundations are unverified.

---

## 3. Strict Anti-Patterns for AI Agents

Agents operating on this repository are explicitly forbidden from:
1. **Faking Implementations**: Never write fake mock methods with hardcoded returns (e.g. `return "Send John the report tomorrow"`) and pretend the feature is implemented.
2. **Tautological Tests**: Never write tests that test nothing (e.g., asserting `true == true`). Unit tests must exercise real logic, edge cases, and failure modes.
3. **Architecture Creep**: Never swap out core frameworks (e.g., replacing Swift with Electron, or introducing a Python runtime into the client bundle) without explicit human architectural review.
4. **Bypassing Safety / Content Lock**: Never bypass Content Lock validation to make a transformation "pass". If validation fails, surface the diff or reject the transformation.
5. **Modifying Unrelated Files**: Confine edits to the subsystem being worked on. Do not perform indiscriminate refactors of stable code.
6. **Committing Secrets or Weights**: Never commit API keys, AWS credentials, tokens, or multi-gigabyte neural network weights into git.

---

## 4. Architectural Boundaries

The codebase is partitioned into isolated layers with clear dependency directions:

```
┌───────────────────────────────────────────────────────────┐
│              FlowMacOS (Native Host Layer)                │
│  - AppKit / SwiftUI UI (Menu bar, Floating HUD)           │
│  - macOS Event Taps (CGEventTap for Global Hotkeys)       │
│  - CoreAudio / AVAudioEngine capture                      │
│  - Accessibility API (AXUIElement) & CGEvent injection    │
│  - ScreenCaptureKit for Screen AI                         │
└─────────────────────────────┬─────────────────────────────┘
                              │ Depends on
┌─────────────────────────────▼─────────────────────────────┐
│             FlowCore (Platform-Agnostic Engine)           │
│  - Audio Buffer & VAD Pipelines                           │
│  - ASR Engine Protocols (WhisperKit, whisper.cpp)         │
│  - Language Engine (Rule-based & NLP formatting)          │
│  - Content Lock Engine (Entity extraction, Diff, Verify)  │
│  - Personal Intelligence (SQLite / GRDB persistence)      │
│  - Developer Mode (Casing engine, syntax formatting)      │
│  - Cloud AI Client (AWS Bedrock REST / SigV4)             │
└───────────────────────────────────────────────────────────┘
```

* `FlowCore` must compile independently and contain zero direct UI dependencies.
* All external capabilities (ASR, Audio Capture, Text Insertion, Cloud AI) MUST be defined via Swift protocols (`ASREngineProtocol`, `AudioSourceProtocol`, `TextInsertionServiceProtocol`) to allow 100% mockability in CI and cross-platform testing.

---

## 5. Coding Standards & Conventions

* **Language**: Swift 6 with strict concurrency checking (`Sendable`, actors, structured concurrency `async`/`await`).
* **Error Handling**: Use typed Swift `Error` enums with descriptive contexts. Never use `fatalError()` or force-unwrap (`!`) in runtime production paths.
* **Logging**: Use Apple's unified logging system (`os.Logger`) with dedicated subsystems (e.g., `com.flow.core.asr`, `com.flow.macos.insertion`). Never use `print()` for production logging.
* **Sensitive Data Redaction**: Audio buffers, user dictation text, and clipboard contents must be marked as private in logs (`logger.debug("Captured text: \(text, privacy: .private)")`).
* **Memory & Resource Hygiene**: Audio recording taps, model inference sessions, and event monitors must be explicitly deallocated and cleaned up on cancellation or app termination.

---

## 6. Verification Checklist Before Reporting Task Complete

Before declaring any engineering task or phase complete, the agent must verify:
- [ ] Code builds cleanly without warnings under Swift 6 strict concurrency.
- [ ] All unit tests pass (`swift test`).
- [ ] Edge cases and failure modes are explicitly tested (e.g., empty audio, microphone permission denial, un-insertable text fields, network dropouts).
- [ ] Content Lock validation assertions are verified with test vectors.
- [ ] Latency budget is respected (no synchronous blocking on main/audio threads).
- [ ] Documentation updated to reflect changes.
