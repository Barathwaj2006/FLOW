# PHASE_2_TEST_STRATEGY.md — Comprehensive Verification Strategy & Application Matrix

> **Standard**: Production Native Desktop Quality (Post-Reconciliation Audit)  
> **Rule**: Real physical testing, honest UI verification, and empirical measurements over synthetic claims.

---

## 1. Test Tier Hierarchy

```
┌────────────────────────────────────────────────────────┐
│  Tier 6: UI & Human Desktop Validation Gate            │
│  (Hub XAML tabs, HUD waveform, shortcut rebinding)     │
├────────────────────────────────────────────────────────┤
│  Tier 5: Real-World Physical Cross-Application Matrix  │
│  (Real mic -> Real Whisper ASR -> 12 Windows apps)     │
├────────────────────────────────────────────────────────┤
│  Tier 4: Windows Integration Tests                     │
│  (UIA focus, WASAPI audio tap, Win32 hooks, clipboard) │
├────────────────────────────────────────────────────────┤
│  Tier 3: End-to-End In-Process Integration Tests       │
│  (VoiceSessionCoordinator full pipeline with ASR)      │
├────────────────────────────────────────────────────────┤
│  Tier 2: Subsystem Integration & Persistence Tests     │
│  (SQLite on-disk CRUD, WAL mode, FTS5 full-text search)│
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
* **`PersonalDictionaryTests`**: Dictionary CRUD, case sensitivity, starred term priority, word boundary safety, JSON/CSV roundtrips.
* **`SnippetExpansionTests`**: Snippets CRUD, spoken trigger matching, longest-match precedence, disabled toggle, Zero-Enter invariant preservation.
* **`StyleFormattingTests`**: Contraction expansion/contraction, formality level substitutions, application mapping resolution.
* **`ZeroEnterUnitTests`**: Asserts that `\r`, `\n`, `\r\n`, and spoken `"new line"` never survive into output prose.

### B. Tier 2: Subsystem & Persistence Integration Tests
* **`SqlitePersonalizationDatabaseTests`**: Real SQLite schema creation, WAL journal mode (`PRAGMA journal_mode = wal`), foreign keys (`PRAGMA foreign_keys = 1`), and connection pooling.
* **`FtsSearchTests`**: SQLite FTS5 full-text indexing over dictation history with prefix and phrase queries.
* **`PersistenceRoundtripTests`**: Database close and reopen from physical Windows disk verifying data integrity and starred ordering.

### C. Tier 3: Core Integration Tests
* **`VoiceSessionCoordinatorTests`**:
  * Happy path: Start $\rightarrow$ Audio Stream $\rightarrow$ End $\rightarrow$ Transcribe $\rightarrow$ Format $\rightarrow$ Insert.
  * Silence rejection: Low energy audio rejected cleanly without calling insertion.
  * User cancellation: `Escape` cancels active session and resets HUD to Idle.
  * ASR Fallback: Primary engine failure triggers secondary engine seamlessly.
  * 20-minute ceiling: Session auto-stops at 1,200 seconds with warning event at 1,140 seconds.

### D. Tier 4: Windows Integration Tests (`tests/Flow.Windows.Tests`)
* **`WasapiAudioCaptureTests`**: Real Core Audio client initialization, device format negotiation, 16kHz float32 conversion, and background capture thread lifecycle.
* **`GlobalHotkeyHookTests`**: Low-level `WH_KEYBOARD_LL` hook registration, KeyDown vs KeyUp differentiation, 350ms double-tap hands-free state machine.
* **`ZeroEnterSafetyIntegrationTests`**: Verifies that `WindowsTextInsertionService` intercepts and rejects any input containing `0x0D` or enter simulation.
* **`ClipboardSafetyTests`**: Validates that SendInput `Ctrl+V` restores the user's prior clipboard text within 150ms.
* **`DpapiEncryptionTests`**: Validates roundtrip protection and unprotection of sensitive database strings using Windows DPAPI.

### E. Tier 5: Physical Cross-Application Test Matrix (12 Target Apps)

Every application in this matrix must be physically validated with real voice dictation:

| Application | Process Name | Window Class / Technology | Target Text Field | Insertion Strategy | Known Quirk / Requirement |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Windows Notepad** | `notepad.exe` | `RichEditD2DPT` (Win11) / `Edit` (Win10) | Main text editor | Tier 1 UIA / Tier 2 Clipboard | Native Win32 baseline. Instant paste. |
| **Microsoft Word** | `WINWORD.EXE` | `_WwG` (Office RichEdit) | Document canvas | Tier 1 UIA / Tier 2 Clipboard | Preserves document styling. |
| **Google Chrome** | `chrome.exe` | `Chrome_WidgetWin_1` | Omnibox, search, web forms | Tier 2 SendInput Clipboard | Fast clipboard restore required. |
| **Microsoft Edge** | `msedge.exe` | `Chrome_WidgetWin_1` | Web text areas, inputs | Tier 2 SendInput Clipboard | Identical Chromium behavior. |
| **Gmail (Web)** | `chrome.exe` | ContentEditable `div` | Compose body, Subject line | Tier 2 SendInput Clipboard | Avoids Enter simulation to prevent auto-send. |
| **Google Docs** | `chrome.exe` | Custom Canvas / Kix editor | Document canvas | Tier 2 SendInput Clipboard | Pure clipboard injection; bypasses UIA canvas limitation. |
| **WhatsApp Web** | `chrome.exe` | ContentEditable `div` | Message input box | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Zero-Enter invariant prevents sending. |
| **Slack (Desktop)** | `slack.exe` | Electron / Quill editor | Message draft box | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Must never simulate Enter. |
| **Notion (Desktop)** | `notion.exe` | Electron / React canvas | Page block | Tier 2 SendInput Clipboard | Fast cursor placement. |
| **Visual Studio Code**| `code.exe` | Electron / Monaco editor | Code file, terminal, Copilot chat | Tier 2 SendInput Clipboard | Does NOT trigger "Screen Reader Mode" alert. |
| **Cursor** | `cursor.exe` | Electron / Monaco editor | Composer, Chat, Code editor | Tier 2 SendInput Clipboard | Supports voice file tagging (`@filename`). |
| **Windows Terminal** | `wt.exe` | `CASCADIA_HOSTING_WINDOW_CLASS` | Active shell tab | Tier 2 SendInput Clipboard | **CRITICAL SAFETY**: Must NEVER execute commands. |

---

## 3. Tier 6: UI & Human Desktop Validation Gate

A capability is NOT complete merely because backend tests pass. Before declaring Phase 2 complete, the following physical UI actions must be executed and confirmed by a human tester on Windows:
1. **Hub Launch**: Hub window opens cleanly from tray icon and displays all navigation tabs.
2. **Dictionary Management**: User can add a new term in the Hub UI, see it listed in the table, toggle its Starred status, and dictate it into Notepad to see it formatted with exact casing.
3. **Snippets Management**: User can create a snippet with trigger cue `"test snippet"` in the Hub UI, speak `"test snippet"` into Slack, and see the expanded text inserted with zero Enter simulation.
4. **Style Configuration**: User can change active style from Default to Formal in the Hub UI and verify that spoken contractions (*"don't"*) are expanded into formal text (*"do not"*).
5. **HUD Waveform**: Floating HUD displays a live fluctuating VU meter / waveform that responds dynamically to user voice volume.
6. **Shortcut Customization**: User can rebind Push-to-Talk to a custom key in Settings and verify the new key operates.
