# AGENTS.md — Autonomous Agent Operating Rules (Windows Native)

> **Status**: Active Mandate — Windows Realignment Baseline  
> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Target Audience**: Antigravity, Autonomous Coding Agents, Subagents, and Human Contributors  
> **Project**: FLOW — AI Voice Productivity Platform for Windows  

---

## 1. Mission & Engineering Philosophy

We are building a production-grade, system-wide **AI voice productivity platform for Windows 10/11**, positioned as a serious competitor to Wispr Flow.

This is **NOT a hackathon demo, hobby script, or toy speech-to-text wrapper**. Every line of code must be written with the rigor, reliability, and security required to ship a commercial native Windows desktop application to millions of enterprise and individual users.

### Core Engineering Hierarchy
When making trade-offs, strictly follow this priority order:
1. **Reliability** (Zero unhandled exceptions, zero hung audio threads, deterministic COM/UIA interaction)
2. **Accuracy** (Faithful transcription; absolute zero hallucinated intent or unmentioned requirements)
3. **Latency** (Low end-to-end local dictation latency; empirical measurement over marketing claims)
4. **Privacy** (100% offline audio processing for core voice dictation)
5. **Security** (Strict process boundaries, safe UI Automation / SendInput injection, secure DPAPI credential storage)
6. **Maintainability** (Modular architecture, typed interfaces, clean async/.NET 9 structured concurrency)
7. **User Control** (Explicit opt-in for cloud AI; clear Prompt Diffs; undoable actions)
8. **Extensibility** (Clean protocol abstractions for ASR, LLM, and Storage)
9. **Product Quality & Polish** (Native Windows 11 Fluent/Mica feel, unobtrusive floating non-activating HUD)

---

## 2. Inviolable Core Principles

### A. LOCAL FIRST (Offline Sovereignty)
* **Zero Cloud Audio**: Audio captured from the microphone for core dictation MUST NEVER be streamed, transmitted, or logged to any cloud service.
* Voice Activity Detection (VAD) and Automatic Speech Recognition (ASR) MUST run entirely on-device using local Windows hardware acceleration (DirectML / GPU / CPU fallback).
* An offline machine must be able to perform 100% of basic dictation and language formatting.

### B. FAITHFUL (Content Lock & The No-Invention Rule)
* **Content Lock**: Speech cleanup and transformations must NEVER alter what the user meant.
* You may clean up: filler words ("um", "uh"), stutter/repetition, spoken punctuation, capitalization, obvious slips of the tongue, and backtracking ("actually Friday").
* You MUST PROTECT:
  * Requirements & specifications
  * Negative constraints ("do not use Firebase", "never", "without")
  * Technical terminology, programming languages, libraries, and frameworks
  * Variable names, code snippets, file paths (`C:\...`, relative paths), URLs, and CLI/PowerShell commands
  * Dates, numbers, currency, units, and proper nouns
* **The No-Invention Rule**: When enhancing prompts, NEVER invent unmentioned technologies, architectural choices, or arbitrary constraints. If information is missing, identify the gap—do not fill it with assumptions.

### C. NO UNINTENTIONAL SEND / SUBMIT / EXECUTE
* The text insertion engine must **NEVER** simulate Enter (`VK_RETURN`, `0x0D`), Keypad Enter, `VK_SEPARATOR`, or click any form submit, message send, or execution buttons.
* Injecting text must only place characters at the active cursor position or replace selected text. Sending messages, submitting forms, or executing commands remains strictly an explicit user action.

### D. PHASE GATING RULE
* Development proceeds strictly sequentially:
  $$\text{Specification} \longrightarrow \text{Architecture} \longrightarrow \text{Implementation} \longrightarrow \text{Unit Tests} \longrightarrow \text{Integration} \longrightarrow \text{Benchmark} \longrightarrow \text{Review} \longrightarrow \text{Next Phase}$$
* **DO NOT** begin implementing features from Phase $N+1$ until Phase $N$ has met 100% of its defined acceptance criteria and has been verified.
* Agents must not jump ahead to build flashy cloud features while local core voice foundations are unverified.

---

## 3. Strict Anti-Patterns for AI Agents

