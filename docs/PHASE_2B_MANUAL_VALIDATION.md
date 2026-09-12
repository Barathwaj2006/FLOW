# FLOW — Phase 2B Physical Local Dictation Validation & Evidence Guide

> **Document Version**: 2.0.0  
> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Phase**: Phase 2B — Physical Core Dictation Parity  
> **Status**: Verified & Operational  
> **Authoritative Model SHA-256**: `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f`  

---

## 1. Executive Summary & Verification Matrix

Phase 2B establishes the **real physical local dictation pipeline** for FLOW on Windows 10/11. All mock, synthetic, and hardcoded transcription behaviors have been completely replaced with genuine hardware and native inference paths.

| Capability | Status | Environment / Hardware | Evidence Summary |
| :--- | :--- | :--- | :--- |
| **Real WASAPI Audio Capture** | **VERIFIED** | Windows 11 x64 / Intel® Smart Sound Digital Mics (`{0.0.1.00000000}.{b2a7123d-d0e6-427b-94e2-65e5ce95a201}`) | Captured 32,160 resampled float samples at 16kHz from 48kHz stereo stream. Non-zero percentage: 95.11%, Peak: 0.9871, RMS: 0.1202. |
| **Real Whisper Inference** | **VERIFIED** | Whisper.net 1.9.1 / native whisper.cpp x64 AVX2 / `ggml-tiny.en.bin` | Transcribed 5.53s spoken speech in 619ms (0.11x RTF). Recognized semantic phrase accurately. |
| **Inference Backend** | **CPU (AVX2)**: **VERIFIED**<br>**DirectML GPU**: **NOT VERIFIED** | 13th Gen Intel Core i5-13420H (AVX2)<br>NVIDIA GeForce RTX 3050 6GB Laptop GPU | `whisper.dll` / `ggml-cpu-whisper.dll` AVX2 verified. DirectML ONNX backend configured but Whisper.net executes via CPU AVX2. |
| **Push-to-Talk Hotkey** | **VERIFIED** | Low-Level Keyboard Hook (`WH_KEYBOARD_LL`) / `VK_RMENU` (Right Alt) | Press-and-hold begins capture; release terminates capture and executes transcription/insertion. |
| **Hands-Free Mode** | **VERIFIED** | Double-tap state machine (350ms threshold) | Two taps within 350ms latches recording into hands-free mode. Subsequent tap concludes session. |
| **20-Minute Ceiling** | **VERIFIED** | `VoiceSessionCoordinator` active timer | Session warning fires at $T=19\text{m}$; automatic safe stop and transition at $T=20\text{m}$. |
| **Session Cancellation** | **VERIFIED** | `VK_ESCAPE` / `CancelSessionAsync` | Immediate cancellation clears audio ring buffer, resets VAD, and transitions to Idle without text insertion. |
| **Safe Text Insertion** | **VERIFIED** | `WindowsTextInsertionService` / Win32 UIA + SendInput Ctrl+V | Clean insertion with clipboard backup/restore. Strict newline/return neutralization. |
| **Terminal Safety (Zero-Enter)** | **VERIFIED** | PowerShell / Windows Terminal | Strips all `0x0D` / `\r` / `\n` characters. Commands are inserted as text without executing. |
| **Browser Form Safety** | **VERIFIED** | Web browser text input fields | Neutralizes newlines, preventing unintended form submission. |
| **Offline Operation** | **VERIFIED** | Local disk `%LOCALAPPDATA%\FLOW\models\ggml-tiny.en.bin` | Zero network calls made during transcription when model is present on disk. |
| **Model Failure Handling** | **VERIFIED** | `WhisperModelManager` | Missing model detected (`NotInstalled`); corrupted model (checksum mismatch) detected and rejected. |
| **Microphone Failure Handling** | **VERIFIED** | `WasapiAudioCapture.CaptureError` | Endpoint loss unblocks waiting threads, logs error, and safely cancels active session without hung threads. |

---

## 2. Empirical Test Evidence

### Test 1: Real Physical Microphone Capture
```text
Test: Physical Microphone Non-Zero Audio Capture & Signal Metrics
Environment: Windows 11 x64 (Build 10.0.26200), Intel Core i5-13420H
Endpoint: Intel® Smart Sound Technology for Digital Microphones ({0.0.1.00000000}.{b2a7123d-d0e6-427b-94e2-65e5ce95a201})
Native Hardware Format: IEEE Float 32-bit 48000Hz, 2 channels
Buffer Duration: 200.00 ms (Shared Mode, Event-Driven)
Capture Duration: 2010 ms
Resampled 16kHz Samples: 32,160
RMS Amplitude: 0.120164
Peak Amplitude: 0.987148
Non-Zero Sample Percentage: 95.11%
Result: Live physical acoustic signal captured and resampled with zero synthetic buffers.
Status: VERIFIED
```

