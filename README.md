# FLOW — System-Wide Voice Productivity for Windows

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-blue.svg)](https://microsoft.com)
[![Runtime](https://img.shields.io/badge/.NET-9.0%20Native%20x64-purple.svg)](https://dotnet.microsoft.com)
[![Inference](https://img.shields.io/badge/ASR-Whisper.net%20%7C%20AVX2%20%7C%20DirectML-green.svg)](https://github.com/sandrohanea/whisper.net)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](THIRD_PARTY_NOTICES.md)
[![Privacy](https://img.shields.io/badge/Privacy-100%25%20Local%20%7C%20Zero%20Cloud-success.svg)](SECURITY.md)

**FLOW** is a production-grade, local-first, system-wide voice productivity platform for Windows 10 and 11. Designed as an offline-sovereign alternative to Wispr Flow, FLOW enables users to dictate naturally into any application, text box, code editor, or browser with sub-second responsiveness and zero cloud audio transmission.

---

## 🌟 Core Product Features

FLOW provides a consumer-grade desktop experience centered around the **FLOW Hub** and the lightweight floating **Flow Bar**:

### 1. Unified FLOW Hub (`FlowHubWindow`)
- **⌂ Home**: Real-time status indicator (`● FLOW Active & Ready`), active microphone endpoint, push-to-talk hotkey card, live audio meter, quick dictation test area, deterministic productivity KPIs (Words today, Average WPM, Sessions today, Daily streak), and recent dictations feed.
- **◷ History**: SQLite FTS5 full-text search across all recorded transcripts, application and date filters, favorites toggle, one-click clipboard copying, soft-delete, and export to JSON, CSV, and Plain Text.
- **◇ Dictionary**: "Teach FLOW your words" — custom vocabulary terms, acronyms, and phonetic misheard-word corrections (e.g. `cube netties` $\rightarrow$ `Kubernetes`) backed by local SQLite storage.
- **▣ Snippets**: "Save text you use again and again" — short spoken trigger phrases that expand into rich multiline templates or links.
- **✦ Styles**: Writing style formatting cards (**Natural / Balanced**, **Formal / Professional**, **Casual / Conversational**, **Concise / Bulleted**).
- **▤ Scratchpad**: Lightweight distraction-free note capture workspace with real-time debounced autosave ("Saved ✓"), note pinning (`📌`), and markdown export.
- **⚙ Settings**: Consolidated settings covering WASAPI microphone endpoints, audio input sensitivity, dictation hotkeys, languages (English, Tamil, Hindi, Spanish, French, German, Auto-Detect), and local privacy retention policies.
- **? About**: Application metadata, version v1.0.0, architecture specifications, and invariant guarantees.

### 2. Floating Flow Bar (`FloatingHudController`)
- Ultra-lightweight native Win32 window (`WS_EX_NOACTIVATE | WS_EX_TOPMOST`).
- Completely non-activating: never steals focus or interferes with the target application cursor (`HTTRANSPARENT`).
- Real-time animated audio RMS waveform visualization during active speech.

---

## 🔒 Inviolable Safety & Architecture Principles

1. **100% Offline Sovereignty**: Audio captured from the physical microphone is processed strictly on-device using local Whisper GGML weights (`ggml-tiny.en.bin` and multilingual models). Raw audio buffers are never transmitted over the network or logged to disk.
2. **Inviolable Zero-Enter Invariant**: FLOW's text insertion engine strictly prevents simulation of `VK_RETURN` (`0x0D`), keypad enter, or form submissions. The user retains sole authority over sending messages, submitting forms, or executing commands.
3. **Execution Isolation**: Dictation contains zero calls to execution primitives (`Process.Start`, `CreateProcess`, `ShellExecute`, `cmd.exe`, `powershell.exe`).
4. **Privacy-Gated UI Automation**: Detection of password fields (`IsPassword == true`) immediately triggers a fail-closed privacy gate, blocking dictation to prevent accidental credential leakage.

---

## 🚀 Keyboard Shortcuts

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| **Hold [Right Alt]** | Push-to-Talk | Hold to record speech; release to format and insert text at active cursor. |
| **Double-Tap [Right Alt]** | Hands-Free Toggle | Double-tap to start continuous dictation; tap again to stop. |
| **[Shift + Right Alt]** | Backtrack Undo | Instantly reverts the previous text insertion in the active target window. |
| **[Escape]** | Cancel Dictation | Immediately aborts the active recording session without inserting text. |

---

## 🛠️ Building and Running

### Prerequisites
- Windows 10/11 x64
- .NET 9.0 SDK
- Visual C++ Redistributable (x64)

### Build
```powershell
# Restore dependencies and build Release
dotnet build Flow.sln -c Release
```

### Run All Tests
```powershell
dotnet test Flow.sln -c Release
```

### Publish Self-Contained Executable
```powershell
dotnet publish src/Flow.Host.Windows/Flow.Host.Windows.csproj -c Release -r win-x64 --self-contained -o artifacts/local-test/win-x64
```

The resulting executable `artifacts\local-test\win-x64\Flow.Host.Windows.exe` runs completely standalone without requiring an external .NET runtime or Visual Studio installation.
