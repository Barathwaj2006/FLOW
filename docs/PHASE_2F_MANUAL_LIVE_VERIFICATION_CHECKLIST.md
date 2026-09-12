# PHASE_2F_MANUAL_LIVE_VERIFICATION_CHECKLIST.md — Phase 2F Interactive Verification Guide

> **Document Type**: Manual Desktop Physical Verification Checklist & Test Plan  
> **Target Subsystem**: Phase 2F — Safe Voice Command Mode & Selection Transforms  
> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Status**: Prepared for Human Operator Execution (Local Machine Only)  
> **Engineering Rule**: Physical human verification required for Level 5 certification. Do not pre-fill results.  

---

## 1. Prerequisites & Environment Verification

Before starting the manual verification sequence, ensure the host system meets the following baseline criteria:

- [ ] Windows 10/11 x64 (Build 19041+ or 22000+)
- [ ] FLOW host executable compiled and present: `src\Flow.Host.Windows\bin\x64\Debug\net9.0-windows10.0.19041.0\Flow.Host.Windows.exe`
- [ ] Local Whisper model verified on disk: `%LOCALAPPDATA%\FLOW\models\ggml-tiny.en.bin` (77,704,715 bytes, SHA-256 `921E4CF8686FDD993DCD081A5DA5B6C365BFDE1162E72B08D75AC75289920B1F`)
- [ ] Physical microphone endpoint active and unmuted in Windows Settings (e.g. Intel® Smart Sound Digital Mic or USB headset)
- [ ] Windows Notepad (`notepad.exe`) available
- [ ] Windows Terminal (`wt.exe`) available
- [ ] Visual Studio Code (`Code.exe`) or Cursor available

---

## 2. Capability Verification Checklists

### A. WF-036 — Dedicated Global Shortcut (`Ctrl + Right Alt`)
- [ ] Start FLOW via PowerShell / Terminal: `dotnet run --project src\Flow.Host.Windows`
- [ ] Keep FLOW running in the background (HUD shows "Ready" and hides; tray icon visible)
- [ ] Open an external application (Notepad) and focus its text editor
- [ ] Press and hold `Ctrl + Right Alt`: verify Command Mode activates (Floating HUD displays `🪄 Command: Listening...`)
- [ ] Verify normal dictation PTT is NOT triggered
- [ ] Release `Ctrl + Right Alt`: verify HUD transitions to `🪄 Transforming...` then `🪄 Transformed`
- [ ] Verify target application (Notepad) retains active foreground focus throughout
- [ ] Test Escape cancellation: Press `Ctrl + Right Alt`, then press `Escape` before releasing — verify session aborts cleanly with zero insertion and HUD resets to Idle
- [ ] Test repeatability: Perform 3 consecutive activations; verify identical state transitions without hanging

### B. WF-037A — Selection-Aware Voice Transforms (Real Applications)

#### Test 1: Notepad — Bullet List Transformation
- [ ] In Notepad, enter:
  ```text
  milk, eggs, bread
  ```
- [ ] Select all text (`Ctrl + A` or drag selection)
- [ ] Hold `Ctrl + Right Alt`, speak clearly into the physical microphone:
  > *"make bullet points"*
- [ ] Release hotkey
- [ ] Verify Whisper transcribes the phrase
- [ ] Verify selected text is replaced with:
  ```text
  • Milk • Eggs • Bread
  ```
- [ ] Verify surrounding unselected text (if any) remains completely untouched
- [ ] Verify Zero-Enter: No Enter key (`VK_RETURN`) simulated; no newline (`\r\n`) inserted

#### Test 2: Notepad — Programmatic Casing Transformations
- [ ] In Notepad, enter:
  ```text
  user profile manager
  ```
- [ ] Select the text
- [ ] Test `camel case`: Speak *"camel case"* $\rightarrow$ Expected: `userProfileManager`
- [ ] Select the text again
- [ ] Test `snake case`: Speak *"snake case"* $\rightarrow$ Expected: `user_profile_manager`
- [ ] Select the text again
- [ ] Test `pascal case`: Speak *"pascal case"* $\rightarrow$ Expected: `UserProfileManager`
- [ ] Verify all casing transforms execute deterministically without invented words

#### Test 3: Windows Terminal — Safe Console Transformation
- [ ] Open Windows Terminal (`wt.exe`)
- [ ] In a safe, non-executing prompt area, type:
  ```text
  hello world
  ```
