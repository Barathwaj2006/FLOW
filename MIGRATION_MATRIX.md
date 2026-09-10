# MIGRATION_MATRIX.md — Platform Realignment Audit & Asset Classification

> **Audit Date**: 2026-09-10  
> **Directive**: Realignment of FLOW from macOS-centric architecture to Windows-native platform (Windows 10/11 x64).  
> **Status**: Audit & Classification Complete — Implementation Halted.  

---

## 1. Classification Taxonomy

Every artifact currently residing in the FLOW repository has been audited and assigned to one of four classifications:

* **Class A: Platform-Independent & Reusable**  
  Domain concepts, product logic, validation rules, test vectors, and architectural tenets that apply universally regardless of operating system.
* **Class B: Platform-Independent but Requiring Modification**  
  Documents, specifications, test runners, or data structures that express valid cross-platform concepts but contain hardcoded references to Apple/macOS technologies (e.g., mentioning CoreAudio, AXUIElement, or macOS keyboard shortcuts).
* **Class C: macOS-Specific & Obsolete for Production**  
  Code, package manifests, and drivers written in Swift targeting macOS-only frameworks (AppKit, SwiftUI, AXUIElement, CGEventTap, ScreenCaptureKit, WhisperKit, SFSpeechRecognizer). These cannot run on Windows and are designated for retirement/archival.
* **Class D: Unknown / Requires Human Architectural Review**  
  Assets where cross-platform viability or licensing in a Windows environment requires experimental benchmarking or legal clarification.

---

## 2. Complete Repository Audit & Classification Inventory

### Root & Governance Documentation

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `AGENTS.md` | **Class B** | Agent operating mandate, anti-patterns, and coding conventions. Currently references Swift 6 and macOS AXUIElement. | Re-author for Windows-first development (.NET / C++), Win32 / UI Automation safety, and `VK_RETURN` blacklisting. | **Realign** |
| `PROJECT_PROFILE.md` | **Class B** | Master product profile, competitive positioning vs. Wispr Flow, core tenets. References Apple Silicon, macOS 14+. | Realign for Windows 10/11 x64, WinUI 3, .NET, DirectML/whisper.cpp, and Windows UIA. | **Realign** |
| `ARCHITECTURE.md` | **Class B** | Technical architecture specification. Heavily references AVAudioEngine, AXUIElement, CGEventTap, ScreenCaptureKit. | Complete rewrite around Option C: WinUI 3 Presentation, .NET Core, Windows Integration (WASAPI, UIA, SendInput), Native DirectML/C++. | **Realign** |
| `ROADMAP.md` | **Class B** | 12-phase product roadmap. Contains macOS milestones and exit gates. | Rebase roadmap around Windows milestones and testing targets (Notepad, VS Code, Windows Terminal). | **Realign** |
| `SECURITY.md` | **Class B** | Threat model and security policies. Mentions macOS permissions (`AXIsProcessTrusted`, `AVCaptureDevice`). | Realign for Windows UIA privilege isolation, Windows privacy settings, DPAPI encryption, and SendInput clipboard hygiene. | **Realign** |
| `THIRD_PARTY_NOTICES.md` | **Class B** | Open source licensing notices. Currently lists WhisperKit, Sparkle, macOS libraries. | Realign to inventory Windows libraries: Windows App SDK, .NET, ONNX Runtime DirectML, whisper.cpp, Silero VAD, SQLite. | **Realign** |
| `TESTING_STRATEGY.md` | **Class B** | Testing pyramid and benchmarks. References macOS `swift test` and AXUIElement test targets. | Realign for .NET / C++ unit testing, WASAPI device switching tests, Windows UI Automation test harness. | **Realign** |
| `DEPENDENCIES.md` | **Class B** | Dependency manifest. Pinned to Swift Package Manager packages (WhisperKit, GRDB.swift). | Realign for NuGet / native C++ dependencies (.NET 9, Windows App SDK, Microsoft.Data.Sqlite, ONNX Runtime). | **Realign** |
| `ACCEPTANCE_CRITERIA.md` | **Class B** | Phase-by-phase exit gates. References TextEdit, Apple Silicon latency budgets. | Realign with Windows test targets: Windows 11, Notepad, VS Code, Chrome, Edge, Word, Windows Terminal. | **Realign** |
| `README.md` | **Class B** | Repository entry point. Mentions macOS badges, Swift, Apple Silicon. | Realign for Windows 10/11, .NET, WinUI 3, DirectML. | **Realign** |

---

### Specifications & Reports (`docs/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `docs/PHASE_1_BENCHMARK_REPORT.md` | **Class C** | Benchmark report comparing AppleSpeechEngine and WhisperKit on Apple Silicon. | Obsolete for Windows production. Preserved in git history or archived as legacy macOS benchmark. | **Archive / Retain for Reference** |

