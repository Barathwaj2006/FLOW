# FLOW — Release Notes (v1.0.0 GA)

> **Release Version**: v1.0.0 General Availability  
> **Target Architecture**: Windows 10/11 x64 Native Desktop  
> **Release Date**: September 17, 2026  
> **Package**: `artifacts/release/FLOW-v1.0.0-win-x64.zip` (81.0 MB)  
> **Executable**: `artifacts/final/win-x64/Flow.Host.Windows.exe`  

---

## Highlights & Key Capabilities

FLOW v1.0.0 is a production-grade, system-wide AI voice productivity platform built natively for Windows 10/11 x64. It is designed to be a local-first, low-latency competitor to Wispr Flow.

### 1. 100% Offline Sovereignty (Zero Cloud Audio)
- Automatic Speech Recognition (ASR) and Voice Activity Detection (VAD) run completely on-device using local DirectML acceleration and CPU fallback.
- Audio captured from the microphone is processed purely in RAM and purged immediately after transcription. Zero audio is stored to disk or transmitted to any cloud servers.

### 2. Inviolable Safety Invariant (Zero Accidental Send)
- Text insertion safely places characters at the active cursor position.
- FLOW is mathematically and architecturally prohibited from simulating Enter (`VK_RETURN`, `0x0D`), Keypad Enter, or submitting messages/forms.

### 3. Native Windows 11 Fluent UX
- **Obsidian Amber Floating HUD**: Docks cleanly above the taskbar on the active display. Uses `WS_EX_NOACTIVATE` and `WS_EX_TOPMOST` with a non-activating click handler, allowing users to toggle dictation via mouse without stealing focus from active editors.
- **Dual Shortcuts**:
  - **Hold-to-Talk**: Hold `Alt + Space`, speak, and release to insert.
  - **Hands-Free Mode**: Press `Alt + B` to toggle recording on and off.
  - **Safe Cancel**: Press `Esc` anytime to cancel active dictation.

### 4. Personalization & Intelligence
- **Personal Vocabulary & Custom Replacements**: Dynamic dictionary rules with phonetics, domain categorizations, and live in-place editing.
- **Text Sanitizer**: Deterministic spoken punctuation parsing, filler word suppression, and casing transforms (`camelCase`, `PascalCase`, `snake_case`, etc.).
- **Snippets**: Spoken trigger expansion into boilerplate paragraphs.
- **Cursor-Independent Transcript Preservation**: If window focus changes mid-sentence, text insertion safely aborts to prevent misdirection while preserving the full transcript in `LastTranscript` and local SQLite History.

### 5. Local HTTP API Bridge & Frontend Integration
- **Local Zero-Dependency Server**: Non-blocking `LocalApiServer` on `http://127.0.0.1:5005` connecting web and native desktop interfaces directly to local Whisper.net inference.
- **100% Offline Web Dictation**: React UI records PCM/WAV and posts to `/api/transcribe` for real-time local Whisper inference with AVX2 and DirectML acceleration.
- **Logarithmic dBFS Audio Meter**: Calibrated true RMS-to-dBFS visualization with natural speech sensitivity.
- **HUD Error State & Retry**: Real-time error handling with visual notification and retry loop in the floating obsidian HUD.

### 6. Account & Voice Credits Management
- Passwordless 6-digit email verification login.
- Cloud AI Credits balance for optional cloud prompt transformations and style synchronization.
- Secured using Windows DPAPI (`DataProtectionScope.CurrentUser`) for cryptographic credential protection at rest.

---

## Test & Validation Metrics

| Suite | Tests | Result | Status |
| :--- | :--- | :--- | :--- |
| **`Flow.Core.Tests`** | 3,702 tests | 3,702 Passed, 0 Failed, 0 Skipped | **100% Pass** |
| **`Flow.Windows.Tests`** | 203 tests | 203 Passed, 0 Failed, 0 Skipped | **100% Pass** |
| **Total Test Suite** | **3,905 tests** | **3,905 / 3,905 Passed** | **100% Pass** |
| **TypeScript / React** | `npm run lint` | 0 Errors, 0 Warnings | **Pass** |
| **Vite Production** | `npm run build` | Built in 1.25s | **Pass** |

---

## Checksums & Artifacts

- **Zip Package**: `artifacts/release/FLOW-v1.0.0-win-x64.zip`
- **Release Directory**: `artifacts/release/FLOW-v1.0.0-win-x64/`
- **Binary**: `artifacts/final/win-x64/Flow.Host.Windows.exe`
