# PHASE_2_DEPENDENCY_GRAPH.md — Subsystem Architecture & Dependency Ordering

> **Status**: Active Architecture Baseline  
> **Target Platform**: Windows 10/11 x64  
> **Purpose**: Visual and structural dependency ordering for Phase 2 implementation.

---

## 1. Visual Dependency Graph

```mermaid
flowchart TD
    subgraph S0["Stage 0: Baseline Specification"]
        P2A["Phase 2A: Parity Audit & Spec\n(docs/PHASE_2_*.md)"]
    end

    subgraph S1["Stage 1: Hardware Core (Physical Execution)"]
        P2B["Phase 2B: Core Dictation Parity\n- Real WASAPI IAudioCaptureClient\n- DirectML ONNX Runtime weights\n- CPU Fallback\n- Hands-free double tap\n- 20m recording limit"]
    end

    subgraph S2["Stage 2: Language & Persistence"]
        P2C["Phase 2C: Smart Formatting\n- Backtracking resolver\n- Number/currency formatter\n- Spoken numbered lists\n- Multi-language config"]
        P2D["Phase 2D: Personalization Engine\n- Local SQLite storage\n- Personal dictionary token biasing\n- Voice snippets (60 / 4000 chars)\n- Styles (Formal/Casual/Excited)"]
    end

    subgraph S3["Stage 3: Developer & Command Productivity"]
        P2E["Phase 2E: Developer Mode\n- camelCase / snake_case / PascalCase\n- CLI & path token shielding\n- Voice file tagging (@filename)\n- IDE integration (VS Code/Cursor)"]
        P2F["Phase 2F: Safe Command Mode\n- Secondary hotkey trigger\n- Voice text formatting\n- ZERO-DESTRUCTIVE-ACTION filter"]
    end

    subgraph S4["Stage 4: History & Presentation"]
        P2G["Phase 2G: History & Productivity\n- FTS5 full-text transcript search\n- Paste-last-transcript hotkey\n- WPM, word count & streak metrics\n- Desktop Scratchpad buffer"]
        P2H["Phase 2H: Native Windows Hub\n- WinUI 3 management window\n- Real-time VU audio meter\n- Shortcut rebinding interface\n- Startup registry integration"]
    end

    subgraph S5["Stage 5: Hardening & Physical Verification"]
        P2I["Phase 2I: Privacy & Security\n- DPAPI database encryption\n- UIA password field detection\n- Endpoint disconnect recovery\n- Clean OS shutdown handling"]
        P2J["Phase 2J: Full Parity Validation\n- Real microphone physical test\n- 12+ Windows application matrix\n- Empirical latency & resource SLA\n- Formal exit gate sign-off"]
    end

    P2A --> P2B
    P2B --> P2C
    P2B --> P2D
    P2C --> P2E
    P2D --> P2E
    P2E --> P2F
    P2D --> P2G
    P2F --> P2G
    P2G --> P2H
    P2B --> P2H
    P2H --> P2I
    P2I --> P2J

    classDef completed fill:#1a3a2a,stroke:#2ecc71,stroke-width:2px,color:#ffffff;
    classDef pending fill:#222,stroke:#444,stroke-width:1px,color:#ddd;
    class P2A completed;
    class P2B,P2C,P2D,P2E,P2F,P2G,P2H,P2I,P2J pending;
```

---

## 2. Layered Architecture Dependencies

The architecture maintains strict one-way dependency boundaries:

```
┌────────────────────────────────────────────────────────┐
│             Presentation Layer (WinUI 3)               │
│  - Floating HUD Window (WS_EX_NOACTIVATE | TOPMOST)    │
│  - Hub Management Window (Home, History, Settings)     │
│  - System Tray NotifyIcon & Context Menu               │
└───────────────────────────┬────────────────────────────┘
                            │ Calls into
┌───────────────────────────▼────────────────────────────┐
│               Core Services Layer (Flow.Core)          │
│  - VoiceSessionCoordinator (Session State Machine)     │
│  - ASREngineRegistry & Fallback Chain                  │
│  - DeterministicTextSanitizer & CasingTransformer      │
│  - BacktrackingResolver & ListFormattingEngine         │
│  - DictionaryManager, SnippetEngine & StyleManager     │
│  - HistoryRepository & FtsSearchEngine (SQLite)        │
└─────────────┬────────────────────────────┬─────────────┘
              │ Implements                 │ Uses
┌─────────────▼──────────────┐┌────────────▼─────────────┐
│  Windows Integration Layer ││ Native Inference Engine  │
│  - GlobalHotkeyHook (Win32)││  - DirectML ONNX Runtime │
│  - WasapiAudioCapture      ││  - whisper.cpp CPU fallback
│  - WindowsTextInsertion    ││  - Silero / Energy VAD   │
│  - UI Automation Context   ││  - Local weight store    │
└────────────────────────────┘└──────────────────────────┘
```

---

## 3. Strict Decoupling Rules

1. **Inference Isolation**: `Flow.Core` never directly imports DirectML or native ONNX C-APIs; all speech recognition goes through the `IASREngine` interface.
2. **Audio Capture Isolation**: `Flow.Core` receives standardized 16kHz float32 arrays or spans; it has zero knowledge of WASAPI or Windows Core Audio COM interfaces.
3. **Text Insertion Isolation**: Text injection is invoked via `ITextInsertionService`; the session coordinator is isolated from Win32 `SendInput`, `IUIAutomation`, or clipboard memory allocation.
4. **Safety Verification Invariant**: Both `DeterministicTextSanitizer` in Core and `WindowsTextInsertionService` in Host independently enforce the Zero-Enter and Zero-Unintended-Action filters.
