# PROJECT_PROFILE.md — Product Master Profile

# FLOW: AI Voice Productivity Platform for macOS

> **Product Vision**: A system-wide, voice-native AI productivity layer for macOS that enables users to dictate naturally into any application, clean and format speech with strict fidelity, transform unstructured thoughts into structured prompts, reason over screen contents, and write code seamlessly—all grounded in local-first privacy and powered by AWS Bedrock for advanced cloud intelligence.

---

## 1. Executive Summary & Market Positioning

### The Opportunity
Current speech-to-text tools suffer from two extremes:
1. **Dumb Dictation**: Legacy tools (Apple Dictation, Dragon) transcribe phonemes into literal text without context, leaving filler words ("um", "uh"), spoken corrections, broken capitalization, and zero syntax awareness.
2. **Lossy / Hallucinatory Cloud AI Wrappers**: Emerging AI speech tools often pipe all microphone audio to remote servers, exhibit high latency (1–3 seconds), and rewrite user input indiscriminately—frequently inventing requirements, altering variable names, dropping negative constraints, or corrupting technical code.

### The FLOW Advantage: Competitive Positioning vs. Wispr Flow
FLOW competes directly with Wispr Flow while establishing deep technological differentiation:

| Dimension | Wispr Flow / Competitors | FLOW (This Product) |
| :--- | :--- | :--- |
| **Privacy Architecture** | Audio streamed to cloud servers for transcription | **100% Local Voice Core**: Audio never leaves the machine for dictation. |
| **Fidelity Guarantee** | Black-box LLM rewrites prone to hallucination | **Content Lock**: Formally verifies preservation of entities, numbers, code, and constraints before insertion. |
| **Spoken Corrections** | Blindly concatenates speech or loses context | **Intent-Aware Backtracking**: Resolves phrases like *"send it tomorrow actually Friday"* into *"Send it tomorrow—actually, Friday."* |
| **Developer Ergonomics** | Mangles code syntax, camelCase, and terminals | **Native Developer Mode**: Zero-friction case switching (`camelCase`, `snake_case`, `kebab-case`), terminal commands, and syntax awareness. |
| **Multimodal Screen AI** | Text-only or cloud-tethered capture | **Native Screen AI**: High-performance region capture via `ScreenCaptureKit` + on-device OCR + Bedrock reasoning. |
| **Cloud Intelligence** | Proprietary cloud lock-in | **Amazon Bedrock Orchestration**: Cloud power reserved exclusively for high-leverage generative tasks (Prompt Engineering, Screen Reasoning). |
| **Multilingual Support** | Generalist models with poor regional fidelity | **Specialized Regional Fidelity**: Native support for code-switched speech (e.g., Tamil + English) and dialect translation. |

---

## 2. The Three Core Philosophies

### 1. LOCAL (Offline Sovereignty)
* Core voice functionality is self-contained.
* Global hotkey capture, Voice Activity Detection (VAD), Apple Silicon Neural Engine (ANE) / Metal speech recognition, rule-based formatting, and cursor text insertion operate with zero internet connectivity.
* No microphone audio packets ever leave the user's computer.

### 2. FAITHFUL (Content Lock & Intent Integrity)
* AI transformations must clean presentation without distorting substance.
* **Permitted Improvements**: Grammar, punctuation, capitalization, spelling, filler-word elimination, stutter removal, backtracking cleanup, and clean whitespace.
* **Protected Substance (Content Lock)**:
  * Entities: Names, email addresses, phone numbers, URLs, dates.
  * Quantitative Data: Numbers, metrics, pricing, units of measurement.
  * Technical Artifacts: Programming languages, frameworks, libraries, variable names, functions, file paths, shell commands, SQL queries.
  * Logic & Intent: Negative instructions (*"Do not use Firebase"* $\rightarrow$ must NEVER use Firebase), architectural constraints, acceptance criteria.
* **The No-Invention Rule**: The system never manufactures facts, dependencies, or specifications the user did not state.

### 3. INTELLIGENT (Targeted Cloud Power)
* Advanced generative operations leverage state-of-the-art foundation models (Amazon Bedrock: Claude 3.5 Sonnet, Claude 3.5 Haiku, Amazon Nova).
* Cloud AI is strictly on-demand, initiated only when the user explicitly triggers features like Prompt Engineering, Reply Generation, Text Transformation, or Screen AI.

---

## 3. Primary User Experience & Interaction Model

FLOW lives as an unobtrusive native macOS menu bar utility with a floating, non-activating HUD overlay:

