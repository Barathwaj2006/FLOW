# ARCHITECTURE.md — System Architecture Specification

> **Status**: Active Architecture Baseline  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  
> **Target Platform**: macOS 14.0+ (Sonoma) / macOS 15.0+ (Sequoia) on Apple Silicon  

---

## 1. System Topology & Architectural Layers

FLOW is designed as a high-performance, modular system partitioned into three strictly segregated layers:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     LAYER 1: macOS Host & UI Layer                       │
│                         (packages/FlowMacOS)                             │
│                                                                          │
│  ┌──────────────────────┐  ┌─────────────────────┐  ┌─────────────────┐  │
│  │   Global Hotkeys     │  │    Floating HUD     │  │  Menu Bar App   │  │
│  │    (CGEventTap)      │  │ (NSPanel / SwiftUI) │  │  (NSStatusItem) │  │
│  └──────────┬───────────┘  └──────────▲──────────┘  └────────┬────────┘  │
│             │                         │                      │           │
│  ┌──────────▼───────────┐  ┌──────────┴──────────┐  ┌────────▼────────┐  │
│  │   Audio Capture      │  │   Text Insertion    │  │ Screen Capture  │  │
│  │ (AVAudioEngine Tap)  │  │ (AXUIElement/Paste) │  │(ScreenCaptureKit│  │
│  └──────────┬───────────┘  └──────────▲──────────┘  └────────┬────────┘  │
└─────────────┼─────────────────────────┼──────────────────────┼───────────┘
              │ 16kHz PCM Audio Stream  │ Validated Text       │ Crop Image
┌─────────────▼─────────────────────────┴──────────────────────▼───────────┐
│                 LAYER 2: Platform-Agnostic Core Engine                   │
│                          (packages/FlowCore)                             │
│                                                                          │
│  ┌───────────────────┐  ┌───────────────────────┐  ┌──────────────────┐  │
│  │  Voice Pipeline   │  │    Language Engine    │  │   Content Lock   │  │
│  │ - Silero VAD      │  │ - Spoken Punctuation  │  │ - Entity Extract │  │
│  │ - WhisperKit ASR  │  │ - Filler Removal      │  │ - Bi-dir Check   │  │
│  │ - Metal / ANE     │  │ - Backtracking Parser │  │ - Diff Engine    │  │
│  └─────────┬─────────┘  └───────────▲───────────┘  └────────┬─────────┘  │
│            │                        │                       │            │
│            └────────────────────────┴───────────────────────┘            │
│                                     │                                    │
│  ┌───────────────────┐  ┌───────────▼───────────┐  ┌──────────────────┐  │
│  │  Developer Mode   │  │  Personal Intelligence│  │  Context Engine  │  │
│  │ - Casing Engine   │  │ - SQLite (GRDB.swift) │  │ - Active App     │  │
│  │ - Code Formatter  │  │ - Custom Dictionary   │  │ - Min Necessary  │  │
│  └───────────────────┘  └───────────────────────┘  └──────────────────┘  │
└─────────────────────────────────────┬────────────────────────────────────┘
                                      │ Explicit User Command (Cloud AI)
┌─────────────────────────────────────▼────────────────────────────────────┐
│                    LAYER 3: AWS Cloud Infrastructure                     │
│                             (infra/aws)                                  │
│                                                                          │
│  ┌───────────────────┐  ┌───────────────────────┐  ┌──────────────────┐  │
│  │  Amazon Bedrock   │  │      AWS Lambda       │  │ Amazon API GW    │  │
│  │ - Claude 3.5 S/H  │  │ - Stateless Handler   │  │ - SigV4 Auth     │  │
│  │ - Amazon Nova     │  │ - Rate Limiting       │  │ - TLS 1.3        │  │
│  └───────────────────┘  └───────────────────────┘  └──────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Voice Engine (Local Audio & ASR Pipeline)

### Audio Ingestion & Buffer Management
* **Capture Interface**: `AVAudioEngine` input node configured with a tap block on a high-priority background audio queue.
* **Audio Format**: Strict 16,000 Hz, 1-channel (mono), 32-bit float or 16-bit linear PCM.
* **Buffering**: Ring buffer (`AudioRingBuffer`) holding up to 30 seconds of rolling audio to eliminate allocation spikes during speech.
* **Noise Suppression**: Apple's native `AUVoiceProcessing` hardware-accelerated DSP unit enabled on the input node for beamforming and acoustic echo cancellation.

### Voice Activity Detection (VAD)
* **Engine**: Silero VAD (v4/v5 ONNX model compiled to CoreML or executed via ONNX Runtime Swift FFI).
* **Chunk Window**: 512 samples (32ms frames).
* **Thresholding**: Speech probability threshold $> 0.65$; trailing silence window of 450ms before triggering speech termination.
* **Modes**:
  * *Push-to-Talk*: Recording active while hotkey is depressed; terminates immediately on key release.
  * *Hands-Free*: Dynamic start on speech detection; auto-commits after silence threshold.

### Automatic Speech Recognition (ASR)
FLOW defines a polymorphic ASR driver protocol:
```swift
public protocol ASREngineProtocol: Sendable {
    func loadModel(identifier: String) async throws
    func transcribe(audio: AudioBuffer, options: TranscribeOptions) async throws -> TranscriptionResult
    func streamTranscribe(audioStream: AsyncStream<AudioChunk>) -> AsyncStream<TranscriptionFragment>
}
```

#### Driver Implementations:
1. **WhisperKit Engine (Primary)**:
   * Optimized for Apple Silicon (M1/M2/M3/M4).
   * Executes Whisper architecture (`base.en`, `small`, `large-v3-turbo`) compiled to CoreML.
   * Dispatches matrix operations directly to the **Apple Neural Engine (ANE)** and **Metal GPU**, minimizing CPU/battery draw.
2. **whisper.cpp Engine (Universal Fallback)**:
   * Portable C/C++ implementation compiled via SPM with Metal acceleration.
   * Serves as reliable fallback for Intel Macs and platforms lacking CoreML weights.
3. **Apple Speech Engine (Instant Fallback)**:
   * Native `SFSpeechRecognizer` with `requiresOnDeviceRecognition = true`. Zero weight download required.

### Latency Budget (Target: $< 400\text{ ms}$)
```
Speech Ends ──(150ms Silence Window)──> VAD Flags EndOfSpeech
            ──(150ms ANE Inference)───> Raw Token String Emitted
            ──(20ms Language Engine)──> Clean Punctuated Text
            ──(15ms Content Lock)─────> Entity/Constraint Verified
            ──(15ms AXUIElement)──────> Characters Appear at Cursor
Total Duration from Vocal Silence to Screen: ~350 ms
```

---

## 3. Language Engine (Local Formatting & Cleanup)

The Language Engine operates in two deterministic stages without cloud dependencies:

### Stage 1: Lexical & State Machine Filter
* **Filler Word Removal**: Configurable dictionary (`"um"`, `"uh"`, `"er"`, `"you know"`, `"like"` when used as hesitation marker).
* **Stutter & Repetition Collapse**: Detects duplicated tokens (*"the the"*, *"in in"*).
* **Spoken Punctuation Parsing**:
  * `"period"` $\longrightarrow$ `.`
  * `"comma"` $\longrightarrow$ `,`
  * `"question mark"` $\longrightarrow$ `?`
  * `"exclamation mark"` $\longrightarrow$ `!`
  * `"new line"` / `"new paragraph"` $\longrightarrow$ `\n` / `\n\n`
  * `"colon"` $\longrightarrow$ `:`
  * `"semicolon"` $\longrightarrow$ `;`
  * `"quote"` / `"unquote"` $\longrightarrow$ `"` / `"`

### Stage 2: Backtracking & Intent Correction
Spoken thought patterns frequently involve mid-sentence corrections:
* **Trigger Patterns**: `/(.+?)\s+(actually|wait no|scratch that|i mean)\s+(.+)/i`
* **Resolution Rule**:
  * Input: *"send the email to Sarah actually John"*
  * Backtrack Detector matches: Antecedent: *"Sarah"*, Trigger: *"actually"*, Replacement: *"John"*.
  * Result: *"Send the email to John."*
  * Input: *"send the report tomorrow actually Friday"*
  * Result: *"Send the report tomorrow—actually, Friday."* (Context-preserving dash when both temporal anchors are meaningful).

---

## 4. Content Lock Engine (Fidelity & The No-Invention Rule)

Content Lock is the primary technological differentiator of FLOW. It guards against accidental corruption or hallucination during AI transformations.

```
                          ┌──────────────────────────┐
                          │     User Spoken Text     │
                          └────────────┬─────────────┘
                                       │
                        ┌──────────────▼──────────────┐
                        │ Protected Entity Extraction │
                        │  - Technical Term Lexicon   │
                        │  - Regex Entity Matchers    │
                        │  - Negative Constraint Extr │
                        └──────────────┬──────────────┘
                                       │
                                       ├─────────────────────────────┐
                                       │ [Entity & Constraint Graph] │
                                       │                             │
                        ┌──────────────▼──────────────┐              │
                        │  AI Transformation Action   │              │
                        │  (Local NLP or Bedrock)     │              │
                        └──────────────┬──────────────┘              │
                                       │                             │
                        ┌──────────────▼──────────────┐              │
                        │   Content Lock Validator    │              │
                        │  - Bi-directional Match     │◄─────────────┘
                        │  - Negative Rule Inversion  │
                        │  - No-Invention Audit       │
                        └──────────────┬──────────────┘
                                       │
                   ┌───────────────────┴───────────────────┐
                   │                                       │
             [Passed: 100%]                      [Failed / Discrepancy]
                   │                                       │
                   ▼                                       ▼
        [Direct Text Insertion]                 [Present Prompt Diff UI]
                                                [Apply or Cancel Manual]
```

### Entity Extraction Schema
Protected categories:
1. **Identifiers & Code**: CamelCase, snake_case, file extensions (`.ts`, `.py`, `.swift`), URLs, IP addresses, CLI flags (`--verbose`).
2. **Quantities & Dates**: Integers, floating-point numbers, percentages, monetary amounts, days of the week, ISO dates.
3. **Negative Directives**: Patterns matching `/(do not|don't|never|without|exclude|avoid)\s+([A-Za-z0-9_\-\.]+)/i`.

### Validation Contract
* If user input specifies *"Do not use Firebase"*:
  * Output containing *"Firebase"* without an explicit negative qualifier fails immediately.
* If user input specifies *"Build a FastAPI backend"*:
  * Output suggesting *"Express.js"* or dropping *"FastAPI"* fails immediately.
* The **No-Invention Rule**: If the input does not specify a database, the transformed prompt must NOT arbitrarily add *"PostgreSQL"*. It may suggest a section: `Database: [Unspecified - please select]`.

---

## 5. Multilingual & Translation Engine

### Language Identification (LID)
* Real-time audio LID evaluated during the first 500ms of speech using Whisper's multilingual head.
* Detected language code emitted to the Floating HUD (e.g., `ta` for Tamil).

### Tamil & Code-Switching Architecture
* Special handling for Tamil $\longleftrightarrow$ English bilingual speech:
  * Phonetic transcription retaining English technical loanwords (e.g., *“Database connect பண்ணு”* $\longrightarrow$ *"Connect to the database"*).
  * Direct translation option: When the system detects Tamil, a non-intrusive HUD toggle presents: `Tamil detected. Translate to English? [Translate | Keep Original]`.
  * Entity preservation ensures Tamil transliterated names, currency (₹ / Rupees), and technical terms are locked during translation.

---

## 6. Prompt Engineer & Prompt Diff

### Transformation Pipeline
Unstructured spoken thoughts are converted into structured engineering prompts following a rigorous schema:
```markdown
# Objective
[Core user goal distilled into a single imperative sentence]

# Context
[Background information provided in the utterance]

# Requirements
[Strict bulleted list of functional requirements stated by the user]

# Constraints
[Bulleted list of negative rules, dependencies, and boundaries]

# Expected Output
[Target format, schema, or language]
```

### Prompt Diff Engine
Before applying a generated prompt, the client computes a character-level and semantic token diff:
* Tokens added $\longrightarrow$ Highlighted green (audited against the No-Invention Rule).
* Tokens removed $\longrightarrow$ Highlighted red (audited to verify no constraints were lost).
* Visual modal displayed with `Apply (Enter)` or `Cancel (Esc)`.

---

## 7. Screen AI Engine

### Capture & Vision Workflow
1. **Invocation**: Dedicated shortcut (`Cmd+Shift+S`).
2. **Selection Overlay**: An unmanaged `NSWindow` covers the active screen with a semi-transparent dark scrim and crosshair cursor.
3. **Crop Capture**: The user drags a rectangular region. Coordinates are passed to macOS `ScreenCaptureKit` to produce a high-resolution CoreGraphics bitmap (`CGImage`).
4. **Local Processing**:
   * Text extraction runs immediately on-device using Apple Vision `VNRecognizeTextRequest` ($< 50\text{ ms}$).
   * If the user requests complex reasoning (*"Explain this error"*, *"Write a reply to this message"*), the compressed image and extracted OCR text are bundled into a payload for Amazon Bedrock.

### Screenshot $\longrightarrow$ Reply Pipeline
* Prevents context leakage by transmitting **only** the selected bounding box, never the full desktop.
* Generated reply is staged in the Floating HUD with a preview.
* Injected into the active application's reply field on confirmation.
* **Inviolable Guarantee**: The engine NEVER clicks "Send" or simulates Enter.

---

## 8. Developer Mode

Developer Mode optimizes the pipeline for programming workflows:
* **Spoken Casing Modes**:
  * `"camel case"` $\longrightarrow$ Lower camelCase (`createUserService`)
  * `"pascal case"` $\longrightarrow$ Upper CamelCase (`OrderRepository`)
  * `"snake case"` $\longrightarrow$ Lower snake_case (`connection_pool_size`)
  * `"screaming snake"` $\longrightarrow$ UPPER_SNAKE_CASE (`MAX_BUFFER_CAPACITY`)
  * `"kebab case"` $\longrightarrow$ kebab-case (`api-gateway-route`)
* **Terminal & CLI Awareness**: Automatically escapes shell arguments, quotes strings containing spaces, and formats commands (`git commit -m "..."`).
* **Active App Detection**: Automatically enables Developer Mode when the active frontmost application is identified as an IDE or Terminal (`com.microsoft.VSCode`, `com.googlecode.iterm2`, `dev.zed.Zed`).

---

## 9. macOS Integration & Text Insertion Engine

### Native Integration Points
* **Global Hotkeys**: Implemented via a low-level event tap (`CGEvent.tapCreate`) for global accessibility and sub-millisecond response, with fallback to `NSEvent.addGlobalMonitorForEventsMatchingMask`.
* **Permissions Health Guard**: Dedicated `PermissionManager` tracking Accessibility, Microphone, and Screen Recording permissions. Displays deep links to System Settings on missing rights.
* **Floating HUD**: An `NSPanel` styled with SwiftUI, configured with:
  * `.nonactivatingPanel` (prevents stealing keyboard focus from the active document/text field).
  * Level: `.floating` or `.popUpMenu` (visible above fullscreen applications).

### Universal Text Insertion Strategy
Reliable text insertion without clipboard clobbering:
```
┌──────────────────────────────────────────────────────────┐
│             Request Text Insertion at Cursor             │
└────────────────────────────┬─────────────────────────────┘
                             │
            ┌────────────────▼────────────────┐
            │ Check Accessibility Element     │
            │ (AXUIElementCopyAttributeValue) │
            └────────────────┬────────────────┘
                             │
                 Can set AXValue / AXSelectedText?
                 ┌───────────┴───────────┐
                 │ YES                   │ NO (Electron / Non-AX Field)
                 ▼                       ▼
    ┌────────────────────────┐  ┌────────────────────────────────────┐
    │  AXUIElementSetValue   │  │  Clipboard Inject Fallback         │
    │  (Instant, No Clipbd)  │  │  1. Backup current NSPasteboard    │
    └────────────────────────┘  │  2. Write text to NSPasteboard     │
                                │  3. CGEventPost(Cmd + V)           │
                                │  4. Sleep 100ms                    │
                                │  5. Restore original NSPasteboard  │
                                └────────────────────────────────────┘
```

* **Safety Isolation**: Keycode `0x24` (`kVK_Return`) is strictly filtered out from all programmatic event generation.

---

## 10. Local Storage & Personal Intelligence

* **Database**: Embedded SQLite 3 managed through `GRDB.swift` (zero external daemon, thread-safe, compile-time typed SQL).
* **Location**: `~/Library/Application Support/com.flow.mac/flow.sqlite`.
* **Data Schemas**:
  * `UserDictionary`: Custom phonetic mappings and technical jargon.
  * `Snippets`: Keyword trigger $\longrightarrow$ expanded text template.
  * `DictationHistory`: Timestamp, raw transcript, clean text, word count, latency metrics (audio buffers are NEVER saved).
  * `Preferences`: Hotkeys, silence timeout, cloud AI toggle, Developer Mode triggers.

---

## 11. AWS Bedrock Cloud Architecture

When explicit cloud operations are invoked, the request follows a secure, minimal serverless flow:

```
[FLOW macOS Client]
        │
        │ HTTPS / TLS 1.3 with AWS SigV4
        ▼
[Amazon API Gateway (REST API)]
        │
        │ Authorizer / Rate Limiter
        ▼
[AWS Lambda Function (Node.js/Python/Rust)]
        │
        │ Minimal Prompt Payload (No Audio)
        ▼
[Amazon Bedrock Runtime]
  - Anthropic Claude 3.5 Sonnet / Haiku
  - Amazon Nova Pro / Lite
        │
        ▼
[Response Streamed Back to Client]
```

* **Zero Audio to Cloud**: The payload contains only the cleaned textual prompt or the selected screenshot crop.
* **No Provider Training**: Bedrock enterprise terms guarantee customer inputs are never used to train base foundation models.
