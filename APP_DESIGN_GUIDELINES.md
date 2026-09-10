# APP_DESIGN_GUIDELINES.md — Windows Desktop UX & Anti-Vibecode Master Directive

> **Status**: Active Mandatory Product Standard  
> **Target**: FLOW Windows-Native Desktop Application (WinUI 3 / Windows App SDK)  
> **Master Directive**: Credibility $\longrightarrow$ Clarity $\longrightarrow$ Speed $\longrightarrow$ Usability $\longrightarrow$ Trust $\longrightarrow$ Precision  

FLOW is a **Windows-native desktop application** distributed as an installer/executable and designed to run locally on Windows 10 and 11. It is NOT a website, NOT an APK, NOT a PWA, and NOT a browser-first wrapper.

The application must feel like it was engineered by an experienced Windows software product team. It must **NOT look or behave like a vibecoded application**.

---

## 1. What "Not Vibecoded" Means

### Strictly Prohibited Aesthetics & Tropes
* **NO Generic AI-Generated Layouts**: No arbitrary card grids, oversized hero cards, or visual templates designed for web mockups.
* **NO Purple AI Aesthetics**: Never use purple, violet, or cyan accents merely because they are clichéd in generative AI marketing.
* **NO Random Gradients or Glowing Blobs**: Zero glowing background orbs, animated mesh gradients, or neon borders.
* **NO Excessive Glassmorphism**: Avoid stacking semi-transparent frosted cards. Surface materials must use Windows 11 standard Mica or Acrylic subtly and strictly where functionally appropriate.
* **NO Giant Pill-Shaped Buttons Everywhere**: Buttons must use moderate standard corner radii ($4\text{px} - 6\text{px}$). Pills are a specific component type (e.g. status tags), not a universal layout style.
* **NO Fabricated UI Artifacts**:
  * Never render fake terminal windows.
  * Never render fake analytics dashboards or graphs.
  * Never render fake code editors.
  * Never render fake metrics.
* **NO Emoji as UI Design**: Emojis must **NEVER** serve as feature icons, button icons, navigation markers, or section headers. Use a coherent, native vector iconography system (e.g. Segoe Fluent Icons) with consistent stroke weight and optical size.
* **NO Placeholder Content in Production UI**: Zero instances of `Lorem ipsum`, `TODO`, `Coming soon`, `Test User`, `John Doe`, or fake company names. If a feature or data is unavailable, omit it or state the actual system state clearly.

---

## 2. Native Windows Desktop UX Standards

FLOW must feel like a native citizen of Windows 10 and Windows 11, not a website wrapped in a desktop frame.

### Key Windows Desktop Conventions
* **Window Behaviors**: Standard title bar styling, native Min/Max/Close controls, proper Snap Layouts support (Windows 11).
* **Keyboard First**: Every action, setting, and workflow must be 100% operable via keyboard alone with visible, high-contrast focus rings.
* **System Tray (`NotifyIcon`)**: Sits unobtrusively in the Windows taskbar notification area with a standard Win32/WinUI context menu (`Open Settings`, `Check Permissions`, `Pause Dictation`, `Exit`).
* **High-DPI & Multi-Monitor**: Pixel-perfect rendering across multi-monitor environments with dynamic per-monitor DPI scaling (100%, 125%, 150%, 175%, 200%).
* **Theme Adaptability**: Seamless automatic support for Windows Light Mode, Dark Mode, and High Contrast Accessibility themes.
* **Notifications**: Native Windows Action Center toasts (`Microsoft.Windows.AppNotifications`) for critical alerts (e.g., microphone disconnected), never custom floating notification banners inside other apps.

---

## 3. Floating HUD Specification

The floating HUD is the primary user-facing touchpoint during active dictation.

```
┌─────────────────────────────────────────────────────────────┐
│                     FLOW FLOATING HUD                       │
├─────────────────────────────────────────────────────────────┤
│  [Status Dot]  Listening...                  [P50: 310ms]   │
└─────────────────────────────────────────────────────────────┘
```

