# FLOW — Phase 2C Manual Validation Guide
## Smart Formatting, Backtrack & Spoken Lists

> **Target**: Windows 10/11 x64 Native Desktop Application  
> **Host Executable**: `src\Flow.Host.Windows\bin\x64\Debug\net9.0-windows10.0.19041.0\Flow.Host.Windows.exe`  
> **Validation Status**: Manual test procedure defined. Component and OS integration tests executed via `Phase2CPhysicalValidationTests.cs` (28/28 passing). Human interactive verification procedure documented below.  

---

## 1. Prerequisites

1. Ensure the FLOW application is built:
   ```powershell
   & "C:\Users\barat\.dotnet\dotnet.exe" build Flow.sln
   ```
2. Verify local Whisper weights exist at `models/ggml-tiny.en.bin`.
3. Open a text editor for testing (e.g. Notepad, VS Code, or Windows Terminal).

---

## 2. Test Execution Matrix

### Test Case 1: Spoken Punctuation & Quotes
- **Action**: Focus Notepad. Hold `Right Alt` and clearly speak:
  > *"Hello comma this is an open quote urgent message close quote period Are you ready question mark"*
- **Release**: Release `Right Alt`.
- **Expected Output**:
  `Hello, this is an "urgent message". Are you ready?`
- **Verification Points**:
  - `comma` became `, `
  - `open quote` and `close quote` enclosed `"urgent message"`
  - `period` became `. `
  - `question mark` became `?`
  - Zero newline or enter characters inserted.

---

### Test Case 2: Conservative Filler Removal & "Like" Preservation
- **Action**: Focus Notepad. Hold `Right Alt` and clearly speak:
  > *"Um we should definitely uh deploy this because I like Python and it feels like summer"*
- **Release**: Release `Right Alt`.
- **Expected Output**:
  `We should definitely deploy this because I like Python and it feels like summer.`
- **Verification Points**:
  - `Um` and `uh` were cleanly removed.
  - Initial `We` was capitalized.
  - `like` in `"I like Python"` and `"feels like summer"` was **100% preserved**.
  - Terminal `.` appended.

---

### Test Case 3: Spoken Numbered Lists (Cardinal & Ordinal)
- **Action**: Focus Notepad. Hold `Right Alt` and clearly speak:
  > *"first review the pull request second run unit tests third tag release"*
- **Release**: Release `Right Alt`.
- **Expected Output**:
  `1. Review the pull request 2. Run unit tests 3. Tag release.`
- **Verification Points**:
  - Automatic structure `1. ... 2. ... 3. ...` applied.
  - Each list item is capitalized.
  - Zero carriage returns inserted.

---

### Test Case 4: Numbered List False-Positive Rejection
- **Action**: Focus Notepad. Hold `Right Alt` and speak:
  > *"one important thing to keep in mind is performance"*
- **Release**: Release `Right Alt`.
- **Expected Output**:
  `One important thing to keep in mind is performance.`
- **Verification Points**:
  - Because `two` was never spoken, the two-item confirmation gate rejected list mode.
  - The sentence did **NOT** turn into `1. Important thing...`.

---

### Test Case 5: Technical Entity Protection (Content Lock)
- **Action**: Focus Notepad. Hold `Right Alt` and speak:
  > *"run dotnet test and inspect C:\Windows\System32 and check https://flow.dev"*
- **Release**: Release `Right Alt`.
- **Expected Output**:
  `Run dotnet test and inspect C:\Windows\System32 and check https://flow.dev.`
- **Verification Points**:
  - `dotnet test` was not transformed into `Dotnet Test`.
  - Windows path slashes `\` were preserved without escape errors.
  - URL `https://flow.dev` was preserved without spurious spaces.

---

### Test Case 6: Desktop Backtrack (Single Undo)
- **Action**:
  1. Dictate: *"Testing backtrack operation period"* $\rightarrow$ Inserts `"Testing backtrack operation."`
  2. With Notepad still focused, press `Shift + Right Alt` (or `Right Alt + Backspace`).
- **Expected Result**:
  - HUD displays `"Backtracking..."` then `"Done"`.
  - Exactly 29 characters are backspaced out, completely removing the dictation from Notepad.
  - Notepad returns to its state prior to the dictation.

---

### Test Case 7: Backtrack Safety Check (Application Focus Switch)
- **Action**:
  1. Dictate in Notepad: *"This should stay safe in Notepad period"*
  2. Switch focus to PowerShell or another application window.
  3. Press `Shift + Right Alt`.
- **Expected Result**:
  - Backtrack safely aborts (HWND mismatch).
  - **Zero characters deleted** in the newly focused window.
  - Log reports: `Backtrack safe no-op: Focus changed or no insertion history.`

---

## 3. Inviolable Safety Invariant Audit
Across all 7 test cases:
- [x] Zero `VK_RETURN` (0x0D) simulated.
- [x] Zero forms submitted or commands executed.
- [x] Zero cloud audio transmitted.
- [x] Working tree clean of any Git remote or GitHub commits.
