# FLOW — Phase 5: Context Awareness & Application Intelligence Parity

## 1. Executive Summary

Phase 5 establishes a production-grade, local-first Context Awareness and Application Intelligence subsystem for FLOW on Windows 10/11 x64 (.NET 9 / Win32).

FLOW dynamically inspects the active Windows foreground application and focused control at session boundaries. Dictation formatting, programming identifier preservation, terminal command safety, and personalization vocabulary scoping adapt deterministically to the user's active working environment without leaking context across applications or violating privacy.

---

## 2. Context Architecture & Domain Models

```
┌────────────────────────────────────────────────────────────┐
│                  VoiceSessionCoordinator                   │
│  - Session onset: calls IUIContextService.CaptureContext() │
│  - Validates IsSensitive (Password/PIN): Fails Closed      │
│  - Scopes FormattingOptions & Personalization Biasing      │
│  - Pre-insertion: Validates target window is still active  │
│  - InvalidateContext(): Clears snapshot upon completion    │
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│              WindowsUIAutomationContextService             │
│  - Win32: GetForegroundWindow, GetWindowThreadProcessId    │
│  - UIA: AutomationElement.FocusedElement, IsPasswordProp   │
│  - UIA TextPattern: Bounded NearbyText & SelectionText     │
│  - Win32: IsWindow + Liveness validation                   │
└──────────────┬──────────────────────────────┬──────────────┘
               │                              │
               ▼                              ▼
┌──────────────────────────────┐┌─────────────────────────────┐
│ RuleBasedApplicationClassif. ││       ContextSnapshot       │
│ - Code (VS Code, Cursor...)  ││ - SessionId (Guid)          │
│ - Terminal (WT, PowerShell)  ││ - Timestamp                 │
│ - Browser (Chrome, Edge...)  ││ - TargetInfo (HWND, PID)    │
│ - Document (Word, Excel...)  ││ - ApplicationCategory       │
│ - GeneralProse (Notepad...)  ││ - FocusedControlInfo        │
│ - Sensitive (Vaults, Auth)   ││ - IsSensitive (bool)        │
│ - Unknown (Safe Fallback)    ││ - NearbyText (max 200)      │
└──────────────────────────────┘│ - SelectionText (max 10000) │
                                └─────────────────────────────┘
```

### Core Contracts:
1. **`ApplicationCategory`**: Strongly-typed enum (`GeneralProse`, `Document`, `Browser`, `Code`, `Terminal`, `Sensitive`, `Unknown`).
2. **`FocusedControlInfo`**: Immutable record storing `ControlType`, `AutomationId`, `ClassName`, `Name`, `IsPassword`, `HasTextPattern`, `HasValuePattern`.
3. **`ForegroundTargetInfo`**: Win32 window properties (`Hwnd`, `ProcessId`, `ProcessName`, `WindowTitle`).
4. **`ContextSnapshot`**: Session-bound immutable snapshot holding context metadata and bounded text.
5. **`IApplicationClassifier` / `RuleBasedApplicationClassifier`**: High-throughput rule classifier evaluating executable identity and window properties.

---

## 3. Application Classification Rules

The classifier matches executable names (case-insensitively, stripping `.exe`) and title markers:

| Category | Typical Executables Recognized | Formatting & Downstream Influence |
| :--- | :--- | :--- |
| **Code** | `code`, `cursor`, `windsurf`, `devenv`, `idea64`, `pycharm64`, `webstorm64`, `clion64`, `rider64`, `goland64`, `rustrover64`, `sublime_text`, `notepad++`, `visualstudio`, `zed` | Prioritizes technical vocabulary, code casing, identifier shields, and code-scoped personal dictionary entries. |
| **Terminal** | `windowsterminal`, `powershell`, `pwsh`, `cmd`, `conhost`, `mintty`, `bash`, `wsl`, `alacritty`, `wezterm`, `hyper`, `tabby`, `wt` | Text-only insertion; flags and paths preserved; **Zero-Enter invariant strictly enforced** (`VK_RETURN = 0`). |
| **Browser** | `chrome`, `msedge`, `firefox`, `brave`, `opera`, `vivaldi`, `arc`, `tor`, `chromium` | Standard web form formatting; conservative casing. |
| **Document** | `winword`, `excel`, `powerpnt`, `onenote`, `acrobat`, `acrord32`, `foxitreader`, `wps`, `libreoffice` | Prose-oriented formatting, complete sentences, punctuation normalization. |
| **GeneralProse** | `notepad`, `wordpad`, `textpad`, `stickynotes` | Standard clean text dictation. |
| **Sensitive** | `credentialuibroker`, `consent`, `keepass`, `1password`, `bitwarden`, `lastpass`, `dashlane`, `enpass` | **Fail Closed**: Recording and command mode blocked; no text extracted. |
| **Unknown** | Any unmapped executable | Conservative generic formatting fallback. |