Agents operating on this repository are explicitly forbidden from:
1. **Platform Drift**: Never re-introduce macOS or Apple-specific frameworks (SwiftUI, AppKit, AXUIElement, CoreAudio, ScreenCaptureKit, WhisperKit). FLOW is strictly Windows-native.
2. **Faking Implementations**: Never write fake mock methods with hardcoded returns (e.g. `return "Send John the report tomorrow"`) and pretend the feature is implemented.
3. **Tautological Tests**: Never write tests that test nothing (e.g., asserting `true == true`). Unit tests must exercise real logic, edge cases, and failure modes.
4. **Architecture Creep**: Never swap out core frameworks (e.g., replacing .NET/C++ with Electron or introducing a Python runtime into the client bundle) without explicit human architectural review.
5. **Bypassing Safety / Content Lock**: Never bypass Content Lock validation to make a transformation "pass". If validation fails, surface the diff or reject the transformation.
6. **Unmeasured Claims**: Never assert that an engine guarantees "<400ms latency" or "<100ms startup" without attaching verifiable benchmark data.
7. **Modifying Unrelated Files**: Confine edits to the subsystem being worked on. Do not perform indiscriminate refactors of stable code.
8. **Committing Secrets or Weights**: Never commit API keys, AWS credentials, tokens, or multi-gigabyte neural network weights into git.

---

## 4. Architectural Boundaries (Windows Target)

The codebase is partitioned into isolated layers with clear dependency directions:

```
┌───────────────────────────────────────────────────────────┐
│              Presentation Layer (WinUI 3 / Win32)         │
│  - Modern Windows 11 UI (WinUI 3, Mica, Acrylic)          │
│  - Non-Activating Floating HUD (WS_EX_NOACTIVATE/TOPMOST) │
│  - System Tray NotifyIcon & Settings Panel                │
└─────────────────────────────┬─────────────────────────────┘
                              │ Depends on
┌─────────────────────────────▼─────────────────────────────┐
│              Application Core (.NET 9 / C#)               │
│  - Session Coordinator & State Machine                    │
│  - Language Engine & Deterministic Sanitizer              │
│  - Content Lock Engine (Entity Extraction & Validation)   │
│  - Storage (SQLite / Microsoft.Data.Sqlite / DPAPI)       │
│  - Optional Cloud AI Client (AWS Bedrock REST / SigV4)    │
└──────────────┬─────────────────────────────┬──────────────┘
               │ Depends on                  │ Depends on
┌──────────────▼──────────────┐┌─────────────▼──────────────┐
│  Windows Integration Layer  ││   Native Inference Engine  │
│  - Global Hotkeys           ││   - DirectML / ONNX Runtime│
│    (RegisterHotKey/Hooks)   ││   - whisper.cpp (C++/AVX2) │
│  - Audio Capture (WASAPI)   ││   - Silero VAD             │
│  - Text Insertion           ││   - GPU / NPU Acceleration │
│    (UIA + SendInput Safe)   ││   - CPU Fallback           │
│  - Windows Graphics Capture ││                            │
└─────────────────────────────┘└────────────────────────────┘
```

* The Application Core must remain decoupled from specific ASR inference backends via clean interfaces (`IASREngine`).
* All external capabilities (Audio Capture, Text Insertion, Cloud AI) MUST be defined via typed interfaces (`IAudioSource`, `ITextInsertionService`, `ICloudAIService`) to allow 100% mockability in CI and automated testing.

---

## 5. Coding Standards & Conventions

* **Language**: C# 12 / .NET 9 with nullable reference types enabled, strict async/await (`CancellationToken` propagated everywhere), and modern C++20 for native inference modules.
* **Error Handling**: Use strongly-typed exceptions or `Result<T, TError>` patterns. Never swallow exceptions with empty `catch` blocks.
* **Logging**: Use `Microsoft.Extensions.Logging` with structured logging. Never use `Console.WriteLine()` for production logging.
* **Sensitive Data Redaction**: Audio buffers, user dictation text, and clipboard contents must be marked as sensitive and redacted in persistent logs.
* **Memory & COM Resource Hygiene**: COM interfaces (`IUIAutomation`, WASAPI `IAudioClient`) must be safely released via `Marshal.ReleaseComObject` or appropriate RAII wrappers. Unmanaged audio buffers and inference sessions must be explicitly disposed.

---

## 6. Verification Checklist Before Reporting Task Complete

Before declaring any engineering task or phase complete, the agent must verify:
- [ ] Code builds cleanly with zero warnings or errors.
- [ ] All unit and integration tests pass.
- [ ] Edge cases and failure modes are explicitly tested (e.g., empty audio, microphone disconnected, un-insertable text fields, application switching, network dropouts).
- [ ] Content Lock validation assertions are verified with test vectors.
- [ ] Empirical latency, CPU, and RAM metrics are recorded.
- [ ] Invariant check: Injected text NEVER contains `VK_RETURN` or triggers automated submissions.
- [ ] Documentation updated to reflect changes.
