# TESTING_STRATEGY.md — Quality Assurance & Verification Strategy (Windows Native)

> **Status**: Windows Realignment Baseline  
> **Target Platform**: Windows 10/11 x64 Native Desktop Application  
> **Mandate**: Every subsystem must be empirically verified across Windows hardware and target applications. No phase may exit without passing automated unit, integration, and reliability suites.  

---

## 1. The Windows Voice AI Testing Pyramid

```
                  ┌────────────────────────┐
                  │   Windows UI & Target  │  Manual & Automation Tests
                  │    Integration Tests   │  (Notepad, VS Code, Word, Chrome)
                  ├────────────────────────┤
                  │ Empirical Benchmarks   │  P50/P95/P99 Latency, WER,
                  │ (Latency, CPU, RAM)    │  GPU/VRAM & WASAPI Jitter
                  ├────────────────────────┤
                  │ Content Lock & Safety  │  Negative Constraint Vectors &
                  │      Verification      │  Inviolable VK_RETURN Blacklist
                  ├────────────────────────┤
                  │ Component Unit Tests   │  C# Language Engine, Casing,
                  │ (xUnit / NUnit)        │  VAD Math, Ring Buffer, SQLite
                  └────────────────────────┘
```

---

## 2. Windows Audio & WASAPI Test Matrix

Audio capture on Windows must remain stable across diverse hardware configurations:

| Test Case | Scenario | Expected Behavior |
| :--- | :--- | :--- |
| **TC-AUDIO-01** | Standard 48kHz / 44.1kHz USB Microphone | Resampler converts audio cleanly to 16kHz mono Float32 without audio clipping. |
| **TC-AUDIO-02** | Bluetooth Hands-Free Device (mSBC / CVSD) | Handles packet jitter, sample rate constraints, and latency shifts. |
| **TC-AUDIO-03** | Default Device Switch Mid-Session | Detects `IMMNotificationClient` change, re-initializes WASAPI capture cleanly. |
| **TC-AUDIO-04** | Physical Microphone Disconnect | Capture engine flags device loss, logs structured warning, avoids hanging thread. |
| **TC-AUDIO-05** | Microphone Reconnect | Automatically binds to the newly restored default input device on next session. |
| **TC-AUDIO-06** | Windows Microphone Privacy Denied | Checks Windows Privacy API, alerts user via non-blocking UI, displays Settings link. |
| **TC-AUDIO-07** | Pure Digital Silence (0.0 amplitude) | VAD flags zero speech; coordinator safely resets to idle with zero insertion. |
| **TC-AUDIO-08** | Extended Audio (> 30 seconds) | Circular buffer rolls over cleanly; memory footprint remains strictly bounded. |

---

## 3. Windows Text Insertion & Target Test Matrix

The text insertion service must be verified across standard Windows application architectures:

| Application Class | Target Applications | Primary Insertion Path | Fallback Path | Verification Criteria |
| :--- | :--- | :--- | :--- | :--- |
| **Standard Win32** | Windows Notepad (`notepad.exe`) | UIA `ValuePattern.SetValue` | `SendInput` (`Ctrl+V`) | Injects at cursor; 0 Enter keycodes. |
| **Modern WinUI 3** | Windows 11 Modern Notepad / Settings | UIA `TextPattern2` | `SendInput` (`Ctrl+V`) | Injects cleanly without losing selection. |
| **Chromium / Web** | Google Chrome, Microsoft Edge | UIA Document Element | `SendInput` (`Ctrl+V`) | Injects into address bar & Google Docs. |
| **Electron** | VS Code, Slack, Discord | UIA Edit Control | `SendInput` (`Ctrl+V`) | Safe clipboard fallback; restores clipboard. |
| **Terminal Emulators**| Windows Terminal, PowerShell, CMD | UIA Terminal Window | `SendInput` (`Ctrl+V`) | Injects command text without executing. |
| **Rich Text (COM)** | Microsoft Word (`WINWORD.EXE`) | UIA TextPattern | `SendInput` (`Ctrl+V`) | Injects styled text without corruption. |

### Insertion Safety & Clipboard Tests:
* **TC-INSERT-01 (VK_RETURN Blacklist)**: Monitor the low-level keyboard hook during 200 consecutive insertions. Exactly 0 instances of `VK_RETURN` (`0x0D`), `VK_SEPARATOR`, or keypad enter are permitted.
* **TC-INSERT-02 (Clipboard Restoration)**: Populate the Windows Clipboard with complex multi-format data (Bitmap image, HTML, text). Trigger clipboard fallback insertion. Verify that original clipboard formats and contents are fully restored within 150ms.
* **TC-INSERT-03 (Focus Loss During Insertion)**: Simulate the user switching windows (`Alt + Tab`) during the 150ms insertion window. Ensure text is not misdirected to an unintended window.

---

## 4. Empirical Performance Benchmark Design

Performance claims must be empirically measured rather than assumed:

### Latency Percentiles (P50, P95, P99)
For each test cycle, record:
1. **Hotkey Detection Latency**: Time from physical key press to session start signal.
2. **Audio Capture Startup**: Time from trigger to first WASAPI PCM buffer received.
3. **VAD Silence Detection**: Time from vocal silence to VAD speech-end event.
4. **ASR Inference Duration**: Time from audio buffer handoff to raw token completion.
5. **Language Sanitization**: Time taken by deterministic rule formatting.
6. **Insertion Latency**: Time from sanitized text to cursor appearance in target application.
7. **Total End-to-End Latency**: Measured from end-of-speech to character rendering.

### Resource Utilization Tracking
* **CPU Usage**: Average and peak CPU percentage across all logical cores during active inference.
* **RAM (Working Set / Private Bytes)**: Resident memory footprint at idle and during active transcription.
* **GPU & VRAM (DirectML)**: GPU Core %, Dedicated VRAM, and Shared VRAM allocated during model inference.
* **Cold-Start Launch Time**: Time from `.exe` invocation to system tray readiness.

---

## 5. Continuous Integration (CI) Specification

Continuous integration runs on GitHub Actions using `windows-latest` runners:

```yaml
name: FLOW Windows CI
on: [push, pull_request]

jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Validate Core Documents
        shell: pwsh
        run: |
          $docs = @("AGENTS.md", "PROJECT_PROFILE.md", "ARCHITECTURE.md", "ROADMAP.md", "SECURITY.md", "MIGRATION_MATRIX.md")
          foreach ($doc in $docs) {
              if (-not (Test-Path $doc)) { throw "Missing document: $doc" }
          }
```
