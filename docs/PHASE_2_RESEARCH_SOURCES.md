# PHASE_2_RESEARCH_SOURCES.md — Wispr Flow Windows Parity Research Log

> **Research Snapshot Date**: September 10, 2026  
> **Target Platform**: Windows 10 (1903+) / Windows 11 x64  
> **Purpose**: Authoritative evidentiary baseline of current Wispr Flow Windows capabilities, verified behaviors, and documented limitations.

---

## 1. Primary Research Sources

### Source 1: Wispr Flow Help Center — Product Overview
* **Capability Area**: Product Definition, Desktop Architecture, Core Features
* **Source Title**: *What is Flow?*
* **Article ID**: `2772472373`
* **URL**: `https://docs.wisprflow.ai/articles/2772472373-what-is-flow`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Wispr Flow functions as a system-wide AI voice layer / voice keyboard for desktop.
  * Windows product includes: Flow Bar (HUD), Hub (main dashboard), History, Personal Dictionary, Snippets, Styles, Command Mode, Developer/IDE features, Scratchpad, shortcut customization, and system tray operation.
  * Inserts text at the active cursor across virtually any application.
  * Operates in background from login; starts minimized to system tray without forcing Hub open.
* **Confidence**: High (Official Documentation)
* **Notes**: Mentions cloud processing dependency for standard Wispr Flow transcription; contrast with FLOW's 100% offline local-first mandate.

---

### Source 2: Wispr Flow Help Center — Snippets Subsystem
* **Capability Area**: Text Expansion & Voice Macros
* **Source Title**: *Create and use snippets*
* **Article ID**: `5784437944`
* **URL**: `https://docs.wisprflow.ai/articles/5784437944-create-and-use-snippets`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Voice trigger / "cue" phrase replaces spoken trigger with pre-configured expansion block.
  * Desktop limits: Maximum 60 characters for trigger cue; maximum 4,000 characters for snippet expansion body.
  * Supports rich text formatting (bold, italics, links, lists) preserved upon injection.
  * Team/shared snippets on Pro/Enterprise plans.
* **Confidence**: High (Official Documentation)
* **Notes**: FLOW local implementation will store snippets in local SQLite (`Flow.Core/Storage`) and match triggers deterministically during language sanitization before insertion.

---

### Source 3: Wispr Flow Help Center — Styles Subsystem
* **Capability Area**: Adaptive Tone & Formatting
* **Source Title**: *How to setup Flow Styles*
* **Article ID**: `2368263928`
* **URL**: `https://docs.wisprflow.ai/articles/2368263928-how-to-setup-flow-styles`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Categories: *Personal*, *Work*, *Email*, *Other*.
  * Specific tone styles: *Formal*, *Casual*, *Very Casual*, *Excited*.
  * Adjusts formatting, punctuation density, and capitalization rules according to the active target application (e.g., Slack gets Casual, Outlook gets Formal).
  * Documentation explicitly clarifies that Styles adjusts *formatting and punctuation*, NOT underlying grammar or user word choice.
* **Confidence**: High (Official Documentation)
* **Notes**: Maps directly to FLOW's `FormattingOptions` and process-aware rule matching in `DeterministicTextSanitizer`.

---

### Source 4: Wispr Flow Help Center — Developer & IDE Features
* **Capability Area**: Developer Mode, Code Syntax, IDE Integration
* **Source Title**: *Use Flow with Cursor, VS Code, and other IDEs*
* **Article ID**: `6434410694`
* **URL**: `https://docs.wisprflow.ai/articles/6434410694-use-flow-with-cursor-vs-code-and-other-ides`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Active editor context recognition for variable, function, and class names.
  * Voice file tagging in Cursor and Windsurf (e.g., speaking *"at filename dot ts"* outputs `@filename.ts`).
  * Automatic casing transformation recognition: `camelCase`, `snake_case`, `PascalCase`.
  * Support for terminals, CLI commands, paths (`C:\...`, relative), JSON, YAML, Markdown.
  * Interaction quirk: In VS Code, certain accessibility injections trigger VS Code's "Screen Reader Optimized" mode alert.
* **Confidence**: High (Official Documentation & Developer Confirmation)
* **Notes**: FLOW avoids the VS Code screen reader trigger by relying on safe `SendInput(Ctrl+V)` with clipboard restore when UIA direct injection is disabled or in code editor surfaces.

---

