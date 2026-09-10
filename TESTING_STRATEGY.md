# TESTING_STRATEGY.md — Quality Assurance & Benchmarking Framework

> **Status**: Inviolable Testing Standard  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  
> **Mandate**: No phase may be certified complete without passing automated unit, integration, and latency benchmarks.  

---

## 1. The Voice AI Testing Pyramid

Testing a native, system-wide AI voice layer requires testing across multiple layers, from pure deterministic logic to real-time audio threads and macOS accessibility APIs:

```
                  ┌────────────────────────┐
                  │    E2E & macOS UI      │  Manual & Screen Recording
                  │   Integration Tests    │  (Active App Text Insertion)
                  ├────────────────────────┤
                  │  Latency & ASR Bench   │  Automated Audio Harness
                  │ (WER, SLA < 400ms)     │  (Standard WAV Test Vectors)
                  ├────────────────────────┤
                  │ Content Lock & Fidelity│  Negative Constraint &
                  │      Test Suites       │  Entity Preservation Vectors
                  ├────────────────────────┤
                  │ Component Unit Tests   │  Punctuation, Backtrack,
                  │ (Language, Casing, DB) │  Casing, SQLite, VAD chunks
                  └────────────────────────┘
```

---

## 2. Unit Testing Strategy by Component

### A. Language Engine Test Suite
* **Punctuation & Formatting Matrix**:
  * Test spoken punctuation (`"hello world period"` $\longrightarrow$ `"Hello world."`).
  * Test newline formatting (`"first line new line second line"` $\longrightarrow$ `"First line\nSecond line"`).
  * Test quote wrapping (`"quote hello world unquote"` $\longrightarrow$ `"\"Hello world\""`).
* **Filler Word & Stutter Removal**:
  * Test hesitation words (`"um so we need to like deploy"` $\longrightarrow$ `"So we need to deploy"`).
  * Test lexical stutter (`"we we should go"` $\longrightarrow$ `"We should go"`).
* **Backtracking Intent Resolution**:
  * Test replacement words:
    * `"Send John the report tomorrow actually Friday"` $\longrightarrow$ `"Send John the report tomorrow—actually, Friday."`
    * `"Schedule the meeting for 2 PM wait no 3 PM"` $\longrightarrow$ `"Schedule the meeting for 3 PM."`
    * `"Create a file called index dot js scratch that index dot ts"` $\longrightarrow$ `"Create a file called index.ts."`

### B. Content Lock Test Suite (The Fidelity Benchmark)
* **Entity Extraction Precision & Recall**:
  * Test extraction of proper nouns, dates, numbers, currency, URLs, file paths, and code tokens.
* **Negative Constraint Preservation**:
  * Test phrases containing negative directives:
    * `"Build an API in Go. Do not use Gin or Gorm."`
    * Validator MUST fail if transformed text introduces `Gin` or `Gorm`.
* **The No-Invention Rule Verification**:
  * Test prompt expansion where unmentioned tech stacks are rejected:
    * Input: `"Build a personal portfolio site for a photographer."`
    * Transformed text containing `"Use Next.js 14 and Tailwind CSS"` MUST fail validation unless marked as an unconfirmed suggestion.
* **Diff Engine Accuracy**:
  * Test character-level and token-level diff generation between original spoken text and structured output.

### C. Developer Mode Test Suite
* **Casing Transform Verification**:
  * `"camel case get user account balance"` $\longrightarrow$ `getUserAccountBalance`
  * `"snake case database max connections"` $\longrightarrow$ `database_max_connections`
  * `"pascal case auth service delegate"` $\longrightarrow$ `AuthServiceDelegate`
  * `"screaming snake default request timeout"` $\longrightarrow$ `DEFAULT_REQUEST_TIMEOUT`
  * `"kebab case billing service worker"` $\longrightarrow$ `billing-service-worker`
* **Shell & Git Command Awareness**:
  * Verify automated quotation of arguments with spaces and proper flag capitalization (`git commit -m "initial commit"`).

