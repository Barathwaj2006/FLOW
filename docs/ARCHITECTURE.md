# FLOW — System Architecture

> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Framework**: .NET 9.0 (`net9.0-windows10.0.19041.0`) with C++20 Inference  
> **Packaging**: Self-Contained Native Executable (`win-x64`)  

---

## 1. Architectural Overview

FLOW is designed using a strictly layered, decoupled architecture that enforces complete local sovereignty and zero-cloud dependency for voice dictation.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Presentation Layer (Flow.Host.Windows)               │
│  - FLOW Hub Window (WPF, Segoe UI Variable, Modern Dark Fluent UI)     │
│  - Flow Bar / Floating HUD (Native Win32, WS_EX_NOACTIVATE/TOPMOST)   │
│  - Windows Notification Tray (Win32 Shell_NotifyIcon + Custom GDI Icon)│
│  - Single-Instance Coordinator (Named Mutex + Activation Broadcast)    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│                       Application Core (Flow.Core)                     │
│  - Voice Session Coordinator & State Machine (Idle, Rec, Proc, Insert) │
│  - Multi-Pass Formatting Pipeline & Deterministic Text Sanitizer       │
│  - Personalization Engines (Dictionary, Snippets, Writing Styles)      │
│  - Context Awareness & Application Classifier (UIA Safe Inspection)    │
│  - History & Productivity Statistics Services                          │
│  - Scratchpad Workspace & Persistence Engine                           │
│  - Local SQLite Storage (WAL Mode + FTS5 Full-Text Search)             │
└───────────────────┬────────────────────────────────┬───────────────────┘
                    │                                │
┌───────────────────▼──────────────┐ ┌───────────────▼───────────────────┐
│     Windows Native Interop       │ │     Local Inference Engine        │
│  - Global Low-Level Keyboard     │ │       (Flow.Inference)            │
│    Hook (WH_KEYBOARD_LL)         │ │  - Whisper.net (C++ Native)       │
│  - WASAPI Audio Capture          │ │  - GGML Tiny.en & Multilingual    │
│    (16kHz Float32 Mono Stream)   │ │  - Energy Voice Activity Detector │
│  - Text Insertion Service        │ │  - Hardware Acceleration          │
│    (UIA Direct + SendInput Safe) │ │    (AVX2 / DirectML / CPU Fallback│
└──────────────────────────────────┘ └───────────────────────────────────┘
```

---

## 2. End-to-End Dictation Pipeline

When the user initiates dictation, the audio and text flow through the following deterministic pipeline:

```
[ User Action: Hold Right Alt ]
              │
              ▼
[ GlobalHotkeyHook ] ──(Low-Level WH_KEYBOARD_LL, physical key state check, injected input rejection)
              │
              ▼
[ VoiceSessionCoordinator.StartSessionAsync() ]
              │
              ├──► [ FloatingHudController.Show() ] (State = Recording, Red Indicator)
              └──► [ WasapiAudioCapture.Start() ] (Captures 16kHz float32 audio into AudioRingBuffer)
              │
[ User Action: Release Right Alt ]
              │
              ▼
[ WasapiAudioCapture.Stop() ]
              │
              ▼
[ EnergyVAD.Process() ] ──► (Trims leading/trailing silence, validates speech presence)
              │
              ▼
[ WhisperNetInferenceEngine.TranscribeAsync() ] ──► (100% Local GGML model inference)
              │
              ▼
[ TranscriptProcessingPipeline ]
              │
              ├── 1. Whitespace & Casing Normalization
              ├── 2. Spoken Punctuation Formatting ("period" -> ".", "comma" -> ",")
              ├── 3. Snippets Expansion (ISnippetRepository)
              ├── 4. Personal Dictionary & Phonetic Corrections (IPersonalDictionaryRepository)
              ├── 5. Conservative Filler Word Removal ("um", "uh")
              ├── 6. Writing Style Polishing (IStyleRepository: Natural, Formal, Casual)
              └── 7. Zero-Enter Invariant Enforcer (Neutralizes \r, \n, and VK_RETURN)
              │
              ▼
[ WindowsTextInsertionService.InsertTextAsync() ]
              │
              ├── Priority 1: UIAutomation ValuePattern / TextPattern insertion directly into cursor
              └── Fallback: Safe SendInput (Ctrl+V) with clipboard preservation and zero Enter keys
              │
              ▼
[ SqliteHistoryRepository.InsertAsync() ] ──► (Saves session, word count, application, transcript to SQLite)
              │
              ▼
[ VoiceSessionCoordinator.ResetToIdle() ] ──► [ FloatingHudController.Hide() ]
```

---

## 3. Inviolable Safety Principles & Invariants

1. **Zero-Enter Invariant**:
   - The text insertion engine enforces that `VK_RETURN` (0x0D), `VK_SEPARATOR`, or keypad enter are NEVER simulated.
   - Any accidental newline characters in transcription are transformed into spaces or managed formatting without emitting enter keys.
2. **Offline Audio Sovereignty**:
   - Zero cloud audio streaming. Audio buffers remain strictly in memory and are discarded after transcription.
3. **Execution Isolation**:
   - FLOW contains zero calls to `Process.Start`, `CreateProcess`, `ShellExecute`, or command shells (`cmd.exe`, `powershell.exe`) in the dictation path.
4. **UIAutomation Fail-Closed Privacy**:
   - Password fields (`IsPassword == true`) and sensitive controls automatically trigger an immediate privacy gate, suppressing dictation recording and context capture.

---

## 4. Storage Architecture

All user data is stored locally in `%LOCALAPPDATA%\FLOW\flow_personalization.db`:
- **SQLite Engine**: Microsoft.Data.Sqlite with Write-Ahead Logging (`PRAGMA journal_mode = WAL;`) and synchronous normal for high write throughput.
- **Full-Text Search (FTS5)**: History entries and Scratchpad notes are indexed using SQLite FTS5 for sub-5ms search across tens of thousands of records.
- **Relational Integrity**: Foreign keys enabled (`PRAGMA foreign_keys = ON;`).
