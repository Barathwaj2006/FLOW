# PROJECT_PROFILE.md — Product Master Profile (Windows Native)

# FLOW: AI Voice Productivity Platform for Windows

> **Product Vision**: A system-wide, voice-native AI productivity layer for Windows 10 and Windows 11 that enables users to speak naturally and turn their voice, screen, or selected context into accurate text, replies, prompts, translations, code, and AI-assisted output—while strictly protecting the user's original intent through Content Lock.

---

## 1. Executive Summary & Market Positioning

### The Opportunity
Windows desktop users represent the vast majority of global enterprise knowledge workers, software developers, and professionals. However, existing voice productivity on Windows is fragmented:
1. **Legacy Windows Voice Typing (`Win + H`)**: Transcribes words into literal text with rudimentary punctuation, lacking contextual cleanup, intent-aware backtracking, developer casing, or entity validation.
2. **Cloud-Tethered Wrappers**: Cloud-based tools stream private microphone audio across remote servers, incur high latency, and often hallucinate or alter critical technical constraints during text rewrites.

### The FLOW Advantage: Competitive Positioning vs. Wispr Flow
FLOW establishes a native Windows-first platform competing directly with Wispr Flow:

| Dimension | Wispr Flow / Cloud Competitors | FLOW (This Product) |
| :--- | :--- | :--- |
| **Primary Platform** | Cloud-dependent, cross-platform wrappers | **Windows-Native Desktop** (WinUI 3, .NET 9, WASAPI, DirectML) |
| **Privacy & Sovereignty**| Audio streamed to cloud servers | **100% Local Core**: Microphone audio never leaves the PC for dictation. |
| **Fidelity Guarantee** | Black-box LLM rewrites prone to hallucination | **Content Lock**: Formally verifies preservation of entities, code, numbers, and negative constraints. |
| **Spoken Corrections** | Appends both phrases or loses context | **Intent-Aware Backtracking**: Resolves phrases like *"send it tomorrow actually Friday"* $\rightarrow$ *"Send it tomorrow—actually, Friday."* |
| **Developer Ergonomics** | Mangled casing, syntax, and terminals | **Native Developer Mode**: Zero-friction case transforms (`camelCase`, `snake_case`), PowerShell/CLI awareness. |
| **Multimodal Screen AI** | Clunky screen recording or cloud tethering | **Windows Graphics Capture**: Hardware-accelerated GPU capture via Direct3D11 with user-defined crop. |
| **Hardware Acceleration**| Proprietary server GPUs | **Heterogeneous DirectML Acceleration**: Runs across NVIDIA RTX, AMD Radeon, Intel Arc, and Copilot+ NPUs. |
| **Insertion Safety** | Inconsistent across non-standard windows | **Deterministic Safety**: Strict filter dropping `VK_RETURN` (0x0D); safe clipboard fallback with 150ms restore. |

---

## 2. The Three Inviolable Tenets

### 1. LOCAL FIRST (Offline Sovereignty)
* Core voice functionality is self-contained and operates with zero internet connectivity.
* Global hotkey capture, Windows Audio (WASAPI), Voice Activity Detection (VAD), speech recognition (DirectML / CPU fallback), and cursor text insertion require no cloud services.
* Raw microphone audio is never transmitted across the network.

### 2. FAITHFUL (Content Lock & Intent Integrity)
* AI transformations must improve presentation without mutating substance.
* **Permitted Cleanup**: Grammar, punctuation, capitalization, spelling, filler-word elimination ("um", "uh"), stutter collapse, and backtracking resolution.
* **Protected Substance (Content Lock)**:
  * Entities: Names, email addresses, phone numbers, URLs, dates.
  * Quantitative Data: Numbers, metrics, pricing, units of measurement.
  * Technical Artifacts: Programming languages, frameworks, libraries, variable names, functions, file paths (`C:\...`), PowerShell commands (`Get-ChildItem`), CLI flags (`--force`).
  * Logic & Intent: Negative instructions (*"Do not use Firebase"* $\rightarrow$ must NEVER use Firebase), architectural constraints, acceptance criteria.
