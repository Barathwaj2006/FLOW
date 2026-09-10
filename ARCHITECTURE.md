# ARCHITECTURE.md — Windows System Architecture Specification

> **Status**: Active Architecture Baseline — Windows Realignment  
> **Project**: FLOW — AI Voice Productivity Platform for Windows  
> **Target Platform**: Windows 10/11 x64 Native Desktop Application  

---

## 1. System Topology & Architectural Layers

FLOW is architected across four distinct boundaries ensuring modularity, native performance, and offline privacy:

```
┌────────────────────────────────────────────────────────────────────────────┐
│                  LAYER 1: Presentation & Shell UX                          │
│                   (WinUI 3 / Windows App SDK)                              │
│                                                                            │
│  ┌─────────────────────────┐  ┌───────────────────────┐  ┌──────────────┐  │
│  │    Floating HUD Panel   │  │   Settings Dashboard  │  │  System Tray │  │
│  │  (WS_EX_NOACTIVATE/TOP) │  │    (Fluent / Mica)    │  │ (NotifyIcon) │  │
│  └────────────▲────────────┘  └───────────┬───────────┘  └──────┬───────┘  │
└───────────────┼───────────────────────────┼─────────────────────┼──────────┘
                │ State / Event Binding     │ User Preferences    │ Lifecycle
┌───────────────▼───────────────────────────▼─────────────────────▼──────────┐
│                  LAYER 2: Application Core (.NET 9 / C#)                   │
│                                                                            │
│  ┌───────────────────────┐  ┌───────────────────────┐  ┌────────────────┐  │
│  │  Session Coordinator  │  │    Language Engine    │  │  Content Lock  │  │
│  │  - Async State Mach.  │  │  - Spoken Punctuation │  │  - Entities    │  │
│  │  - PTT / Hands-Free   │  │  - Backtrack Resolver │  │  - Constraints │  │
│  │  - Error Handling     │  │  - Filler Removal     │  │  - Diff Engine │  │
│  └───────────┬───────────┘  └───────────▲───────────┘  └────────┬───────┘  │
│              │                          │                       │          │
│              ├──────────────────────────┴───────────────────────┘          │
│              │                                                             │
│  ┌───────────▼───────────┐  ┌───────────────────────┐  ┌────────────────┐  │
│  │    Developer Mode     │  │  Personal Intelligence│  │ Local Storage  │  │
│  │  - Casing Transformer │  │  - Custom Dictionary  │  │ - SQLite DB    │  │
│  │  - Shell & Code Rules │  │  - Snippet Expander   │  │ - DPAPI Encrypt│  │
│  └───────────────────────┘  └───────────────────────┘  └────────────────┘  │
└──────────────┬──────────────────────────────────────────────┬──────────────┘
               │ Audio Stream / Insertion Commands            │ Direct Calls
┌──────────────▼──────────────────────────────┐┌──────────────▼──────────────┐
│  LAYER 3A: Windows Integration Subsystems   ││  LAYER 3B: Native AI Engine │
│                                             ││                             │
│  ┌───────────────────────┐  ┌────────────┐  ││  ┌───────────────────────┐  │
│  │ WASAPI Audio Capture  │  │ Global PTT │  ││  │ Silero VAD (ONNX/DML) │  │
│  │ (IAudioCaptureClient) │  │(WH_KEYBD_LL│  ││  └───────────┬───────────┘  │
│  └───────────────────────┘  └────────────┘  ││              │              │
│  ┌───────────────────────┐  ┌────────────┐  ││  ┌───────────▼───────────┐  │
│  │ UI Automation Engine  │  │ SendInput  │  ││  │ IASREngine (DirectML  │  │
│  │ (IUIAutomation Target)│  │ (Ctrl+V)   │  ││  │  or whisper.cpp)      │  │
│  └───────────────────────┘  └────────────┘  ││  └───────────┬───────────┘  │
│  ┌───────────────────────────────────────┐  ││  ┌───────────▼───────────┐  │
│  │ Windows Graphics Capture (Direct3D11) │  ││  │ GPU / NPU / CPU Fallbk│  │
│  └───────────────────────────────────────┘  ││  └───────────────────────┘  │
└─────────────────────────────────────────────┘└─────────────────────────────┘
                                      │ Optional Cloud Invocation (Opt-In)
┌─────────────────────────────────────▼──────────────────────────────────────┐
│                 LAYER 4: AWS Bedrock Cloud Infrastructure                  │
│                                                                            │
│  [FLOW Windows App] ──HTTPS/SigV4──► [API Gateway] ──► [Lambda Proxy]      │
│                                                              │             │
│                                                    [Amazon Bedrock]        │
│                                                    - Claude 3.5 Sonnet     │
│                                                    - Amazon Nova           │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Windows Integration Subsystems

### A. Global Hotkey Management
* **Push-to-Talk (PTT) Requirement**: Push-to-talk requires capturing distinct `KeyDown` (start capture) and `KeyUp` (stop capture and commit) transitions.
* **Architecture**:
  * Standard Win32 `RegisterHotKey` generates a `WM_HOTKEY` notification only on key press and does not natively report key release.
  * Therefore, FLOW utilizes a low-level keyboard hook (`SetWindowsHookEx(WH_KEYBOARD_LL)`) or dedicated raw input thread to monitor the push-to-talk trigger (e.g. `Ctrl + Space`, `Right Alt`, or `CapsLock`) with sub-millisecond precision.
  * The hook procedure dispatches events asynchronously to a background worker to avoid blocking the OS message queue.

### B. Audio Capture Pipeline (WASAPI)
* **API**: Windows Audio Session API (WASAPI) utilizing `IAudioClient3` and `IAudioCaptureClient`.
* **Mode**: Event-driven shared mode (with configurable exclusive mode for ultra-low-latency setups).
* **Format**: Standardized internally to 16,000 Hz, 1-channel (mono), 32-bit linear PCM float.
* **Resampling**: If the hardware device operates at 48,000 Hz or 44,100 Hz, a lightweight native resampler (via Media Foundation or WDL-Resampler) converts frames to 16kHz before buffering.
* **Buffering**: Lock-free circular audio buffer (`AudioRingBuffer`) holding up to 30 seconds of speech.
* **Device Handling**: Listens for `IMMNotificationClient` callbacks to gracefully handle USB/Bluetooth microphone disconnections, default device switches, and route changes without application crash or hung threads.

### C. Focused Target Detection & Text Context
* **API**: Windows UI Automation (`IUIAutomation`).
* **Inspection**: On hotkey trigger, queries `IUIAutomation::GetFocusedElement` to resolve:
  * Control Type (`UIA_EditControlTypeId`, `UIA_DocumentControlTypeId`).
  * Supported Control Patterns (`IUIAutomationValuePattern`, `IUIAutomationTextPattern2`).
  * Active Process & Executable name (e.g. `code.exe`, `notepad.exe`, `slack.exe`).
* **Performance**: Calls use background thread caching via `IUIAutomationCacheRequest` to minimize cross-process COM stalls.

---

## 3. Text Insertion & Safety Architecture

The text insertion service places characters at the active cursor position without clipboard corruption or accidental message dispatch.

```
┌──────────────────────────────────────────────────────────────┐
│                  Initiate Text Insertion                     │
└──────────────────────────────┬───────────────────────────────┘
                               │
            ┌──────────────────▼──────────────────┐
            │ Query Focused IUIAutomationElement   │
            └──────────────────┬──────────────────┘
                               │
                Check Supported Control Patterns
                ┌──────────────┴──────────────┐
                │                             │
       Supports ValuePattern?        No / Non-Standard Target
                │                             │
                ▼                             ▼
   ┌───────────────────────────┐ ┌───────────────────────────────┐
   │ IUIAutomationValuePattern │ │ Safe Clipboard Fallback       │
   │ .SetValue(cleanText)      │ │ 1. Backup Win32 Clipboard     │
   │ (Instant, zero clipboard) │ │ 2. SetText(cleanText)         │
   └───────────────────────────┘ │ 3. SendInput(Ctrl + V)        │
                                 │ 4. Sleep 150ms                │
                                 │ 5. Restore original Clipboard │
                                 └──────────────┬────────────────┘
                                                │
                                 ┌──────────────▼────────────────┐
                                 │ Inviolable Filter Safeguard   │
                                 │ Strictly reject VK_RETURN,    │
                                 │ VK_ENTER, and form submission │
                                 └───────────────────────────────┘