---

### Core Logic Package (`packages/FlowCore/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `packages/FlowCore/Package.swift` | **Class C** | Swift Package Manager manifest. | Obsolete on Windows. Replaced by .NET Solution (`Flow.sln`, `Flow.Core.csproj`). | **Retire** |
| `packages/FlowCore/Sources/FlowCore.swift` | **Class B** | Subsystem logging and versioning in Swift. | Port conceptual logging structure to .NET `Microsoft.Extensions.Logging`. | **Retire Swift / Port to .NET** |
| `packages/FlowCore/Sources/AudioProcessing/AudioBuffer.swift` | **Class A** | Standardized 16kHz Float32 mono audio buffer definition. | Conceptually reusable 100%. Target: C# struct/record `AudioBuffer`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/AudioProcessing/AudioRingBuffer.swift` | **Class A** | Thread-safe circular audio buffer for rolling recording. | Algorithm is platform-independent. Target: C# `AudioRingBuffer`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/AudioProcessing/VADProtocol.swift` | **Class A** | Interface for Voice Activity Detection. | Pure interface. Target: C# `IVoiceActivityDetector`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/AudioProcessing/EnergyVAD.swift` | **Class A** | RMS energy + zero-crossing rate silence detector. | Deterministic math. Target: C# `EnergyVAD`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/ASR/ASREngineProtocol.swift` | **Class A** | Abstract speech recognition interface and models. | Pure abstraction. Target: C# `IASREngine`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/ASR/ASREngineRegistry.swift` | **Class A** | Multi-engine fallback escalation registry. | Pure coordination logic. Target: C# `ASREngineRegistry`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/ASR/AppleSpeechEngine.swift` | **Class C** | Apple SFSpeechRecognizer driver (`#if os(macOS)`). | Incompatible with Windows. Replaced by DirectML / whisper.cpp. | **Retire** |
| `packages/FlowCore/Sources/ASR/WhisperCppEngine.swift` | **Class B** | Whisper.cpp wrapper. Mentions Metal GPU mode. | Adapt concept to Windows whisper.cpp (CUDA / DirectML / AVX2). | **Port concept to .NET/C++** |
| `packages/FlowCore/Sources/ASR/MockASREngine.swift` | **Class A** | Deterministic mock engine for testing. | Reusable testing pattern. Target: C# `MockASREngine`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/LanguageEngine/DeterministicTextSanitizer.swift`| **Class A** | Safe sentence capitalization and terminal punctuation. | Rule-based regex logic. Target: C# `DeterministicTextSanitizer`.| **Port concept to .NET** |
| `packages/FlowCore/Sources/LanguageEngine/LanguageEngineProtocol.swift` | **Class A** | Spoken punctuation and cleanup contract. | Pure contract. Target: C# `ILanguageEngine`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/LanguageEngine/RuleBasedLanguageEngine.swift` | **Class A** | Spoken punctuation, filler removal, backtracking parser. | Deterministic string algorithms. Target: C# `RuleBasedLanguageEngine`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/ContentLock/ContentLockProtocol.swift` | **Class A** | Protected entity extraction and validation models. | Pure domain model. Target: C# `IContentLockEngine`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/DeveloperMode/CasingStyle.swift` | **Class A** | camelCase, snake_case, PascalCase, kebab-case converter. | Pure algorithmic string utility. Target: C# `CasingTransformer`. | **Port concept to .NET** |
| `packages/FlowCore/Sources/Session/VoiceSessionCoordinator.swift` | **Class A** | State machine actor coordinating recording lifecycle. | Concurrency pattern. Target: C# async coordinator class / channel. | **Port concept to .NET** |
| `packages/FlowCore/Tests/*` | **Class B** | Unit tests for ring buffer, VAD, casing, and 12 reliability vectors. | Reusable test cases and assertions. Port to xUnit / NUnit. | **Port concept to .NET** |

---

