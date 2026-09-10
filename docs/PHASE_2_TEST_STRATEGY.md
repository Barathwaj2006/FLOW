# PHASE_2_TEST_STRATEGY.md — Comprehensive Verification Strategy & Application Matrix

> **Standard**: Production Native Desktop Quality  
> **Rule**: Real implementations, physical testing, and empirical measurements over synthetic claims.

---

## 1. Test Tier Hierarchy

```
┌────────────────────────────────────────────────────────┐
│  Tier 5: Real-World Physical Cross-Application Tests   │
│  (Real mic -> Real DirectML ASR -> Target App window)  │
├────────────────────────────────────────────────────────┤
│  Tier 4: Windows Integration Tests                     │
│  (UIA focus, WASAPI audio tap, Win32 hooks, clipboard) │
├────────────────────────────────────────────────────────┤
│  Tier 3: End-to-End In-Process Integration Tests       │
│  (VoiceSessionCoordinator full pipeline with Mock/ASR) │
├────────────────────────────────────────────────────────┤
│  Tier 2: Subsystem Integration & Persistence Tests     │
│  (SQLite CRUD, FTS5 search, Registry fallback chains)  │
├────────────────────────────────────────────────────────┤
│  Tier 1: Deterministic Unit Tests                      │
│  (RingBuffer, EnergyVAD, Sanitizer, Casing, Backtrack) │
└────────────────────────────────────────────────────────┘
```

---

## 2. Test Suites Defined

### A. Tier 1: Deterministic Unit Tests (`tests/Flow.Core.Tests`)
* **`AudioRingBufferTests`**: Pre-allocation capacity, sequential read/write integrity, overflow rollover truncation, clear/reset pointer hygiene.
* **`EnergyVADTests`**: Zero-energy silence detection, speech onset thresholding, trailing silence cutoff, dynamic threshold updates.
* **`DeterministicTextSanitizerTests`**: Initial capitalization, terminal periods, question marks, spoken punctuation replacement, filler word suppression (*"um"*, *"uh"*), multiple whitespace normalization, and Content Lock entity preservation.
* **`CasingTransformerTests`**: `camelCase`, `snake_case`, `PascalCase`, `kebab-case`, `SCREAMING_SNAKE`, `Title Case`, and `Sentence case`.
* **`BacktrackingTests`**: Intent correction (*"Friday actually next Monday"* $\rightarrow$ *"Next Monday"*).
* **`ListFormattingTests`**: Spoken numbers and lists (*"one ... two ... three ..."* $\rightarrow$ `1. ...\n2. ...\n3. ...`).
* **`ZeroEnterUnitTests`**: Asserts that `\r`, `\n`, `\r\n`, and spoken `"new line"` never survive into output prose.

### B. Tier 2: Subsystem & Persistence Integration Tests
* **`DatabaseRepositoryTests`**: SQLite schema creation, migrations, transaction rollbacks, and WAL file hygiene.
* **`DictionaryManagerTests`**: Case-insensitive custom vocabulary lookups and Whisper prompt biasing token construction.
* **`SnippetEngineTests`**: Voice cue matching within $\le 60$ characters; rich text snippet expansion up to $4,000$ characters.
* **`FtsSearchTests`**: SQLite FTS5 full-text indexing over dictation history with prefix and phrase queries.
* **`StyleManagerTests`**: Category/tone profile resolution based on target application process names.

### C. Tier 3: Core Integration Tests
* **`VoiceSessionCoordinatorTests`**:
  * Happy path: Start $\rightarrow$ Audio Stream $\rightarrow$ End $\rightarrow$ Transcribe $\rightarrow$ Format $\rightarrow$ Insert.
  * Silence rejection: Low energy audio rejected cleanly without calling insertion.
  * User cancellation: `Esc` cancels active session and resets HUD to Idle.
  * ASR Fallback: Primary engine simulated failure triggers secondary engine seamlessly.
  * Rapid repeated triggers: Stress test simulating 20 rapid PTT activations without race conditions.

### D. Tier 4: Windows Integration Tests (`tests/Flow.Windows.Tests`)
* **`WasapiAudioCaptureTests`**: Real Core Audio client initialization, device format negotiation, 16kHz float32 conversion, and background capture thread lifecycle.
* **`GlobalHotkeyHookTests`**: Low-level `WH_KEYBOARD_LL` hook registration, KeyDown vs KeyUp differentiation, and shortcut rebinding.
* **`ZeroEnterSafetyIntegrationTests`**: Verifies that `WindowsTextInsertionService` intercepts and rejects any input containing `0x0D` or enter simulation.
* **`ClipboardSafetyTests`**: Validates that SendInput `Ctrl+V` restores the user's prior clipboard text within 150ms.
* **`DpapiEncryptionTests`**: Validates roundtrip protection and unprotection of sensitive configuration strings using Windows DPAPI.

### E. Tier 5: Physical Cross-Application Test Matrix