```

### Safety Rules:
1. **Never Send / Submit**: Under no circumstances shall FLOW simulate `VK_RETURN` (`0x0D`), keypad enter, or trigger automated button clicks.
2. **Clipboard Isolation**: When using the `Ctrl+V` fallback:
   * Existing clipboard data (text, HTML, images, custom formats) is backed up.
   * New text is written to the clipboard.
   * `SendInput` emits `Ctrl+V`.
   * The original clipboard payload is restored within 150ms.
3. **Target Validation**: If the focused element is a password field (`IsPassword == true`) or read-only, FLOW displays an informative tooltip and aborts insertion.

---

## 4. Local AI & Speech Recognition (ASR) Layer

FLOW defines an extensible inference abstraction (`IASREngine`):

```csharp
public interface IASREngine : IAsyncDisposable
{
    string ModelIdentifier { get; }
    Task LoadModelAsync(string modelPath, CancellationToken ct);
    Task<TranscriptionResult> TranscribeAsync(AudioBuffer audio, TranscribeOptions options, CancellationToken ct);
}
```

### Supported & Evaluated Backends:
1. **ONNX Runtime with DirectML (Primary Candidate)**:
   * Leverages Microsoft DirectML (DirectX 12 compute) to execute Whisper and Silero VAD models.
   * Provides hardware-accelerated tensor computation across NVIDIA GeForce/RTX, AMD Radeon, Intel Arc GPUs, and Windows Copilot+ NPUs.
2. **whisper.cpp with AVX2 CPU Fallback (Secondary Candidate)**:
   * Highly optimized C++ implementation with lightweight GGML quantized weights (`q4_0`, `q5_0`).
   * Provides predictable performance on non-GPU enterprise Windows machines via AVX2 / AVX-512 vector extensions.
3. **Mock ASR Engine**:
   * Deterministic test driver for automated CI and reliability verification.

---

## 5. Screen AI Architecture (Windows Graphics Capture)

* **Invocation**: Dedicated hotkey (e.g. `Ctrl + Shift + S`).
* **Capture Pipeline**:
  * Utilizes `Windows.Graphics.Capture` (Windows 10 Build 1803+ / Windows 11).
  * Direct3D11 surface binding via `Direct3D11CaptureFramePool`.
  * Renders a non-activating semi-transparent selection overlay across all active monitors with DPI scaling awareness.
* **Data Minimization**: Only the user-selected rectangle is extracted into a Direct3D texture / bitmap. Full desktop contents outside the bounding box are immediately discarded.
* **Vision & OCR**:
  * Local: Windows native OCR (`Windows.Media.Ocr`) runs on-device in $< 50\text{ ms}$.
  * Cloud: Complex reasoning (summarization, reply drafting) sends the cropped image to Amazon Bedrock.

---

## 6. Content Lock Engine (Fidelity & The No-Invention Rule)

Content Lock prevents AI hallucination or silent alteration of user requirements:

```
                          ┌──────────────────────────┐
                          │    Raw Transcribed Text  │
                          └────────────┬─────────────┘
                                       │
                        ┌──────────────▼──────────────┐
                        │  Protected Entity Extractor │
                        │  - Technical Identifiers    │
                        │  - Regex Entity Matchers    │
                        │  - Negative Constraints     │
                        └──────────────┬──────────────┘
                                       │
                                       ├─────────────────────────────┐
                                       │ [Protected Entity Graph]    │
                                       │                             │
                        ┌──────────────▼──────────────┐              │
                        │    AI Transformation Action │              │
                        │   (Local Rules or Bedrock)  │              │
                        └──────────────┬──────────────┘              │
                                       │                             │
                        ┌──────────────▼──────────────┐              │
                        │    Content Lock Validator   │              │
                        │  - Bi-directional Match     │◄─────────────┘
                        │  - Negative Rule Inversion  │
                        │  - The No-Invention Audit   │
                        └──────────────┬──────────────┘
                                       │
                   ┌───────────────────┴───────────────────┐
                   │                                       │
             [Passed: 100%]                      [Failed / Discrepancy]
                   │                                       │
                   ▼                                       ▼
        [Direct Text Insertion]                 [Display Prompt Diff UI]
                                                [User Manually Resolves]
```

### The No-Invention Rule
If user input is:
> *"Build an API in Python."*

Content Lock rejects any transformed prompt that introduces unmentioned frameworks (`FastAPI`, `Flask`, `PostgreSQL`, `Docker`) unless marked as an unconfirmed clarification.

---

## 7. Local Storage & Security Boundaries

* **Database**: Embedded SQLite via `Microsoft.Data.Sqlite`.
* **Location**: `%LOCALAPPDATA%\Flow\flow.db`.
* **Encryption**: Sensitive tables (user dictionary, personal snippets, dictation history) are encrypted using Windows Data Protection API (DPAPI) or SQLCipher.
* **Audio Isolation**: Raw audio buffers exist solely in volatile memory and are zeroed immediately after transcription. No audio recordings are saved to persistent disk.