```
[User presses Global Hotkey: Fn / Option+Space / Custom]
                         │
                         ▼
        [Floating HUD Appears (Recording)]
                         │
        [User Speaks into any Application]
  (VS Code, Slack, Mail, Notion, Terminal, Chrome, etc.)
                         │
                         ▼
  [Voice Activity Detection (VAD) detects end of speech]
                         │
                         ▼
[Local ASR (WhisperKit on Apple Silicon) generates raw text]
                         │
                         ▼
   [Language Engine cleans punctuation, fillers, & backtracks]
                         │
                         ▼
     [Content Lock Engine verifies entity & constraint fidelity]
                         │
                         ▼
 [Text is Injected Directly at Active Cursor via AXUIElement]
                         │
                         ▼
       [HUD disappears — Ready for next utterance]
```

### Critical Reliability Rules for Interaction
* **No Manual Copy/Paste**: The application automatically places clean text into the active field.
* **No Accidental Submissions**: The injection engine is strictly forbidden from simulating `Return` / `Enter` or triggering form submit actions. Message dispatching remains strictly under the user's manual physical control.
* **Undo Fidelity**: Any inserted text can be undone with a single native `Cmd+Z` keystroke.

---

## 4. Flagship Feature Modules

### Module A: Voice Core & Hardware Acceleration
* Real-time audio streaming via `AVAudioEngine` / `CoreAudio` at 16kHz mono.
* Hardware-accelerated Voice Activity Detection (Silero VAD) with dynamic energy thresholds and configurable silence windows (default: 450ms).
* Local ASR using Apple Neural Engine (ANE) and Metal via `WhisperKit` and `whisper.cpp`.
* Latency budget: Under 400ms end-to-end for a standard 3-second dictation.

### Module B: Language Engine & Formatting
* Rule-based and syntactic post-processing.
* Spoken punctuation mapping (*"period"*, *"comma"*, *"new line"*, *"semicolon"*).
* Spoken formatting (*"bullet point"*, *"all caps"*, *"quote unquote"*).
* Contextual backtracking: Automatically replaces aborted thoughts when words like *"actually"*, *"I mean"*, *"scratch that"* are detected.

### Module C: Multilingual & Code-Switched Speech
* Initial focus: **Tamil $\longleftrightarrow$ English** code-switching.
* Automatic language identification (LID).
* Mode selection: Transcribe in native script, transliterate, or translate directly to English with technical entity preservation.

### Module D: Content Lock Engine
* Pre-extraction of named entities, numbers, code tokens, and negative constraints.
* Post-transformation bi-directional verification.
* Diff inspection modal whenever validation indicates an entity shift or ambiguity.

### Module E: Prompt Engineer & Prompt Diff
* Converts conversational spoken ideas into structured, battle-tested LLM prompts (Objective, Context, Constraints, Expected Output).
* Strict enforcement of the No-Invention Rule.
* Interactive visual Diff viewer showing exactly what was structured without additions.

### Module F: Screen AI
* Global shortcut (`Cmd+Shift+S`) triggers an interactive crosshair region selector via `ScreenCaptureKit`.
* Extracted region undergoes on-device OCR (`VNRecognizeTextRequest`) or multimodal analysis via Bedrock.
* Contextual actions: Generate Reply, Extract Code, Summarize, Explain Error, or Translate UI text.

### Module G: Developer Mode
* Voice-driven casing transformation:
  * *"camel case get user profile"* $\longrightarrow$ `getUserProfile`
  * *"snake case database pool timeout"* $\longrightarrow$ `database_pool_timeout`
  * *"kebab case auth service endpoint"* $\longrightarrow$ `auth-service-endpoint`
  * *"screaming snake max retry count"* $\longrightarrow$ `MAX_RETRY_COUNT`
* Code formatting for Markdown, JSON, YAML, SQL queries, and Git workflows.

### Module H: Personal Intelligence
* Local SQLite database managed via `GRDB.swift`.
* Custom user dictionary: phonetic and text replacements (e.g., *"fast api"* $\longrightarrow$ `FastAPI`).
* Expansion snippets: Trigger phrases expand into structured boilerplate (e.g., *"my standard signature"*).

### Module I: AWS Bedrock Cloud Intelligence
* Scalable serverless bridge: macOS Client $\longrightarrow$ AWS API Gateway $\longrightarrow$ AWS Lambda $\longrightarrow$ Amazon Bedrock.
* Support for Claude 3.5 Sonnet / Haiku and Amazon Nova models.
* End-to-end encrypted transport, least-privilege IAM roles, zero storage of user prompt payloads by cloud providers.

---

## 5. Target Operating Environment & Compatibility

* **Target OS**: macOS 14.0 (Sonoma) and macOS 15.0+ (Sequoia).
* **Target Hardware**:
  * Primary: Apple Silicon (M1, M2, M3, M4 series) utilizing Apple Neural Engine (ANE) and Metal.
  * Secondary / Fallback: Intel x86_64 utilizing AVX2/Metal CPU fallbacks.
* **Development & Build System**: Swift 6, Swift Package Manager (SPM), Xcode 16+.