- [ ] Select `hello world`
- [ ] Hold `Ctrl + Right Alt`, speak:
  > *"make uppercase"*
- [ ] Release hotkey
- [ ] Verify text is replaced with `HELLO WORLD`
- [ ] **CRITICAL SAFETY CHECK**: Verify no Enter (`0x0D`) was simulated; no shell command was submitted or executed

#### Test 4: VS Code / Cursor — Code Identifier Transformation
- [ ] Open a text file in VS Code or Cursor
- [ ] Enter:
  ```text
  user profile manager
  ```
- [ ] Select the text
- [ ] Hold `Ctrl + Right Alt`, speak:
  > *"camel case"*
- [ ] Release hotkey
- [ ] Verify selection is replaced with `userProfileManager`
- [ ] Verify editor focus is retained; no terminal opens; no code runs; no Enter generated

### C. WF-037B — Flow Bar / HUD Non-Activating Behavior
- [ ] During command recording, verify Floating HUD pill appears with `🪄` wand icon
- [ ] Verify HUD never steals foreground focus from Notepad, Terminal, or VS Code
- [ ] Verify HUD never becomes the text insertion target
- [ ] Verify HUD state sequence:
  $$\text{Idle} \longrightarrow \text{🪄 Listening} \longrightarrow \text{🪄 Transforming} \longrightarrow \text{🪄 Transformed} \longrightarrow \text{Ready / Idle}$$
- [ ] Verify pressing `Escape` during recording transitions HUD immediately to `Cancelled` then `Ready / Idle`

### D. WF-038 — Zero-Destructive Execution Safety & Dictation Isolation

#### Test 1: Command Mode Rejection of Dangerous Instructions
- [ ] In Notepad, select any harmless word (e.g. `test`)
- [ ] Hold `Ctrl + Right Alt`, speak:
  > *"shutdown"* or *"open terminal"* or *"delete file"*
- [ ] Release hotkey
- [ ] Verify `DeterministicCommandSafetyPolicy` reports **Blocked** or **Unknown**
- [ ] Verify HUD shows warning / cancellation
- [ ] Verify **zero process is spawned**, zero shell command executes, zero text is deleted

#### Test 2: Normal Dictation Isolation
- [ ] Release all modifier keys
- [ ] Hold `Right Alt` alone (Normal Dictation PTT)
- [ ] Speak:
  > *"shutdown slash s slash t zero"*
- [ ] Release `Right Alt`
- [ ] Verify text is inserted literally as text: `shutdown /s /t 0`
- [ ] Verify system does NOT execute shutdown; normal dictation remains 100% inert text

### E. Password Field Exclusion Regression
- [ ] Open a real Windows password prompt (or safe local password input field)
- [ ] Focus the password field and enter placeholder characters
- [ ] Hold `Ctrl + Right Alt`
- [ ] Verify Command Mode immediately blocks: selection extraction returns empty string
- [ ] Verify password content never appears in HUD, console logs, or transcripts
- [ ] Verify zero insertion or alteration occurs

### F. Final Verification Gate
- [ ] Complete test evidence recorded in template below
- [ ] Automated regression suite confirms 360/360 passing tests
- [ ] Zero compiler errors, zero warnings
- [ ] Zero P0 / P1 defects observed during manual execution

---

## 3. Physical Verification Evidence Template

*(To be completed by the human operator following manual desktop testing)*

```text
================================================================================
FLOW PHASE 2F — HUMAN OPERATOR PHYSICAL VERIFICATION EVIDENCE
================================================================================

Date: 
Windows version: 
FLOW build/configuration: 
Microphone: 
Whisper model: 
Target application: 
Target process: 
Target HWND: 

--------------------------------------------------------------------------------
CAPABILITY MATURITY VERIFICATION
--------------------------------------------------------------------------------

WF-036 result (Global Shortcut Ctrl+RightAlt): 
WF-037A result (Selection Transform across Apps): 
WF-037B result (Flow Bar / HUD Non-Activating): 
WF-038 result (Zero-Destructive Execution Safety): 

--------------------------------------------------------------------------------
MANDATORY SAFETY ASSERTIONS
--------------------------------------------------------------------------------

Real microphone: 
Zero-Enter: 
Focus preservation: 
Password exclusion: 
Normal dictation isolation: 

--------------------------------------------------------------------------------
OBSERVATIONS & NOTES
--------------------------------------------------------------------------------
Notes: 


Operator Signature / Certification: 
================================================================================
```
