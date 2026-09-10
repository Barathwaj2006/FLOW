# ACCEPTANCE_CRITERIA.md — Phase Acceptance Criteria & Quality Gates (Windows Native)

> **Status**: Windows Realignment Baseline  
> **Target Platform**: Windows 10/11 x64 Native Desktop Application  
> **Rule**: No phase may transition to "Complete" until every gate in its acceptance criteria is empirically verified and documented.  

---

## Phase 0: Windows Architecture Realignment Gate

* [x] **Repository Audit**: All existing files audited, classified into Classes A/B/C/D, and documented in `MIGRATION_MATRIX.md`.
* [x] **Specification Realignment**: `AGENTS.md`, `PROJECT_PROFILE.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`, `TESTING_STRATEGY.md`, and `DEPENDENCIES.md` re-authored for Windows native platform.
* [x] **Windows Architecture Options Evaluated**: Rigorous trade-off analysis completed comparing Option A (.NET/WinUI), Option B (C++20/WinUI), and Option C (.NET Host + Native C++ AI).
* [x] **Inviolable Directives Established**: Strict rules against automated `VK_RETURN` (Enter), platform drift, and premature implementation codified.
* [x] **Zero Premature Code**: No Phase 1 production code written before human review and authorization.

---

## Phase 1: Voice Core Gate (Pending Authorization)

* [ ] **Target Application Verification**: Text insertion verified across diverse Windows application architectures:
  * Win32 / Native: Windows Notepad (`notepad.exe`).
  * WinUI 3 / XAML: Windows Settings / Calculator / Modern Notepad.
  * WPF / WinForms: Legacy enterprise test target.
  * Chromium / Edge: Microsoft Edge / Google Chrome address bar and Google Docs.
  * Electron: VS Code (`code.exe`), Slack, Discord.
  * Terminal Emulators: Windows Terminal, PowerShell, Command Prompt (CMD).
  * Rich Text: Microsoft Word (`WINWORD.EXE`).
* [ ] **Safety Constraint (Zero Enter)**: Exactly 0 instances of simulated `VK_RETURN` (`0x0D`), `VK_SEPARATOR`, or keypad enter across 200 consecutive dictation runs.
* [ ] **Local Offline Operation**: Operates with 100% functionality with network adapter disabled (`Disable-NetAdapter`).
* [ ] **Empirical Latency Characterization**: Measure and report actual P50, P95, and P99 latency percentiles without unverified marketing claims.
* [ ] **Resource Consumption**: Idle memory $< 100\text{ MB}$; CPU usage $< 15\%$ peak during active inference.
* [ ] **Device Robustness**: Handles USB and Bluetooth microphone disconnections and reconnections without application crash.

---

## Phase 2: Smart Dictation & Multilingual Gate

* [ ] **Spoken Punctuation Precision**: $> 98\%$ accuracy across standard punctuation test suite (commas, periods, colons, newlines).
* [ ] **Filler Word Elimination**: 100% elimination of standalone hesitations ("um", "uh") with zero grammatical false positives.
* [ ] **Backtracking Intent Resolution**: Resolves 100% of standard backtracking phrases in test corpus (*"actually Friday"*, *"scratch that"*).
* [ ] **Tamil Multilingual Fidelity**: Accurately detects Tamil speech and translates to English without dropping numbers or technical terms.

---

## Phase 3: Content Lock Gate

* [ ] **Entity Preservation Rate**: 100% of extracted named entities, dates, URLs, code identifiers, and numbers preserved in transformed output.
* [ ] **Negative Constraint Preservation**: 100% detection of inverted or dropped negative constraints (*"Do not use Firebase"*).
* [ ] **The No-Invention Rule**: 0 instances of hallucinated technical frameworks or unmentioned specifications.
* [ ] **Prompt Diff UX**: Accurate visual diff rendering with responsive Apply (`Ctrl+Enter`) and Cancel (`Esc`) shortcuts.

---

## Phase 4: AI Productivity Gate

* [ ] **No Automatic Send Guarantee**: 0 automated message dispatches across 100 test runs of Reply Generator.
* [ ] **Bedrock Payload Minimization**: Zero microphone audio sent to AWS; only cleaned text prompt transmitted.
* [ ] **Prompt Structure Schema**: 100% compliance with 5-part engineering prompt structure.

---

## Phase 5: Screen AI Gate

* [ ] **Capture Scope**: Only user-selected rectangular region captured; 0 instances of uncropped desktop leakage.
* [ ] **Windows Graphics Capture Performance**: Region capture handoff completes in $< 50\text{ ms}$ via Direct3D11.
* [ ] **DPI Awareness**: Renders pixel-perfect selection across multi-monitor setups with mixed DPI scaling (100%, 125%, 150%, 200%).

---

## Phase 6: Developer Mode Gate

* [ ] **Casing Transformation Precision**: 100% accuracy on `camelCase`, `snake_case`, `kebab-case`, `PascalCase`, and `SCREAMING_SNAKE_CASE` test vectors.
* [ ] **PowerShell / CLI Awareness**: Correctly escapes Windows file paths with spaces (`C:\Program Files\...`) and quotes arguments.
* [ ] **Active Window Auto-Detection**: Switches automatically to Developer Mode upon focusing VS Code, Visual Studio, or Windows Terminal.

---

## Phase 7: Personal Intelligence Gate

* [ ] **Dictionary Lookup Latency**: Custom phonetic replacement executes in $< 5\text{ ms}$ via SQLite.
* [ ] **Snippet Replacement**: Expands trigger keywords without clipboard race conditions.
* [ ] **DPAPI Encryption**: SQLite database file verified encrypted at rest using Windows DPAPI.

---

## Phase 8: Context Intelligence Gate

* [ ] **Application Context Precision**: Identifies active application and focused element across 50 top Windows applications without crashes.
* [ ] **Data Minimization Guarantee**: Never scrapes text outside the active window boundary.

---

## Phase 9: AWS Hackathon Build Gate

* [ ] **One-Click Deploy**: Serverless stack deploys via AWS SAM / CloudFormation in $< 5\text{ minutes}$.
* [ ] **Live Demo Script Stability**: 100% success rate across all live demonstration flows on Windows 11.

---

## Phase 10: Public MVP Gate

* [ ] **Windows App Packaging**: MSIX / standalone installer installs cleanly.
* [ ] **Crash-Free Rate**: Automated stress testing simulates 1,000 consecutive dictation cycles with $0$ crashes or memory leaks.

---

## Phase 11: Commercial Scale Gate

* [ ] **Enterprise Security**: SOC-2 Type 1 readiness and zero-knowledge synchronization.
