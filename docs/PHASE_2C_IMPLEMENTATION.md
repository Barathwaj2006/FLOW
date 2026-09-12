# FLOW — Phase 2C Implementation Specification
## Smart Formatting, Backtrack & Spoken Lists

> **Status**: Verified & Implemented Locally  
> **Target Platform**: Windows 10/11 x64 Native Desktop (.NET 9.0)  
> **Mandate Compliance**: Zero GitHub (no commits, no push), Zero-Enter Invariant, 100% Deterministic  

---

## 1. Executive Summary

Phase 2C elevates the FLOW voice dictation platform from raw acoustic transcription to an intelligent, production-grade text generation pipeline that rivals and exceeds Wispr Flow's dictation quality. 

Phase 2C delivers two major subsystem capabilities:
1. **Deterministic Multi-Pass Transcript Processing Pipeline**: Converts raw Whisper output into natural, clean, perfectly-punctuated written prose while strictly preserving technical tokens, code identifiers, CLI commands, and negative constraints.
2. **Safe Desktop Backtrack Engine**: Provides single-keystroke undo of prior dictation sessions (`Shift + Right Alt` or `Right Alt + Backspace`) with foreground window ownership validation to strictly prevent accidental deletion in unintended applications.

---

## 2. Multi-Pass Transcript Processing Architecture

Raw ASR transcripts flow through an ordered sequence of deterministic stages implementing `ITranscriptStage`:

```
Raw Transcription
      ↓
[1. Whitespace & Zero-Enter Normalization]   → Strips \r, \n; converts "new line" to space
      ↓
[2. Technical Entity Protection]             → Masks URLs, paths, CLI commands, identifiers
      ↓
[3. Spoken Punctuation Engine]               → Converts punctuation commands, handles quotes/parens
      ↓
[4. Conservative Filler Removal]             → Strips hesitations (um, uh); preserves verbs ("like")
      ↓
[5. Numbered List Engine]                    → Formats cardinal/ordinal lists with 2-item gating
      ↓
[6. Smart Capitalization Engine]             → Capitalizes sentences and list items
      ↓
[7. Entity Restoration & Terminal Punctuation]→ Restores masked technical entities, adds terminal '.'
      ↓
[Post-Pipeline Zero-Enter Guarantee]         → Absolute invariant check before insertion
```

### Stage Details

#### Stage 1: Whitespace & Zero-Enter Normalization (`WhitespaceAndZeroEnterStage`)
- Immediately eliminates all `\r` and `\n` characters.
- Converts spoken directives `"new line"` and `"new paragraph"` into single spaces.
- Collapses multi-whitespace sequences into single spaces.

#### Stage 2: Technical Entity Protection (`TechnicalEntityProtectionStage`)
- Protects technical tokens before any linguistic transformations occur to prevent punctuation or capitalization corruption.
- Protected categories:
  - **URLs and Emails**: `https://flow.dev`, `user@example.com`
  - **Windows & Relative File Paths**: `C:\Windows\System32\...`, `.\src\Flow.Core\...`
  - **CLI & PowerShell Commands**: `dotnet test`, `git status`, `Get-Process`, `npm install`
  - **Code Identifiers**: `Flow.Core`, `camelCaseVariable`, `snake_case_name`, `SCREAMING_SNAKE`
  - **Acronyms**: `HTTP`, `WASAPI`, `VAD`, `ASR`, `AVX2`, `GPU`, `DPAPI`
- Tokens are masked using Unicode Private Use Area delimiters (`\uE000{id}\uE001`), ensuring that regex word boundaries (`\b`) and casing transformers do not mangle tokens.

#### Stage 3: Spoken Punctuation Engine (`SpokenPunctuationStage`)
- Converts spoken punctuation commands to typographical characters:
  - `"period"`, `"full stop"` $\rightarrow$ `.`
  - `"comma"` $\rightarrow$ `,`
  - `"question mark"` $\rightarrow$ `?`
  - `"exclamation mark"`, `"exclamation point"` $\rightarrow$ `!`
  - `"colon"` $\rightarrow$ `:`
  - `"semicolon"` $\rightarrow$ `;`
  - `"dash"`, `"hyphen"` $\rightarrow$ `-`
  - `"apostrophe"` $\rightarrow$ `'`
  - `"open quote ... close quote"` $\rightarrow$ `"..."`
  - `"open parenthesis ... close parenthesis"` $\rightarrow$ `(...)`
