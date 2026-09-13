# FLOW — Product Specification

> **Platform**: Windows 10/11 x64 Native Desktop  
> **Product Positioning**: System-wide voice productivity platform for Windows  
> **Core Architecture**: Local-First, Offline Sovereignty, Inviolable Zero-Enter Invariant  
> **Version**: 1.0.0 Production  

---

## 1. Product Overview & Vision

FLOW is a high-performance, private, system-wide voice productivity application built natively for Windows 10 and 11. Positioned as a serious, local-first alternative to cloud-dependent voice dictation platforms, FLOW delivers real-time voice transcription into any Windows text field while ensuring that 100% of user voice audio and transcription text remains on the local device.

### Core Value Pillars
- **Zero Cloud Audio**: Microphone audio is captured via native WASAPI and processed locally with Whisper.net (GGML tiny.en and multilingual models) with AVX2 and DirectML hardware acceleration. No audio packets ever leave the device.
- **Inviolable Zero-Enter Invariant**: FLOW never simulates `VK_RETURN` (0x0D), newline execution triggers, or form submissions. The user retains complete control over when to send, submit, or execute.
- **Content Lock & Zero Invention**: Spoken words are faithfully transcribed. Smart formatting cleans up fillers ("um", "uh"), normalizes whitespace, formats spoken punctuation, and expands user snippets, but never invents unmentioned terms or alters intent.
- **Consumer Product Model**: A clean, distraction-free desktop experience modeled around user productivity:
  - **FLOW Hub**: The central workspace for productivity metrics, history search, custom vocabulary, voice snippets, writing styles, quick note scratchpad, and system settings.
  - **Flow Bar**: An unobtrusive, non-activating floating desktop pill indicating live recording and processing states with real-time waveform visualization.

---

## 2. Information Architecture & Navigation

The primary FLOW Hub window is structured around 8 consumer sections with zero engineering clutter or developer dashboard surfaces:

```
FLOW
────────────────────────────
WORKSPACE
⌂  Home            - Status, mic endpoint, shortcut, test area, live KPIs, recent feed
◷  History         - Full-text search (FTS5), date/app filters, favorites, export
◇  Dictionary      - "Teach FLOW your words", custom vocabulary, phonetic corrections
▣  Snippets        - Spoken trigger phrase expansions for templates and boilerplate
✦  Styles          - Writing styles: Natural, Formal, Casual, Concise
▤  Scratchpad      - Lightweight distraction-free note capture with real-time autosave
────────────────────────────
PREFERENCES
⚙  Settings        - Consolidated audio, shortcut, language, and privacy preferences
?  About           - Application version, local model status, invariant guarantees, exit
```

---

## 3. Detailed Subsystem Specifications

### 3.1 Home Screen
- **Status Badge**: Clear state indicator (`● FLOW Active & Ready`, `● Recording Audio...`, `● Transcribing Locally...`).
- **Endpoint Cards**: Shows active WASAPI recording endpoint and configured global push-to-talk shortcut (`Right Alt`).
- **Quick Dictation Test Box**: In-window text area allowing immediate validation of speech-to-text without leaving the app.
- **Deterministic Productivity KPIs**:
  - *Words Today*: Aggregate words dictated during the current calendar day.
  - *Average WPM*: True average speech-to-text words per minute across sessions.
  - *Sessions Today*: Count of successful dictation sessions completed today.
  - *Daily Streak*: Consecutive active days with dictation.
  *(Zero fabricated metrics. If no history exists, empty state prompts user to begin dictating).*
- **Recent Dictation Feed**: Chronological list of the most recent dictations with application badge, word count, timestamp, and one-click clipboard copy.

### 3.2 History & Productivity
- **Search**: Real-time SQLite FTS5 full-text search across all recorded transcripts.
- **Filters**: By target application, language, and starred favorites.
- **Transcript Cards**: Shows exact timestamp, target application, duration, word count, and full text.
- **Actions**: One-click copy to clipboard, star/favorite toggle, soft-delete with permanent purge options.
- **Export**: Full export to JSON, CSV, and Plain Text formats.

### 3.3 Personal Dictionary
- **Purpose**: Teach FLOW specialized domain vocabulary, acronyms, proper nouns, and misheard word corrections.
- **Features**:
  - Add Word/Phrase dialog.
  - Phonetic Correction mapping (e.g., misheard `cube netties` $\rightarrow$ `Kubernetes`).
  - Star favorite terms.
  - Sub-millisecond in-memory regex compilation for real-time dictation pipeline biasing.

### 3.4 Voice Snippets
- **Purpose**: Expand short spoken trigger phrases into rich multiline text templates, email responses, or links.
- **Features**:
  - Spoken trigger phrase matching (e.g., "my calendly" $\rightarrow$ `https://calendly.com/user/30min`).
  - Character counter and multiline template editor.
  - Instant pipeline expansion.

### 3.5 Writing Styles
- **Purpose**: Format transcribed speech to match the formality and tone of different contexts.
- **Style Profiles**:
  - **Natural / Balanced** (Default): Standard everyday dictation with natural punctuation and capitalization.
  - **Formal / Professional**: Expands contractions ("do not" instead of "don't") with polished business flow.
  - **Casual / Conversational**: Preserves natural spoken contractions and relaxed phrasing.
  - **Concise / Bulleted**: Short, punchy sentences formatted for rapid notes and chat.

### 3.6 Scratchpad
- **Purpose**: Quick note capture workspace for dictating thoughts, meeting minutes, and draft documents.
- **Features**:
  - Instant note creation with auto-derived titles.
  - Real-time debounced autosave with visual status ("Saved ✓" / "Saving...").
  - Note pinning (`📌`), markdown export, and clipboard copying.

### 3.7 Settings & Audio
- **Microphone**: WASAPI physical device enumeration, default endpoint detection, live RMS level meter, and VAD sensitivity tuning.
- **Dictation**: Configurable push-to-talk hotkey, hands-free double-tap mode, and Backtrack insertion undo (`Shift + Right Alt`).
- **Languages**: Supported model language picker (English, Tamil, Hindi, Spanish, French, German, Auto-Detect).
- **Privacy**: History retention policy (Unlimited, 90 days, 30 days, 7 days) and one-click local database purge.

### 3.8 Flow Bar (Floating HUD)
- **Win32 Specification**: `WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`.
- **Focus Safety**: Never steals focus from active applications (`HTTRANSPARENT`, `MA_NOACTIVATE`).
- **Visual Display**: Dark pill surface, animated state dot (Green/Red/Blue), real-time audio RMS waveform bars, Segoe UI status text.
