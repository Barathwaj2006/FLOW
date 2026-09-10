# FLOW — Windows-Native AI Voice Productivity Platform

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-blue.svg)](https://microsoft.com)
[![Runtime](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com)
[![UI](https://img.shields.io/badge/UI-WinUI%203-0078D4.svg)](https://github.com/microsoft/WindowsAppSDK)
[![AI](https://img.shields.io/badge/Inference-DirectML%20%7C%20ONNX%20Runtime-green.svg)](https://github.com/microsoft/onnxruntime)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](THIRD_PARTY_NOTICES.md)
[![Privacy](https://img.shields.io/badge/Privacy-100%25%20Local%20Voice%20Core-success.svg)](SECURITY.md)

**FLOW** is a system-wide, voice-native AI productivity layer for Windows 10 and 11, designed as a production-grade, local-first alternative to Wispr Flow.

It empowers users to speak naturally and turn their voice, screen, or selected context into accurate text, replies, prompts, translations, code, and AI-assisted output—anchored in local privacy and protected by **Content Lock** to prevent AI from mutating the user's intent.

---

## 🧭 Core Architectural Invariants

1. **LOCAL FIRST (Offline Sovereignty)**: Core voice dictation, Voice Activity Detection (VAD), and speech recognition operate 100% locally on Windows hardware via DirectML and AVX2 CPU fallbacks. Raw microphone audio never leaves your machine.
2. **FAITHFUL (Content Lock)**: The AI cleans grammar, punctuation, and filler words without mutating user intent. Technical terms, code, numbers, file paths (`C:\...`), and negative constraints (*"Do not use Firebase"*) are strictly locked and verified.
3. **USER CONTROL (Zero Enter / No Accidental Send)**: Text injection places characters at the active cursor position. The system strictly blacklists `VK_RETURN` (`0x0D`) and keypad enter, guaranteeing that dictation never submits a form, sends a message, or executes a shell command.
4. **INTELLIGENT (Targeted Cloud Power)**: Advanced generative tasks (Prompt Engineering, Screen AI, Reply Generation) leverage Amazon Bedrock foundation models strictly on demand.

---

## 📚 Windows Engineering Specifications

| Document | Purpose & Scope |
| :--- | :--- |
| **[MIGRATION_MATRIX.md](MIGRATION_MATRIX.md)** | Full repository audit, classification (Classes A/B/C/D), and realignment strategy from legacy macOS prototype to Windows-native platform. |
| **[AGENTS.md](AGENTS.md)** | Mandatory operating mandate, anti-patterns, coding conventions, and text insertion safety rules for AI agents and contributors. |
| **[PROJECT_PROFILE.md](PROJECT_PROFILE.md)** | Master product profile, competitive positioning vs. Wispr Flow on Windows, core tenets, and technology evaluation status. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Deep technical architecture across all subsystems: WinUI 3, .NET 9 Core, WASAPI Audio, Windows UI Automation, SendInput, DirectML, and AWS Bedrock. |
| **[ROADMAP.md](ROADMAP.md)** | 12-phase sequential engineering roadmap from Phase 0 Realignment to Phase 11 Commercial Scale. |
| **[SECURITY.md](SECURITY.md)** | Windows threat model, volatile RAM audio quarantine, UIPI privilege boundaries, DPAPI encryption, and insertion safety rules. |
| **[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)** | Open-source licensing compliance, copyright notices, and model provenance for the Windows ecosystem. |
| **[TESTING_STRATEGY.md](TESTING_STRATEGY.md)** | Windows audio test matrix (WASAPI shared/exclusive, device switching), UI Automation target testing, and empirical latency benchmarking (P50/P95/P99). |
| **[DEPENDENCIES.md](DEPENDENCIES.md)** | Windows dependency manifest (.NET 9, Windows App SDK, DirectML, whisper.cpp, Silero VAD, SQLite) with licensing and provenance. |
| **[ACCEPTANCE_CRITERIA.md](ACCEPTANCE_CRITERIA.md)** | Strict quantifiable quality gates across Windows 11 target applications (Notepad, VS Code, Chrome, Edge, Word, Windows Terminal). |
| **[WEBSITE_GUIDELINES.md](WEBSITE_GUIDELINES.md)** | Anti-AI-slop rules, visual quality standards, copy honesty, and pre-launch quality gates for public web presence. |

---

## 🏗️ Target Windows Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    WinUI 3 Presentation                      │
│      HUD Panel │ Settings Window │ System Tray NotifyIcon     │
├──────────────────────────────────────────────────────────────┤
│                    .NET 9 Application Core                   │
│   Session Coordinator │ Language Engine │ Content Lock       │
│   Developer Mode      │ Storage (DPAPI) │ AWS Bedrock Client │
├──────────────────────────────┬───────────────────────────────┤
│    Windows Integration       │     Native Inference Engine   │
│  WASAPI Audio Capture        │  ONNX Runtime / DirectML      │
│  UI Automation & SendInput   │  whisper.cpp (AVX2 CPU)       │
│  Windows Graphics Capture    │  Silero VAD (ONNX)            │
└──────────────────────────────┴───────────────────────────────┘
```

---

## ⚡ Current Status

* **Active Stage**: **Phase 0 — Windows Architecture Realignment** Complete.
* **Execution Status**: **AUDIT + DOCUMENTATION ONLY — Implementation Authorization NOT GRANTED**.
* **Next Target**: Awaiting human approval of Phase 0 Windows realignment before Phase 1 Voice Core implementation commences.
