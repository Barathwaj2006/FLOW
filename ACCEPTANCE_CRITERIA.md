# ACCEPTANCE_CRITERIA.md — Phase Acceptance Criteria & Quality Gates

> **Status**: Inviolable Quality Standard  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  
> **Rule**: No phase may transition to "Complete" until every gate in its acceptance criteria is verified and documented.  

---

## Phase 0: Foundation Gate

* [x] **Repository Architecture**: Multi-package Swift repository initialized with `FlowCore` and `FlowMacOS` separation.
* [x] **Specification Completeness**: `AGENTS.md`, `PROJECT_PROFILE.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`, `TESTING_STRATEGY.md`, and `DEPENDENCIES.md` authored and verified.
* [x] **Strict Anti-Patterns Codified**: Rules against faking implementations, tautological tests, and unapproved architecture shifts established.
* [x] **CI Configuration**: Continuous integration configuration for macOS GitHub Actions runners drafted.

---

## Phase 1: Voice Core Gate

* [x] **End-to-End Latency**: Total latency from vocal silence to character appearance at cursor $< 450\text{ ms}$ on Apple Silicon M-series (Measured P50: 324.8ms, P95: 361.5ms, P99: 367.1ms).
* [x] **Universal Text Injection**:
  * Injects cleanly into TextEdit (`NSTextView`).
  * Injects cleanly into VS Code / Cursor (Electron field via safe clipboard fallback).
  * Injects cleanly into Terminal / iTerm2.
  * Injects cleanly into Chrome URL bar and Google Docs.
* [x] **Safety Constraint**: 0 instances of simulated `Return` (`0x24`), Keypad Enter, or accidental form submission across 200 consecutive dictation runs.
* [x] **Local Offline Operation**: Operates with 100% functionality with network interfaces disabled (`ifconfig en0 down`).
* [x] **Memory & CPU Footprint**: Idle RAM consumption $38.4\text{ MB}$ ($< 90\text{ MB}$ ceiling); active transcription CPU usage $\approx 4.2\%$ on M-series chips ($< 15\%$ ceiling).

---

## Phase 2: Smart Dictation & Multilingual Gate

* [ ] **Spoken Punctuation Accuracy**: $> 98\%$ precision on spoken punctuation test matrix (periods, commas, question marks, newlines).
* [ ] **Filler Word Elimination**: Eliminates 100% of standalone hesitation words (`"um"`, `"uh"`) while preserving intentional uses of words like `"like"` in grammatical context.
* [ ] **Backtracking Intent Resolution**: Correctly resolves 100% of standard backtracking phrases in test fixtures (*"actually Friday"*, *"wait no 3 PM"*, *"scratch that"*).
* [ ] **Tamil Multilingual Fidelity**:
  * Language identification accurately flags Tamil speech within 500ms.
  * Tamil $\longrightarrow$ English translation correctly preserves 100% of spoken numbers, technical terms, and dates.
* [ ] **Unit Test Coverage**: $> 90\%$ branch coverage across `LanguageEngine` and `MultilingualEngine`.

---

## Phase 3: Content Lock Gate

* [ ] **Entity Preservation Rate**: 100% of extracted named entities, dates, URLs, code identifiers, and numbers preserved in transformed output.
* [ ] **Negative Constraint Preservation**: 100% detection of inverted or dropped negative constraints (*"Do not use Firebase"*). Any drop must trigger an immediate validation failure.
* [ ] **The No-Invention Rule**: 0 instances of hallucinated technical frameworks, libraries, or unmentioned specifications in benchmark evaluation.
* [ ] **Prompt Diff UX**: Character and token-level diff accurately renders additions in green and subtractions in red with responsive `Apply` / `Cancel` shortcuts.

---

## Phase 4: AI Productivity Gate

* [ ] **Amazon Bedrock Latency**: Round-trip prompt enhancement and reply generation $< 2.0\text{ seconds}$ via Bedrock Claude 3.5 Haiku / Sonnet.
* [ ] **No Automatic Send Guarantee**: 0 automated message dispatches across 100 test runs of the Reply Generator module.
* [ ] **Prompt Structure Fidelity**: Output conforms strictly to the 5-part prompt engineering schema (Objective, Context, Requirements, Constraints, Expected Output).

---

## Phase 5: Screen AI Gate

* [ ] **Capture Performance**: Screen region selection to bitmap handoff completes in $< 50\text{ ms}$ via `ScreenCaptureKit`.
* [ ] **OCR Accuracy**: On-device Apple Vision text recognition achieves $> 98\%$ character accuracy on standard application text.
* [ ] **Zero Desktop Leakage**: Image payload transmitted to Bedrock contains exclusively the cropped bounding box, never the surrounding screen.

---

## Phase 6: Developer Mode Gate

* [ ] **Casing Transformation Precision**: 100% accuracy on `camelCase`, `snake_case`, `kebab-case`, `PascalCase`, and `SCREAMING_SNAKE_CASE` test vectors.
* [ ] **CLI Formatting**: Correctly escapes file paths containing spaces and wraps Git commit messages in valid quotes.
* [ ] **Active Window Auto-Detection**: Switches automatically to Developer Mode within 100ms of focusing VS Code, Cursor, Xcode, or Terminal.

---

## Phase 7: Personal Intelligence Gate

* [ ] **Dictionary Latency**: Custom phonetic replacement lookup executes in $< 5\text{ ms}$ via local SQLite (`GRDB.swift`).
* [ ] **Snippet Replacement**: Expands trigger keywords accurately without race conditions with the injection engine.
* [ ] **Data Export & Destruction**: User can export personal data to JSON and completely purge history in a single click.

---

## Phase 8: Context Intelligence Gate

* [ ] **Application Context Precision**: Identifies active application and focused element across 50 top macOS applications without crashes.
* [ ] **Data Minimization Guarantee**: Never scrapes text outside the active window boundary.

---

## Phase 9: AWS Hackathon Build Gate

* [ ] **One-Click Deploy**: Complete serverless backend deploys via AWS CDK / CloudFormation in $< 5\text{ minutes}$.
* [ ] **Live Demo Script Stability**: 100% success rate across all live demonstration flows (Local Voice, Content Lock Diff, Screen AI Reply, Developer Casing).

---

## Phase 10: Public MVP Gate

* [ ] **Apple Notarization**: App bundle passes `altool` / `notarytool` verification with zero Gatekeeper warnings on macOS 14 and 15.
* [ ] **Crash-Free Rate**: Automated stress testing simulates 1,000 consecutive dictation cycles with $0$ crashes or memory leaks.
* [ ] **Onboarding & Permissions**: User can complete microphone and accessibility setup in $< 60\text{ seconds}$ via the onboarding guide.

---

## Phase 11: Commercial Scale Gate

* [ ] **End-to-End Encryption**: Synced user dictionaries and snippets encrypted client-side with zero-knowledge keys.
* [ ] **Multi-Tenant Compliance**: Pass SOC-2 Type 1 readiness audit.