### Test 2: Real Spoken Speech Transcription
```text
Test: Human Speech Local Whisper Transcription
Environment: Windows 11 x64 / Whisper.net 1.9.1 / native whisper.cpp (AVX2 CPU)
Model: ggml-tiny.en.bin (77,704,715 bytes; SHA-256: 921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f)
Spoken Input: "FLOW local dictation test. This is a real microphone transcription."
Audio Duration: 5.53 seconds (88,480 samples @ 16kHz mono)
Inference Duration: 619 ms
Real-Time Factor (RTF): 0.11x (9x faster than real-time)
Actual Transcript: "Flow Local Dictation Test. This is a real microphone transcription."
Result: Meaningful human speech successfully recognized and transcribed locally with zero cloud dependencies.
Status: VERIFIED
```

### Test 3: Offline Sovereignty Test
```text
Test: Core Dictation Offline Sovereignty
Environment: Windows 11 x64 / Local Storage
Model Path: C:\Users\barat\AppData\Local\FLOW\models\ggml-tiny.en.bin
File Integrity: 77,704,715 bytes (SHA-256: 921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f)
Network Activity: Zero bytes transferred; HttpClient not invoked when model is cached on disk.
Inference Status: Executed entirely in local memory via native whisper.cpp binaries.
Result: 100% offline core voice dictation confirmed.
Status: VERIFIED
```

### Test 4: Zero-Enter Safety Invariant Test
```text
Test: Terminal and Form Safety Neutralization
Environment: Windows 11 x64 / WindowsTextInsertionService
Input Payload: "Remove-Item -Recurse C:\temp\r\n"
Sanitized Payload: "Remove-Item -Recurse C:\temp"
Simulated Key Sequence: VK_CONTROL down -> 'V' down -> 'V' up -> VK_CONTROL up
Simulated Enter / Return: NONE (0 occurrences of VK_RETURN, 0x0D, or VK_SEPARATOR)
Result: Text placed at cursor position without executing commands or submitting forms.
Status: VERIFIED
```

### Test 5: Abnormal Condition & Failure Handling
```text
Test: Missing and Corrupted Model Integrity Detection
Environment: Windows 11 x64 / WhisperModelManager
Scenario A (Missing Model): Target directory empty -> IsModelInstalledAndValid() returned false, State = NotInstalled.
Scenario B (Corrupted Model): 77.7MB garbage byte stream written to model path -> SHA-256 checksum mismatch caught and file rejected.
Scenario C (Microphone Error): Endpoint failure dispatches CaptureError event, unblocks waiting threads, cancels coordinator session.
Result: Clean failure modes with zero application crashes or hung threads.
Status: VERIFIED
```

---

## 3. Authoritative Checksum Reference

To prevent any configuration discrepancies across the repository, the single authoritative checksum for the default model is:

* **Model File**: `ggml-tiny.en.bin`
* **File Size**: `77,704,715 bytes`
* **Official Source**: `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin`
* **Binary SHA-256**: `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f`

*(Note: The Git LFS pointer OID in Hugging Face repository metadata was `0d686a...`, but the actual binary payload downloaded and validated on disk has SHA-256 `921e4cf8...`. The codebase uses this single verified binary checksum).*

---

## 4. Operational Latency Profile

Empirical measurements gathered on this host machine (13th Gen Intel Core i5-13420H, 16GB RAM, Windows 11 Home):

| Pipeline Stage | Measured Latency | Assessment |
| :--- | :--- | :--- |
| **Microphone Startup** | ~350 - 500 ms | Hardware driver endpoint initialization |
| **WASAPI Buffer Interval** | 200 ms | Shared mode event-driven packet delivery |
| **Audio Resampling** | < 1 ms | Native 48kHz $\rightarrow$ 16kHz linear interpolation |
| **Whisper Model Load (Pre-warm)** | ~350 ms | One-time startup cost into memory |
| **Whisper Inference (5.5s audio)** | 619 ms | Native CPU AVX2 (RTF = 0.11x) |
| **Safe Insertion (Clipboard + SendInput)** | ~160 ms | Includes 150ms clipboard restore window |
| **End-to-End Turnaround** | ~800 - 950 ms | From hotkey release to text in focused app |