* **The No-Invention Rule**: The system never manufactures facts, dependencies, or specifications the user did not state.

### 3. USER CONTROL (No Accidental Actions)
* The text insertion engine places characters at the active cursor position or replaces selected text.
* It must **NEVER** simulate Enter (`VK_RETURN`, `0x0D`), Keypad Enter, `VK_SEPARATOR`, or trigger form submit/message send buttons. Dispatching messages or running commands remains strictly the user's manual action.

---

## 3. Technology Evaluation & Approval Status

| Subsystem | Candidate Technology | Status | Rationale |
| :--- | :--- | :--- | :--- |
| **Host Application Framework** | C# / .NET 9 + WinUI 3 (Windows App SDK) | **Proposed (Candidate 1)** | High development velocity, rich Windows 11 Fluent/Mica UX, robust Win32/WASAPI/UIA interop. |
| **Host Application Framework (Alt)**| Pure C++20 + WinUI 3 (C++/WinRT) | **Evaluated (Rejected)** | Extreme development overhead, slow compilation, fragile XAML bindings, complex COM maintenance. |
| **Audio Capture** | WASAPI (`IAudioClient3` / `IAudioCaptureClient`)| **Approved** | Low-latency Windows native audio interface for 16kHz Float32 mono capture. |
| **Global Hotkey** | Win32 `RegisterHotKey` + `WH_KEYBOARD_LL` Hook | **Proposed** | Low-level hook required to capture push-to-talk keyup/keydown reliably. |
| **Focused Target Detection** | Windows UI Automation (`IUIAutomation`) | **Approved** | Comprehensive accessibility interface for Win32, WinUI, WPF, Chromium, and Electron. |
| **Text Insertion** | UIA `ValuePattern` / `TextPattern` + `SendInput`| **Proposed** | Direct programmatic insertion with safe clipboard fallback (`Ctrl+V`) and 150ms restore. |
| **Local ASR Backend** | ONNX Runtime with DirectML | **Proposed (Primary)** | Accelerates Whisper on NVIDIA, AMD, Intel GPUs and Windows Copilot+ NPUs. |
| **Local ASR Backend (Alt)** | `whisper.cpp` (DirectX 12 / AVX2 CPU) | **Proposed (Secondary)** | Lightweight, portable C++ inference with proven CPU fallback on older x64 hardware. |
| **Voice Activity Detection** | Silero VAD (ONNX Runtime / DirectML) | **Proposed** | State-of-the-art chunk segmentation and silence detection ($< 150\text{ ms}$). |
| **Screen AI Capture** | Windows Graphics Capture (`Direct3D11`) | **Proposed** | High-performance GPU surface capture with minimal latency and multi-monitor/DPI support. |
| **Local Persistence** | SQLite via `Microsoft.Data.Sqlite` + DPAPI | **Approved** | Zero-service embedded database encrypted via Windows Data Protection API. |
| **Cloud Intelligence** | Amazon Bedrock via API Gateway + Lambda | **Approved (Opt-in)**| On-demand generative AI (Claude 3.5 Sonnet, Amazon Nova) strictly for advanced operations. |

---

## 4. Target Operating Environment & Hardware Matrix

* **Target OS**: Windows 10 (Version 21H2+ / Build 19044+) and Windows 11 (Build 22000+).
* **Architecture**: x64 (Primary), with future roadmap support for ARM64 (Snapdragon X Elite).
* **Target Applications for Text Insertion**:
  * Development Environments: VS Code, Visual Studio, Cursor, JetBrains IDEs.
  * Terminals: Windows Terminal, PowerShell, Git Bash, CMD.
  * Office & Productivity: Microsoft Word, Excel, Outlook, OneNote, Notion.
  * Web & Electron: Google Chrome, Microsoft Edge, Slack, Discord, WhatsApp Desktop.
  * Built-in Windows Utilities: Notepad, Sticky Notes.