Every application in this matrix must be physically validated with real voice dictation before Phase 2 acceptance:

| Application | Process Name | Window Class / Technology | Target Text Field | Insertion Strategy | Known Quirk / Requirement |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Windows Notepad** | `notepad.exe` | `RichEditD2DPT` (Win11) / `Edit` (Win10) | Main text editor | Tier 1 UIA / Tier 2 Clipboard | Native Win32 baseline. Instant paste. |
| **Microsoft Word** | `WINWORD.EXE` | `_WwG` (Office RichEdit) | Document canvas | Tier 1 UIA / Tier 2 Clipboard | Preserves document styling. |
| **Google Chrome** | `chrome.exe` | `Chrome_WidgetWin_1` | Omnibox, search, web forms | Tier 2 SendInput Clipboard | Fast clipboard restore required. |
| **Microsoft Edge** | `msedge.exe` | `Chrome_WidgetWin_1` | Web text areas, inputs | Tier 2 SendInput Clipboard | Identical Chromium behavior. |
| **Gmail (Web)** | `chrome.exe` | ContentEditable `div` | Compose body, Subject line | Tier 2 SendInput Clipboard | Avoids Enter simulation to prevent auto-send (`Ctrl+Enter` or accidental submit). |
| **Google Docs** | `chrome.exe` | Custom Canvas / Kix editor | Document canvas | Tier 2 SendInput Clipboard | Does not expose standard UIA text elements; requires pure clipboard injection. |
| **WhatsApp Web** | `chrome.exe` | ContentEditable `div` | Message input box | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Zero-Enter invariant must prevent sending messages. |
| **Slack (Desktop)** | `slack.exe` | Electron / Quill editor | Message draft box | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Must never simulate Enter or trigger message dispatch. |
| **Notion (Desktop)** | `notion.exe` | Electron / React canvas | Page block | Tier 2 SendInput Clipboard | Fast cursor placement. |
| **Visual Studio Code**| `code.exe` | Electron / Monaco editor | Code file, terminal, Copilot chat | Tier 2 SendInput Clipboard | Does NOT trigger "Screen Reader Mode" alert. |
| **Cursor** | `cursor.exe` | Electron / Monaco editor | Composer, Chat, Code editor | Tier 2 SendInput Clipboard | Supports voice file tagging (`@filename`). |
| **PowerShell** | `powershell.exe` | `ConsoleWindowClass` | Command prompt | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Must NEVER simulate Enter or execute commands. |
| **Windows Terminal** | `wt.exe` | `CASCADIA_HOSTING_WINDOW_CLASS` | Active shell tab | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Must NEVER simulate Enter or execute commands. |

---

## 3. Empirical Performance SLA

Synthetic unit tests must NEVER be reported as "end-to-end latency."

Measurements must be taken from the start of user audio capture through to the visual appearance of text in the target application:

| Pipeline Stage | Target P50 | Target P95 | Target P99 | Measurement Method |
| :--- | :--- | :--- | :--- | :--- |
| **Audio Capture to Ring Buffer** | $< 2\text{ ms}$ | $< 5\text{ ms}$ | $< 10\text{ ms}$ | High-resolution timestamp from WASAPI packet arrival to ring buffer write. |
| **Voice Activity Detection** | $< 1\text{ ms}$ | $< 2\text{ ms}$ | $< 3\text{ ms}$ | RMS energy & silence calculation per 100ms audio chunk. |
| **ASR Inference (DirectML)** | $< 250\text{ ms}$ | $< 400\text{ ms}$ | $< 600\text{ ms}$ | Model forward pass on 3-second audio buffer using GPU DirectML. |
| **ASR Inference (CPU Fallback)** | $< 500\text{ ms}$ | $< 900\text{ ms}$ | $< 1400\text{ ms}$ | Model forward pass using AVX2 CPU execution. |
| **Language Sanitization** | $< 1\text{ ms}$ | $< 2\text{ ms}$ | $< 5\text{ ms}$ | Regex formatting, zero-enter stripping, and snippet replacement. |
| **Text Insertion (Clipboard Tier)**| $< 160\text{ ms}$ | $< 180\text{ ms}$ | $< 220\text{ ms}$ | Win32 clipboard write + SendInput + 150ms app read delay + restore. |
| **Total End-to-End (Local GPU)** | **$< 420\text{ ms}$** | **$< 600\text{ ms}$** | **$< 850\text{ ms}$** | From hotkey release to text in target application window. |

### Resource Budgets
* **Idle RAM (Working Set)**: $< 65\text{ MB}$
* **Active Transcription RAM**: $< 280\text{ MB}$ (with `whisper-base.en` loaded in ONNX Runtime)
* **VRAM Allocation (DirectML)**: $< 450\text{ MB}$
* **Idle CPU Usage**: $< 0.1\%$
* **Cold Start Time**: $< 600\text{ ms}$