---

## 4. UI Automation & Text Extraction Strategy

- **Narrowly Scoped**: Inspects only the active focused `AutomationElement` or target window. Never traverses or crawls the full desktop UI tree.
- **Bounded Constraints**:
  - `MaxNearbyCharacters = 200`: Characters preceding the caret extracted via `TextPattern`.
  - `MaxSelectionCharacters = 10000`: Selection range extracted for transforms.
- **Data Injection Defense**: Text extracted from documents/editors is treated strictly as passive data. Prompt injection attempts (e.g. "Ignore previous instructions...") inside document text are never executed.

---

## 5. Inviolable Safety Gates

### A. Password & Credential Exclusion
- If `IsFocusInPasswordField()` returns `true` or target process is in `SensitiveProcesses`:
  - `ContextSnapshot.IsSensitive = true`
  - `NearbyText = null`
  - `SelectionText = null`
  - `VoiceSessionCoordinator` immediately cancels the session, issues a security warning, and suppresses all audio recording and command execution.
  - ASR prompt biasing is completely blocked, preventing credential leakage into local Whisper inference.
  - Secondary verification in `EndSessionAsync` verifies focus has not moved into a password field during dictation before text insertion.

### B. Zero-Enter Invariant
- Text insertion service strictly prohibits `VK_RETURN` (0x0D) and `VK_SEPARATOR` (0x6C).
- Spoken terminal commands (e.g. "git status", "shutdown /s /t 0") are formatted and inserted strictly as plain text characters. No Enter key is simulated, no shell process is spawned.

---

## 6. Lifecycle & Session Isolation

1. **Session Start**: `Guid sessionId = Guid.NewGuid();` captures an immutable `ContextSnapshot`.
2. **Session Execution**: Downstream formatting and biasing use the snapshot.
3. **Pre-Insertion Target Check**: `ValidateTargetStillActive(ActiveTarget)` verifies:
   - Target HWND is still a valid Win32 window (`IsWindow`).
   - Target HWND is still the foreground window (`GetForegroundWindow`).
   - Process ID matches the initial session target.
   - If focus changed to another application, insertion is aborted for safety and the context is invalidated.
4. **Session Cleanup**: `InvalidateContext()` resets `ActiveContext`, `ActiveTarget`, and `ActiveNearbyContext`. Session B never inherits Session A's context.

---

## 7. Performance & Verification Metrics

- **Classification Latency**: < 0.005 ms (average 1.5 µs per operation across 10,000 iterations).
- **UIA Context Extraction**: < 3.5 ms on active WPF controls.
- **Whisper Biasing RTF**: 0.16x Real-Time Factor with application context tokens.
- **Test Suite**:
  - `Flow.Core.Tests`: 473 / 473 passing (+55 Phase 5 tests)
  - `Flow.Windows.Tests`: 119 / 119 passing (+7 Phase 5 physical tests)
  - **Total**: 592 / 592 passing (0 errors, 0 warnings).

---

## 8. Known Limitations

- **Non-Standard Controls**: Applications using custom owner-draw controls that do not implement UI Automation `TextPattern` or `ValuePattern` will yield empty nearby text. Dictation continues safely in fallback mode.
- **Elevated / Integrity Windows**: When dictating into elevated processes (run as Administrator) from a non-elevated FLOW process, UI Automation queries are restricted by Windows UIPI (User Interface Privilege Isolation). FLOW detects target PID/process name but safely falls back to standard insertion.