### D. Local Storage & Database Test Suite
* Verify SQLite migrations execute idempotently via `GRDB.swift`.
* Verify custom dictionary replacement occurs in $< 5\text{ ms}$.
* Verify snippet expansion resolves correctly with dynamic placeholders (e.g., current date).
* Verify database encryption and thread-safe concurrent reads/writes.

---

## 3. Audio & Latency Benchmarking Framework

### The Latency Benchmark Harness
Located in `tools/scripts/benchmark_latency.swift`. This harness measures the exact latency breakdown on synthetic and recorded audio fixtures:

| Milestone | Latency Target (Apple Silicon M-Series) | Hard Ceiling (Failing Gate) |
| :--- | :--- | :--- |
| **VAD Silence Detection** | $150\text{ ms}$ | $250\text{ ms}$ |
| **WhisperKit ASR Inference (3s Audio)** | $120 - 180\text{ ms}$ | $300\text{ ms}$ |
| **Language Engine Post-Processing** | $5 - 15\text{ ms}$ | $30\text{ ms}$ |
| **Content Lock Verification** | $10 - 20\text{ ms}$ | $40\text{ ms}$ |
| **macOS Text Insertion (AXUIElement)** | $10 - 20\text{ ms}$ | $50\text{ ms}$ |
| **End-to-End Local Dictation SLA** | **$< 350\text{ ms}$** | **$450\text{ ms}$** |

### Synthetic Test Fixtures
Stored in `tools/test_fixtures/`:
* `silence_1000ms.wav`: 1 second of calibrated digital silence (tests VAD non-triggering).
* `speech_english_short.wav`: 3-second English sentence (baseline latency fixture).
* `speech_backtrack.wav`: Spoken sentence with self-correction (*"actually Friday"*).
* `speech_tamil_mixed.wav`: Tamil + English code-switched utterance (*"Server down ஆச்சு, உடனே fix பண்ணு"*).
* `speech_developer_casing.wav`: Spoken casing instruction (*"camel case fetch customer records"*).

---

## 4. macOS Integration & Accessibility Testing

Because UI and Accessibility APIs interact with external processes, integration testing employs a dual strategy:

### 1. Mock Integration Testing (CI / Headless)
* `MockTextInsertionService`: Implements `TextInsertionServiceProtocol`. Captures injected strings, detects simulated keycodes, and asserts that keycode `0x24` (`Return`) is never emitted.
* `MockAudioSource`: Implements `AudioSourceProtocol`. Pumps PCM buffers from WAV files into the audio queue.

### 2. Live macOS Testing (Local Machine & Staging)
* Test harness launches a lightweight test target application (`TestFieldApp.app`).
* Verifies direct insertion into:
  * Standard AppKit `NSTextView`
  * SwiftUI `TextEditor`
  * Chromium / Electron web text field
  * Terminal / CLI window (`iTerm2` / `Terminal.app`)
* Verifies that clipboard contents prior to insertion are restored intact within 150ms.

---

## 5. Multilingual & Tamil Evaluation

* **Word Error Rate (WER)** benchmarked on a curated set of 100 Tamil and code-switched Tamil-English audio recordings.
* **Translation Fidelity**: Verifies that when Tamil audio is translated into English:
  * Numbers and dates match the spoken Tamil values.
  * Technical English words embedded in Tamil speech remain unchanged.

---

## 6. Continuous Integration (CI) Specifications

Continuous integration runs on GitHub Actions using `macos-14` (Apple Silicon M1 runner) and `macos-15` (M2 runner):

```yaml
name: FLOW macOS CI
on: [push, pull_request]

jobs:
  build-and-test:
    runs-on: macos-14
    steps:
      - uses: actions/checkout@v4
      - name: Select Xcode
        run: sudo xcode-select -s /Applications/Xcode_15.4.app
      - name: Run FlowCore Unit Tests
        run: swift test --package-path packages/FlowCore
      - name: Run Latency Benchmark Harness
        run: swift run --package-path packages/FlowCore FlowCoreBenchmark
```
