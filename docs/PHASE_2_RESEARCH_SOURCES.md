# PHASE_2_RESEARCH_SOURCES.md — Wispr Flow Windows Parity Research Log

> **Research Snapshot Date**: September 10, 2026 (Reconciliation Gate Update)  
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
  * System-wide AI voice layer / voice keyboard for desktop.
  * Windows product includes: Flow Bar (HUD), Hub (main dashboard), History, Personal Dictionary, Snippets, Styles, Command Mode, Developer/IDE features, Scratchpad, shortcut customization, and system tray operation.
  * Inserts text at the active cursor across virtually any application.
  * Operates in background from login; starts minimized to system tray without forcing Hub open.
* **Confidence**: High (Official Documentation)

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
  * Team/shared snippets on Pro/Enterprise plans.
* **Confidence**: High (Official Documentation)

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
  * Voice file tagging in Cursor and Windsurf (speaking *"at filename dot ts"* outputs `@filename.ts`).
  * Automatic casing transformation recognition: `camelCase`, `snake_case`, `PascalCase`.
  * Terminals, CLI commands, paths (`C:\...`, relative), JSON, YAML, Markdown.
* **Confidence**: High (Official Documentation)

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
  * Text selected: Executes editing instructions (*"make this shorter"*, *"turn into bullets"*, *"fix grammar"*).
  * No text selected: Accepts voice queries and returns generated answers.
* **Confidence**: High (Official Documentation)

---

### Source 6: Wispr Flow Help Center — Auto Cleanup Levels & Undo AI Edit
* **Capability Area**: Intelligent Post-Processing
* **Source Title**: *Auto Cleanup Control & Modes*
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Four levels of cleanup under the Style tab:
    1. *None*: Raw verbatim speech-to-text transcript.
    2. *Light*: Removes filler words ("um", "uh") and applies basic punctuation/capitalization.
    3. *Medium*: Balances clarity, removes repetitions, and enhances flow.
    4. *High*: Full structuring with bullet points and paragraph breaks.
  * "Undo AI Edit" button: Reverts formatted text to the raw speech transcript.
* **Confidence**: High (Official Release Documentation)

---

### Source 7: Wispr Flow Help Center — Transforms (Beta)
* **Capability Area**: Post-Dictation Voice Editing
* **Source Title**: *Using Transforms with Flow Bar*
* **Article ID**: `7194821034`
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows & macOS
* **Observed Behavior**:
  * Post-processing AI feature accessed via a wand icon on the Flow Bar or a dedicated shortcut.
  * Allows highlighting existing text and applying prompt transformations (e.g., *"turn this outline into a formal memo"*, *"translate to Spanish"*, *"make concise"*).
  * Managed under the "Transforms" tab in the Hub.
* **Confidence**: High (Official Release Documentation)

---

### Source 8: Wispr Flow Documentation — Recording Duration Limits
* **Capability Area**: Session Ceiling & Hands-Free
* **Source Title**: *Continuous Recording Limits on Desktop*
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows
* **Observed Behavior**:
  * Desktop continuous recording limit is **20 minutes** (upgraded from earlier 6-minute cap).
  * Automatic audible/visual alert at the **19-minute mark** ($T - 60\text{s}$) to prompt user wrap-up.
  * Meeting Notetaker supports up to **6 hours** for calendar meetings.
* **Confidence**: High (Verified Official Documentation)

---

### Source 9: Remote Desktop & Virtual Environments (Citrix / RDP / VDI)
* **Capability Area**: Enterprise Windows Compatibility
* **Source Title**: *Remote Desktop & Virtualized Environments*
* **Snapshot Date**: September 10, 2026
* **Platform**: Windows Enterprise
* **Observed Behavior**:
  * Wispr Flow encounters known injection issues in RDP, Citrix, and Horizon sessions because virtual desktop clients block local UIA element interaction.
  * FLOW resolves this via Tier-2 `SendInput(Ctrl+V)` clipboard injection, which works seamlessly across local and remote desktop sessions.
* **Confidence**: High (Developer & User Experience Verification)

---

### Source 10: Model Context Protocol (MCP) in Wispr Flow
* **Capability Area**: External AI Integration
* **Source Title**: *MCP Connections with Wispr Flow Notetaker*
* **Snapshot Date**: September 10, 2026
* **Platform**: Cross-Platform (Cloud Backend)
* **Observed Behavior**:
  * Wispr Flow uses MCP exclusively for its **AI Notetaker** meeting product, allowing Claude/ChatGPT to query cloud meeting transcripts and summaries.
  * It is NOT part of core local Windows desktop dictation.
* **Confidence**: High (Official Integration Documentation)

---

## 2. Contradiction & Ambiguity Log

| ID | Topic | Documentation A | Documentation B | Evidentiary Resolution for FLOW |
| :--- | :--- | :--- | :--- | :--- |
| **C-01** | **Recording Limit** | Older documentation cites 6-minute maximum limit. | Newer 2026 release notes cite 20-minute maximum desktop limit with 19-minute warning. | **RESOLVED**: Wispr increased the desktop cap from 6 to 20 minutes in 2026. FLOW implements the **20-minute ceiling with 19-minute warning** in `VoiceSessionCoordinator.cs`. |
| **C-02** | **Offline Operation** | Wispr marketing highlights low latency. | System requirements mandate active internet connection (cloud server transcription). | **RESOLVED (Deliberate Difference)**: FLOW operates **100% offline** via local whisper.cpp AVX2 CPU + DirectML GPU inference. |
| **C-03** | **Meeting Notetaker** | Mentioned on marketing website. | Docs clarify Notetaker is a separate meeting recorder with cloud calendar bots. | **RESOLVED (Out of Scope)**: Notetaker is cloud/meeting-specific; FLOW focuses strictly on system-wide Windows desktop cursor productivity. |
| **C-04** | **Styles vs Grammar** | Reviews claim Styles "rewrites sentences". | Official docs state Styles adjusts punctuation, capitalization, and contractions—not core grammar. | **RESOLVED**: FLOW's `StyleFormattingEngine.cs` adjusts punctuation and contractions deterministically while strictly preserving Content Lock. |