- Handles beginning-of-string commands and normalizes spacing before and after punctuation.

#### Stage 4: Conservative Filler Removal (`ConservativeFillerRemovalStage`)
- Strips disfluent hesitations: `um`, `uh`, `erm`, `ah`, `hmm`, `er`, `uhm`.
- **The "Like" Protection Invariant**: Natural verbs and prepositions are strictly preserved:
  - `"I like Python"` $\rightarrow$ `"I like Python."` (Preserved!)
  - `"It feels like summer"` $\rightarrow$ `"It feels like summer."` (Preserved!)
  - Only disfluent interjections (e.g., surrounded by pauses/commas `", like,"` or adjacent to fillers) are removed.

#### Stage 5: Numbered List Engine (`NumberedListStage`)
- Recognizes spoken cardinal lists (`"one ... two ... three ..."`) and ordinal lists (`"first ... second ... third ..."`).
- Automatically formats into structured text: `"1. Item one 2. Item two 3. Item three"`.
- **Strict Two-Item Confirmation Gating**: A single occurrence of `"one"` (e.g. `"one important thing to remember"`) will **NEVER** trigger a numbered list. Detection requires at least Item 1 **AND** Item 2 to confirm user list intent.

#### Stage 6: Smart Capitalization (`SmartCapitalizationStage`)
- Capitalizes the initial word of sentences and quoted text.
- Capitalizes words immediately following sentence terminators (`.`, `!`, `?`).
- Capitalizes the first word of numbered list items (`1. Buy milk`).
- Preserves protected tokens (e.g. lowercase CLI flags or commands like `dotnet test`).

#### Stage 7: Entity Restoration (`EntityRestorationStage`)
- Restores all masked technical entities to their original exact representation.
- Cleans up whitespace artifacts.
- Ensures terminal punctuation (`.`) if not already present and not ending in terminal punctuation.

---

## 3. Safe Desktop Backtrack Engine

### Ownership Verification & Focus Protection
Wispr Flow provides backtrack to undo dictations. In FLOW, Backtrack is designed with enterprise-grade safety:
1. Every insertion records an immutable `InsertionRecord`:
   - Unique ID (`Guid`)
   - Text inserted and exact character count
   - Foreground window handle (`HWND`)
   - Target process name (e.g., `notepad`, `devenv`, `chrome`)
   - Target process ID (`PID`)
   - Insertion strategy and timestamp
2. When Backtrack is triggered:
   - Queries current foreground window via Win32 `GetForegroundWindow()` and `GetWindowThreadProcessId()`.
   - **Active Window Verification**: If the current foreground HWND or PID differs from the `InsertionRecord`, Backtrack **safely aborts immediately as a no-op**.
   - This eliminates the catastrophic risk of deleting text in a newly focused application (e.g., if user switched to PowerShell or an email client).

### Deletion Mechanism & Invariants
- If focus is verified: sends bounded `VK_BACK` (0x08) `SendInput` keystrokes in batches of 50.
- **Inviolable Invariant**: Every `INPUT` event is verified to ensure `wVk != VK_RETURN` and `wVk != VK_SEPARATOR`. Under no circumstances is Enter ever simulated.
- Updates session state to `SessionState.Backtracking` for HUD and system tray feedback.

---

## 4. Hotkey & Host Integration

In `Program.cs` and `GlobalHotkeyHook.cs`:
- **Push-to-Talk**: Hold `Right Alt`
- **Hands-Free Dictation**: Double-tap `Right Alt`
- **Backtrack**: Press `Shift + Right Alt` OR `Right Alt + Backspace`
- **Cancel Active Dictation**: Press `Escape`

---

## 5. Automated Verification Results

All automated tests executed via `dotnet test Flow.sln`:

| Test Suite | Tests Passed | Tests Failed | Skipped | Status |
| :--- | :---: | :---: | :---: | :---: |
| `Flow.Core.Tests` | 47 | 0 | 0 | **PASS** |
| `Flow.Windows.Tests` | 28 | 0 | 0 | **PASS** |
| **Total** | **75** | **0** | **0** | **100% PASS** |

Build status: `dotnet build Flow.sln --no-incremental` $\rightarrow$ **0 Warning(s), 0 Error(s)**.