### macOS Host Package (`packages/FlowMacOS/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `packages/FlowMacOS/Package.swift` | **Class C** | Swift Package Manager manifest. | Incompatible with Windows. | **Retire** |
| `packages/FlowMacOS/Sources/FlowMacOS.swift` | **Class C** | AppKit host initialization and logging. | Replaced by `Flow.Host.Windows` (WinUI 3 / Win32). | **Retire** |
| `packages/FlowMacOS/Sources/AudioCapture/AudioCaptureEngine.swift` | **Class C** | AVAudioEngine input tap and format converter. | Incompatible with Windows. Replaced by WASAPI (`IAudioClient3`). | **Retire** |
| `packages/FlowMacOS/Sources/Hotkey/GlobalHotkeyManager.swift` | **Class C** | NSEvent global monitor / Carbon hotkeys. | Incompatible with Windows. Replaced by `RegisterHotKey` / `WH_KEYBOARD_LL`. | **Retire** |
| `packages/FlowMacOS/Sources/Insertion/NativeTextInsertionService.swift`| **Class C** | AXUIElement direct injection with NSPasteboard fallback. | Incompatible with Windows. Replaced by Windows UI Automation + `SendInput`. | **Retire** |
| `packages/FlowMacOS/Sources/Insertion/TextInsertionServiceProtocol.swift`| **Class A** | Contract for text insertion and editable target detection. | Pure abstraction. Target: C# `ITextInsertionService`. | **Port concept to .NET** |
| `packages/FlowMacOS/Sources/Insertion/MockTextInsertionService.swift` | **Class A** | Mock text inserter recording injection history. | Reusable testing pattern. Target: C# `MockTextInsertionService`. | **Port concept to .NET** |
| `packages/FlowMacOS/Sources/Permissions/PermissionManager.swift` | **Class C** | AVCaptureDevice & AXIsProcessTrusted check. | Incompatible with Windows. Replaced by Windows Microphone Privacy check. | **Retire** |
| `packages/FlowMacOS/Sources/UI/FloatingHUDController.swift` | **Class C** | AppKit NSPanel non-activating floating window. | Incompatible with Windows. Replaced by WinUI 3 / Win32 topmost tool window. | **Retire** |
| `packages/FlowMacOS/Tests/*` | **Class C** | Tests invoking Swift AppKit / FlowMacOS stubs. | Replaced by Windows UI Automation integration tests. | **Retire** |

---

### Cloud & Shared Infrastructure (`infra/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `infra/aws/template.yaml` | **Class A** | AWS SAM template for API Gateway + Lambda Bedrock proxy. | Fully platform-independent serverless infrastructure. | **Preserve** |
| `infra/aws/lambda/handler.py` | **Class A** | Python Lambda handler invoking Amazon Bedrock. | Fully platform-independent cloud service. | **Preserve** |

---

### Tools & Test Fixtures (`tools/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `tools/test_fixtures/README.md` | **Class A** | Documentation of test vectors and ground truths. | Platform-independent. | **Preserve** |
| `tools/test_fixtures/entities.json` | **Class A** | Test vectors for Content Lock and No-Invention Rule. | Platform-independent. | **Preserve** |
| `tools/test_fixtures/punctuation.json` | **Class A** | Test vectors for spoken punctuation and cleanup. | Platform-independent. | **Preserve** |
| `tools/test_fixtures/speech_corpus.json` | **Class A** | Test vectors for WER and latency benchmarking. | Platform-independent. | **Preserve** |
| `tools/scripts/download_models.sh` | **Class B** | Bash model downloader. | Adapt for Windows (`download_models.ps1` / PowerShell). | **Realign** |
| `tools/scripts/benchmark_latency.swift` | **Class C** | Swift latency simulation script. | Replace with .NET BenchmarkDotNet / C# harness. | **Retire / Replace** |
| `tools/benchmarks/LatencyBenchmarkHarness.swift` | **Class C** | Swift percentile benchmark script. | Replace with C# latency benchmark tool. | **Retire / Replace** |
| `tools/benchmarks/WERBenchmark.swift` | **Class C** | Swift WER Levenshtein benchmark script. | Replace with C# WER evaluation script. | **Retire / Replace** |

---

### CI Configuration (`.github/`)

| File Path | Classification | Current State / Role | Proposed Windows Target / Replacement | Action |
| :--- | :--- | :--- | :--- | :--- |
| `.github/workflows/ci.yml` | **Class B** | GitHub Actions CI targeting `macos-14` runner. | Realign to target `windows-latest` running .NET test runner. | **Realign** |

---

## 3. Preservation and Disposal Strategy

1. **No Destructive Bulk Deletion**: The existing Swift codebase (`packages/FlowCore`, `packages/FlowMacOS`) will be formally marked as **Retired (Legacy macOS Prototype)** in the documentation. We will NOT delete files destructively until the Windows solution structure is initialized and approved.
2. **Concept Extraction**: All algorithmic logic developed in Phase 0/1 (`DeterministicTextSanitizer`, `RuleBasedLanguageEngine`, `CasingTransformer`, `EnergyVAD`, `AudioRingBuffer`, `VoiceSessionCoordinator`, and the 12 reliability test scenarios) will be ported into .NET during Phase 1 under the approved Windows architecture.
3. **Specification Realignment**: All 10 root engineering specifications are rewritten immediately to establish the authoritative Windows architecture.