### HUD Requirements:
* **Minimal & Unobtrusive**: Compact footprint positioned near the active cursor or docked at bottom-center.
* **Keyboard-Safe & Non-Activating**: Must use Win32 window styles:
  `WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`
  It must **NEVER** steal keyboard focus from the active text field (e.g. in VS Code, Word, or Chrome).
* **Deterministic States**:
  * `Idle`: Hidden or tiny quiescent tray presence.
  * `Listening`: Subtle status dot indicating active microphone capture.
  * `Processing`: Clean, minimal activity indicator.
  * `Completed`: Transient visual confirmation ($< 300\text{ ms}$) before fading out.
  * `Error`: Clear, actionable message (e.g. *"Microphone muted in Windows Settings"*).
  * `Permission Required`: Direct prompt to enable Windows Microphone access.
* **NO Giant Animated Orb**: Do NOT turn the HUD into a pulsating, glowing, or swirling AI orb.

---

## 4. Design System Architecture

### A. Typography Hierarchy
Utilize the native Windows system font family: **Segoe UI Variable** (Windows 11) with fallback to **Segoe UI** (Windows 10).

| Role | Font Size | Weight | Line Height |
| :--- | :--- | :--- | :--- |
| **App Title / Header** | 20px | Semi-Bold (600) | 28px |
| **Section Title** | 16px | Semi-Bold (600) | 24px |
| **Body Text** | 14px | Regular (400) | 20px |
| **Secondary / Caption**| 12px | Regular (400) | 16px |
| **Monospace / Code** | 13px | Regular (Consolas / Cascadia Code) | 18px |

### B. Spacing Scale (8pt Grid)
Strictly enforce an 8-point geometric scale:
$$2\text{px}, 4\text{px}, 8\text{px}, 12\text{px}, 16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}$$
Do not select arbitrary margins or padding.

### C. Semantic Color System
Colors are defined semantically and bound to Windows Theme Resources:

```text
Background     → SolidBackgroundFillColorBase / MicaAlt
Surface        → CardBackgroundFillColorDefault
Primary        → SystemAccentColor (User's Windows Accent Color)
TextPrimary    → TextFillColorPrimary
TextSecondary  → TextFillColorSecondary
Success        → SystemFillColorSuccess
Warning        → SystemFillColorCaution
Error          → SystemFillColorCritical
FocusBorder    → FocusStrokeColorOuter
```

---

## 5. Engineering Quality & Anti-"Magic Code" Rules

* **No Magic Constants**: Every threshold, buffer size, timeout, and window dimension must be a named, documented constant with architectural rationale.
* **No Swallowed Exceptions**: Empty `catch` blocks are strictly forbidden. Every failure must produce a user-facing recovery state or structured log.
* **Deterministic Concurrency**: All background threads (WASAPI capture, keyboard hooks, ASR dispatch) must pass explicit `CancellationToken`s and handle task cancellation gracefully.
* **Zero Memory Leaks**: COM interfaces (`IUIAutomation`, `IAudioClient3`) and native unmanaged buffers must be explicitly disposed.

---

## 6. Visual Review Gate Before Declaring UI Complete

Before marking any UI screen or component complete, evaluate the **6 Desktop Review Questions**:

1. **Does this look like a professional, enterprise-grade Windows desktop application?**  
   *If it looks like a website inside a frame, redesign it.*
2. **Does every visual element have a clear, functional purpose?**  
   *If an element is purely decorative or filler, eliminate it.*
3. **Does the UI respect native Windows desktop ergonomics?**  
   *Full keyboard navigability, standard dialogs, correct tab orders, visible focus states.*
4. **Does it avoid generic AI-product aesthetics?**  
   *Zero purple gradients, zero glowing blobs, zero fake dashboards.*
5. **Is anything visually exaggerated merely to make a demo impressive?**  
   *Reject flashy gimmicks that compromise usability or speed.*
6. **Could a professional Windows engineer install and use this daily without distraction?**  
   *If no, continue refining.*
