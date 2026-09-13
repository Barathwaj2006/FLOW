# FLOW — UI/UX Design System Specification

> **Status**: Production Reference Specification (Google Stitch Parity)  
> **Target Platform**: Windows 10/11 x64 Native Desktop  
> **Visual Direction**: Windows 11 Fluent Dark / Light System Utility (Wispr Flow Parity)

---

## 1. Design Mission & Philosophy

FLOW is a high-performance Windows voice productivity application. It allows users to hold a keyboard shortcut, speak naturally, and have their speech transcribed, formatted, and inserted into any Windows application without stealing focus or simulating accidental submission keys.

### Core Visual Principles
- **Calm & Unobtrusive**: The application disappears into the user's workflow. It is not an AI marketing landing page or enterprise dashboard.
- **Zero "White-Coded" Leaks**: All controls, dropdown popups, scrollbars, focus rectangles, sliders, and borders are completely styled with custom WPF templates. Default unstyled white Windows controls are strictly prohibited.
- **Fluent Windows 11 Integration**: Uses Mica/Acrylic styling cues, restrained geometry, 4px grid spacing, subtle hairline borders, and Segoe UI typography.
- **Faithful Feedback**: State transitions (Ready, Recording, Processing, Success, Error) use clear color tokens and vector geometry rather than decorative emoji.

---

## 2. Color System & Design Tokens

### A. Dark Theme (Primary / Default)
| Token | Hex Value | Usage |
|---|---|---|
| `BrushBackground` | `#0B0F17` | Window main background (Deep Obsidian) |
| `BrushSurfaceCard` | `#131B2E` | Primary cards, content panels |
| `BrushSurfaceElevated` | `#0E1422` | Header, sidebar, inputs, combobox dropdowns |
| `BrushSurfaceKpi` | `#101726` | KPI metric cards |
| `BrushBorderSubtle` | `#1E2B45` | Hairline card & panel borders |
| `BrushBorderFocus` | `#38BDF8` | Active input & focus indicator |
| `BrushTextPrimary` | `#F8FAFC` | Headings, primary labels, transcript text |
| `BrushTextSecondary` | `#94A3B8` | Subtitles, metadata, timestamps |
| `BrushTextMuted` | `#64748B` | Placeholder text, page counters |
| `BrushAccentPrimary` | `#2563EB` | Primary CTA buttons |
| `BrushAccentHover` | `#3B82F6` | Primary CTA hover state |
| `BrushSuccessBg` | `#064E3B` | Ready badge background |
| `BrushSuccessBorder` | `#059669` | Ready badge border |
| `BrushSuccessText` | `#A7F3D0` | Ready status label |
| `BrushSuccessDot` | `#10B981` | Live active indicator dot |
| `BrushDangerBg` | `#7F1D1D` | Destructive buttons |
| `BrushDangerText` | `#FCA5A5` | Destructive text |

### B. Light Theme (Fluent Light Mode)
| Token | Hex Value | Usage |
|---|---|---|
| `BrushBackground` | `#F8FAFC` | Window background (Soft Warm Slate) |
| `BrushSurfaceCard` | `#FFFFFF` | Primary cards, content panels |
| `BrushSurfaceElevated` | `#F1F5F9` | Header, sidebar, inputs |
| `BrushSurfaceKpi` | `#F8FAFC` | KPI metric cards |
| `BrushBorderSubtle` | `#E2E8F0` | Subtle hairline borders |
| `BrushBorderFocus` | `#0284C7` | Active input & focus indicator |
| `BrushTextPrimary` | `#0F172A` | Headings, primary labels, transcript text |
| `BrushTextSecondary` | `#475569` | Subtitles, metadata, timestamps |
| `BrushTextMuted` | `#94A3B8` | Placeholder text, page counters |
| `BrushAccentPrimary` | `#0284C7` | Primary CTA buttons |
| `BrushAccentHover` | `#0369A1` | Primary CTA hover state |
| `BrushSuccessBg` | `#ECFDF5` | Ready badge background |
| `BrushSuccessBorder` | `#A7F3D0` | Ready badge border |
| `BrushSuccessText` | `#065F46` | Ready status label |
| `BrushSuccessDot` | `#059669` | Live active indicator dot |
| `BrushDangerBg` | `#FEF2F2` | Destructive buttons |
| `BrushDangerText` | `#991B1B` | Destructive text |

---

## 3. Typography & Hierarchy

| Element | Size | Weight | Line Height |
|---|---|---|---|
| **App Title** | 20px | Bold (700) | 26px |
| **Section Header** | 18px | Bold (700) | 24px |
| **Card Header** | 14px | SemiBold (600) | 20px |
| **Body Primary** | 13px | Regular (400) | 18px |
| **Body Secondary** | 12px | Regular (400) | 16px |
| **Badge / Caption**| 11px | SemiBold (600) | 14px |
| **Font Family** | `Segoe UI Variable, Segoe UI, system-ui` |

---

## 4. Component Hierarchy & Patterns

### 1. Hub Window (`FlowHubWindow`)
- **Dimensions**: `1100×740` (Min: `900×600`).
- **Layout**: Fixed compact sidebar (`220px`) + Header bar (`52px`) + Dynamic content area + Footer status bar.
- **8 Consumer Tabs**:
  1. `Home` (Status, Mic, Shortcut, Test Box, KPIs, Recent Transcripts)
  2. `History` (FTS Search, Filters, Transcripts, Pagination, Export)
  3. `Dictionary` ("Teach FLOW your words", Term/Correction, Starred)
  4. `Snippets` ("Save text you use again and again", Trigger, Expansion)
  5. `Styles` (Natural, Professional, Casual, Concise)
  6. `Scratchpad` (Note list, Autosaving editor, Pin, Copy, Export)
  7. `Settings` (General, Dictation, Mic, Languages, Privacy, Flow Bar)
  8. `About` (Version, Invariants, Sovereignty Badge, Exit)

### 2. Flow Bar (`FloatingHudController`)
- **Dimensions**: `260×48` compact floating non-activating window (`WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`).
- **States**:
  - `Idle`: Hidden (`0×0` render).
  - `Listening`: Live RMS animated waveform bars + "Listening...".
  - `Transcribing`: Progress indicator + "Transcribing...".
  - `Inserting`: "Inserting...".
  - `Done`: Emerald check + "Done".

### 3. Controls (Zero White-Coded Leaks)
- **ComboBox**: Custom popup template with dark elevated surface (`#0E1422`), custom border, and styled hover highlight (`#1E2B4C`).
- **TextBox**: Caret brush `#38BDF8`, padding 10/7, border focus transition, no white background on focus.
- **ScrollBar**: 8px slim track, transparent background, rounded thumb `#334155`.
- **Slider**: Custom track and thumb geometry with direct tick snapping.
