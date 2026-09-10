# FLOW — AI Voice Productivity Platform for macOS

[![macOS](https://img.shields.io/badge/platform-macOS%2014%2B%20%7C%20Apple%20Silicon-blue.svg)](https://apple.com)
[![Swift](https://img.shields.io/badge/Swift-6.0-orange.svg)](https://swift.org)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](THIRD_PARTY_NOTICES.md)
[![Privacy](https://img.shields.io/badge/Privacy-100%25%20Local%20Voice%20Core-brightgreen.svg)](SECURITY.md)

**FLOW** is a system-wide, voice-native AI productivity layer for macOS designed as a production-grade, local-first alternative to Wispr Flow.

It empowers users to dictate naturally into any application, clean and format speech with deterministic fidelity, reason over screen contents, and write code seamlessly—anchored in local privacy and accelerated by AWS Bedrock for on-demand cloud intelligence.

---

## 🧭 Core Philosophies

1. **LOCAL (Offline Sovereignty)**: Core voice dictation, VAD, and speech recognition operate 100% locally on Apple Silicon (Apple Neural Engine / Metal). Raw microphone audio never leaves your Mac.
2. **FAITHFUL (Content Lock)**: The AI cleans grammar, punctuation, and filler words without mutating user intent. Requirements, code, file paths, numbers, and negative constraints (*"Do not use Firebase"*) are strictly locked and verified.
3. **INTELLIGENT (Targeted Cloud Power)**: Advanced generative tasks (Prompt Engineering, Screen AI, Reply Generation) leverage Amazon Bedrock foundation models on demand.

---

## 📚 Engineering & Architecture Documentation

| Document | Description |
| :--- | :--- |
| **[AGENTS.md](AGENTS.md)** | Mandatory operating rules, anti-patterns, and coding conventions for autonomous agents and contributors. |
| **[PROJECT_PROFILE.md](PROJECT_PROFILE.md)** | Master product profile, competitive positioning vs. Wispr Flow, user flows, and system specifications. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Detailed technical system architecture covering Voice Engine, Language Engine, Content Lock, macOS Integration, and AWS Bedrock. |
| **[ROADMAP.md](ROADMAP.md)** | 12-phase sequential product roadmap with milestone deliverables and progress tracking. |
| **[SECURITY.md](SECURITY.md)** | Threat model, macOS permissions security, audio quarantine, injection safety guarantees, and Keychain storage. |
| **[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)** | Open-source licensing compliance, copyright notices, and machine learning model provenance. |
| **[TESTING_STRATEGY.md](TESTING_STRATEGY.md)** | Voice AI testing pyramid, unit test matrix, audio latency benchmark harness ($< 400\text{ ms}$ SLA), and CI specifications. |
| **[DEPENDENCIES.md](DEPENDENCIES.md)** | Dependency principles, supply chain audit, version pinning, and model weights checksums. |
| **[ACCEPTANCE_CRITERIA.md](ACCEPTANCE_CRITERIA.md)** | Quantifiable quality gates that must be satisfied prior to exiting each development phase. |

---

## 🏗️ Repository Architecture

The project is structured into modular Swift packages:

```
FLOW/
├── AGENTS.md                  # Agent operating mandate & boundaries
├── PROJECT_PROFILE.md         # Product profile & competitive analysis
├── ARCHITECTURE.md            # System architecture specification
├── ROADMAP.md                 # 12-phase development roadmap
├── SECURITY.md                # Security architecture & threat model
├── THIRD_PARTY_NOTICES.md     # Third-party notices & model provenance
├── TESTING_STRATEGY.md        # Testing pyramid & latency benchmarks
├── DEPENDENCIES.md            # Dependency audit & version pinning
├── ACCEPTANCE_CRITERIA.md     # Quality gates per phase
├── packages/
│   ├── FlowCore/              # Platform-agnostic core logic
│   │   ├── Package.swift
│   │   ├── Sources/
│   │   │   ├── AudioProcessing/   # VAD, ring buffers, noise gate
│   │   │   ├── ASR/               # Speech recognition protocols & engines
│   │   │   ├── LanguageEngine/    # Spoken punctuation & backtracking
│   │   │   ├── ContentLock/       # Entity extraction, diff & validation
│   │   │   ├── CloudBedrock/      # AWS Bedrock SigV4 client
│   │   │   ├── Storage/           # SQLite / GRDB local persistence
│   │   │   └── DeveloperMode/     # Casing transforms & code formatting
│   │   └── Tests/
│   └── FlowMacOS/             # Native macOS AppKit/SwiftUI host app
│       ├── Package.swift
│       ├── Sources/
│       │   ├── App/               # NSStatusItem, MenuBar, Floating HUD
│       │   ├── Hotkey/            # CGEventTap global hotkey daemon
│       │   ├── AudioCapture/      # CoreAudio / AVAudioEngine capture
│       │   ├── Insertion/         # AXUIElement & CGEvent text injector
│       │   ├── ScreenCapture/     # ScreenCaptureKit region selector
│       │   └── Permissions/       # PermissionManager & onboarding
│       └── Tests/
├── infra/
│   └── aws/                   # CloudFormation / CDK for Bedrock proxy
└── tools/
    ├── scripts/               # Latency benchmarking & model downloaders
    └── test_fixtures/         # Calibrated audio WAVs & test vectors
```

---

## ⚡ Current Status

* **Active Phase**: **Phase 0 (Foundation)** complete.
* **Next Target**: **Phase 1 (Voice Core)** — Global Hotkey $\longrightarrow$ Mic Capture $\longrightarrow$ Silero VAD $\longrightarrow$ Local ASR $\longrightarrow$ Universal Cursor Insertion.
