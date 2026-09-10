# FLOW — Phase 2B Physical Local Dictation Manual Validation Guide

> **Document Version**: 1.0.0  
> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Phase**: Phase 2B — Physical Core Dictation Parity  
> **Status**: Verified & Operational  

---

## 1. Executive Summary

Phase 2B transitions FLOW from architectural specification (Phase 2A) to a **genuine, physically functional local voice-to-text platform** on Windows 10/11. All synthetic, mock, and hardcoded transcription behaviors have been completely replaced with:

1. **Hardware WASAPI Audio Capture**: Direct COM integration (`IMMDeviceEnumerator`, `IMMDevice`, `IAudioClient`, `IAudioCaptureClient`) capturing live microphone streams, converting 48kHz/stereo float samples to canonical 16kHz mono Float32 audio buffers.
2. **Local Whisper Inference**: Native 64-bit AVX2/GPU inference via `Whisper.net` 1.9.1 executing `ggml-tiny.en.bin` with SHA-256 integrity verification (`921e4cf8...`). Zero cloud audio streaming; 100% offline sovereignty.
3. **Double-Tap Hands-Free Toggle**: Global low-level keyboard hook (`WH_KEYBOARD_LL`) supporting both hold-to-record (push-to-talk) and double-tap (within 350ms) hands-free recording.
4. **20-Minute Safety Ceiling**: Active session time enforcement issuing a warning at $T=19\text{m}$ and safely finalizing at $T=20\text{m}$.
5. **Zero-Enter Safety Invariant**: Strictly cursor-only and selection-replacement text insertion via Windows UI Automation with safe SendInput fallback. Under NO circumstances does FLOW simulate `0x0D`, `VK_RETURN`, or automated submission.

---

## 2. Hardware & Runtime Environment

| Subsystem | Specification |
| :--- | :--- |
| **Operating System** | Windows 11 Home / Pro x64 (Build 10.0.22631+) |
| **Audio Architecture** | Windows Core Audio WASAPI (Shared Mode, Event-Driven) |
| **Audio Capture Endpoint** | Intel® Smart Sound Technology for Digital Microphones / Realtek High Definition Audio |
| **Native Inference Engine** | Whisper.net 1.9.1 (native whisper.cpp 64-bit DLL with AVX2 CPU acceleration & DirectML GPU support) |
| **Model Weights** | `ggml-tiny.en.bin` (77,704,715 bytes; SHA-256: `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f`) |
| **Target Storage** | `%LOCALAPPDATA%\FLOW\models\ggml-tiny.en.bin` |
| **Global Activation Key** | `VK_RMENU` (Right Alt) |

---

## 3. Step-by-Step Operator Verification Procedures

### Test Case 1: Push-to-Talk in Notepad (Standard Dictation)

**Objective**: Verify end-to-end hotkey hold, live WASAPI capture, local Whisper transcription, and safe text insertion.

**Procedure**:
1. Open Windows Notepad (`notepad.exe`).
2. Focus the text editing area so the cursor is blinking.
3. Press and **hold** the **Right Alt** key.
4. Observe the floating HUD indicator appear with the recording indicator active.
5. Speak clearly: *"The quick brown fox jumps over the lazy dog period"*
6. Release the **Right Alt** key.
7. Observe the HUD transition to processing state and then fade out.
8. **Expected Result**:
   - The text `"The quick brown fox jumps over the lazy dog."` is inserted cleanly at the cursor position.
   - The period was converted to `.` by deterministic punctuation rules.
   - No newline or Enter character was typed; the cursor remains immediately following the period.

---

### Test Case 2: Hands-Free Double-Tap Activation

**Objective**: Verify hands-free recording toggle using double-tap gesture.

**Procedure**:
1. With any text editor open and focused, **double-tap** the **Right Alt** key (two presses within 350ms).
2. Release the key completely after the second tap.
3. Observe that the HUD remains in the active recording state without holding down any key.
4. Speak a sentence: *"Testing hands free dictation mode on Windows"*
5. Tap the **Right Alt** key once more to end the hands-free session.
6. **Expected Result**:
   - Audio is transcribed locally and inserted into the editor.
   - HUD returns to idle state.

---

### Test Case 3: PowerShell / Terminal Safety (Zero-Enter Verification)

**Objective**: Verify the **Inviolable Core Principle**: FLOW must NEVER execute or submit shell commands accidentally.

**Procedure**:
1. Open Windows PowerShell or Windows Terminal.
2. At the prompt (`PS C:\>`), press and hold **Right Alt**.
3. Speak: *"Get-Process"*
4. Release **Right Alt**.
5. **Expected Result**:
   - The characters `Get-Process` appear at the terminal cursor.
   - The command is **NOT executed**. The prompt remains at `PS C:\> Get-Process` waiting for the user to manually press Enter.
   - This verifies the absolute prohibition against `VK_RETURN` simulation.

---

### Test Case 4: Web Browser Input Field (No Form Auto-Submit)

**Objective**: Ensure dictation into a web browser form field does not submit the form.

**Procedure**:
1. Open Google Chrome, Microsoft Edge, or Firefox.
2. Navigate to `https://www.google.com` or any search engine.
3. Click into the search input box.
4. Hold **Right Alt** and speak: *"local voice recognition for windows"*
5. Release **Right Alt**.
6. **Expected Result**:
   - The text `"local voice recognition for windows"` appears in the search input field.
   - The search form is **NOT submitted**; results do not load until the user clicks Search or presses Enter manually.

---

### Test Case 5: Model Integrity & Offline Sovereignty

**Objective**: Verify that the local model is loaded strictly from disk and functions completely offline.

**Procedure**:
1. Disconnect the machine from Wi-Fi and Ethernet (turn on Airplane Mode).
2. Ensure `%LOCALAPPDATA%\FLOW\models\ggml-tiny.en.bin` exists.
3. Start FLOW (`Flow.Host.Windows.exe`).
4. Perform dictation into Notepad.
5. **Expected Result**:
   - Dictation completes successfully in full offline mode.
   - No network requests are made; zero telemetry or audio packets leave the system.

---

## 4. Verification Sign-Off Checklist

- [x] WASAPI capture endpoint activates cleanly with Intel Smart Sound / standard audio drivers.
- [x] Audio resampler cleanly produces 16,000Hz mono 32-bit float samples.
- [x] Local Whisper.net native inference initializes and processes audio samples on CPU AVX2.
- [x] Push-to-talk and Hands-free double-tap operate deterministically without key conflict.
- [x] 20-minute safety ceiling active in session coordinator.
- [x] ZERO-ENTER invariant strictly enforced across all tested native and web applications.
- [x] 100% test suite passing with zero failures.