### Source 5: Wispr Flow Help Center — Command Mode
* **Capability Area**: Voice Editing & Search
* **Source Title**: *How to use Command Mode*
* **Article ID**: `4816967992`
* **URL**: `https://docs.wisprflow.ai/articles/4816967992-how-to-use-command-mode`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Activated via separate dedicated shortcut / hold modifier.
  * When text is selected: Executes editing instructions (*"make this shorter"*, *"turn into bullets"*, *"fix grammar"*).
  * When no text is selected: Accepts search / informational queries and returns generated answer.
* **Confidence**: High (Official Documentation)
* **Critical Distinction & Safety Rule**:
  * Wispr executes LLM-driven edits and online search.
  * In FLOW, Command Mode must strictly adhere to the **ZERO-ENTER & ZERO-UNINTENTIONAL-ACTION** safety invariant: Under no circumstance will Command Mode dispatch Enter, submit forms, send messages, execute shell commands, or delete content without explicit user review.

---

### Source 6: Wispr Flow Official Site — Core Dictation Features
* **Capability Area**: Voice Capture, VAD, Cleanup
* **Source Title**: *Wispr Flow Features*
* **URL**: `https://wisprflow.ai/features`
* **Snapshot Date**: September 10, 2026
* **Platform**: Cross-platform (Windows / macOS / Mobile)
* **Observed Behavior**:
  * Core dictation: Push-to-talk (hold key) and Hands-free mode (double-press key).
  * Filler word removal (*"um"*, *"uh"*, stutters).
  * Spoken punctuation (*"period"*, *"comma"*, *"question mark"*).
  * Automatic punctuation and capitalization.
  * Real-time backtracking correction (e.g., *"Friday actually next Monday"* replaces prior token).
  * Numbered lists and bullet formatting by voice.
* **Confidence**: High (Official Website)

---

### Source 7: Wispr Flow Documentation — Recording Limits & Hands-Free
* **Capability Area**: Session Lifecycle & Limits
* **Source Title**: *Recording Duration & Hands-Free Behavior*
* **URL**: `https://docs.wisprflow.ai/articles/2772472373-what-is-flow` & Help Center updates
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows
* **Observed Behavior**:
  * Hands-free toggle activated via double-tap of activation hotkey.
  * Continuous recording limit: 20 minutes on desktop (increased from earlier 6-minute ceiling).
  * Warning alert provided when approaching limit (e.g., at 19-minute mark).
* **Confidence**: High (Verified documentation update)

---

### Source 8: Wispr Flow Documentation — Languages & Multilingual
* **Capability Area**: Multi-Language Support
* **Source Title**: *Languages & Auto-Detection*
* **URL**: `https://docs.wisprflow.ai/articles/languages`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Supports 100+ languages.
  * Features an **Auto-Detect Mode** to recognize spoken language automatically.
  * Official recommendation encourages manual language selection in Settings for maximum accuracy.
  * Code-switching / bilingual speech supported (e.g., Hinglish, Spanglish).
* **Confidence**: High (Official Documentation)
* **Contradiction / Architecture Note**:
  * Auto-detection occasionally suffers from initial phrase latency or misclassification on short utterances (< 2 seconds).
  * FLOW architecture separates **transcription language selection/detection** from downstream **translation** (Phase 4).

---

## 2. Contradiction & Ambiguity Log

| ID | Topic | Documentation A | Documentation B | Interpretation / Resolution for FLOW |
| :--- | :--- | :--- | :--- | :--- |
| **C-01** | **Recording Limit** | Older documentation cites 6-minute maximum limit. | Newer (2026) release notes cite 20-minute maximum desktop limit with 19-minute warning. | **FLOW Standard**: Default 20-minute ceiling with configurable threshold in settings (5, 10, 20 mins) and audible/visual notification at $T - 60\text{s}$. |
| **C-02** | **Offline Operation** | Wispr marketing highlights low latency. | System requirements state active broadband internet required (cloud inference). | **FLOW Divergence**: Wispr is cloud-dependent; FLOW is strictly **100% offline local-first** for core dictation. |
| **C-03** | **Meeting Notetaker** | Mentioned in feature marketing. | Documentation clarifies Notetaker is Mac-only; Windows support listed as roadmap. | **FLOW Status**: Notetaker is Mac-specific / out-of-scope for Phase 2 Windows baseline; focus is cursor-level productivity. |
| **C-04** | **Styles vs Grammar** | Third-party reviews claim Styles "rewrites your sentences to sound formal". | Official Wispr docs clarify Styles only adjusts capitalization, punctuation, and emoji/spacing density—not vocabulary. | **FLOW Resolution**: Follow official documentation. Styles adjust deterministic formatting rules, strictly adhering to Content Lock without hallucinating text. |
