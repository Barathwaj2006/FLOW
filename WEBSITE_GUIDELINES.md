# WEBSITE_GUIDELINES.md — FLOW Website Quality & Anti-AI-Slop Rules

> **Status**: Active Mandatory Governance  
> **Target**: Public Website, Landing Pages, Documentation Portal, and Web Presence  
> **Master Directive**: Truth $\longrightarrow$ Clarity $\longrightarrow$ Usability $\longrightarrow$ Aesthetics $\longrightarrow$ Marketing  

The FLOW website must look like a **real, professionally designed software product created by an experienced product and design team**. It must NOT look AI-generated, template-generated, hackathon-generated, or visually derivative of generic SaaS websites.

**Quality is strictly more important than speed.**

---

## 1. Visual Quality & Anti-Patterns

### Strictly Prohibited Generic AI-SaaS Tropes
* **NO Purple/Blue Gradients**: Never use purple/blue/cyan gradients merely because they are common on AI websites.
* **NO Excessive Gradients or Blobs**: No giant glowing background blobs, neon gradient borders, or diffuse mesh gradients.
* **NO Glassmorphism Everywhere**: No excessive backdrop-blur frosted cards layered on top of each other.
* **NO Excessive Shadows or Rounded Pills**: Avoid making every card a pill. Rounded corners must follow a component hierarchy.
* **NO Floating Decorative Elements**: No arbitrary floating cards, 3D icons, or floating badges that serve no functional purpose.
* **NO Fabricated UI Artifacts**:
  * Never render fake terminal windows.
  * Never render fake analytics dashboards or graphs.
  * Never render fake code editors.
  * Never render fake browser window chrome.
  * Never render decorative, AI-generated interface mockups.

### Typography Rules
* Clear visual hierarchy:
  $$\text{Product} \longrightarrow \text{Section} \longrightarrow \text{Supporting Explanation} \longrightarrow \text{Detail} \longrightarrow \text{Action}$$
* No huge, vague hero typography chosen purely for shock value.
* No random mixed font families, excessive font weights, or overly condensed display fonts that impair readability.

---

## 2. Copywriting & Content Honesty

### No Vague Marketing Copy
Eliminate empty buzzwords and platitudes:
* ❌ *"The future of productivity."*
* ❌ *"Unlock your potential."*
* ❌ *"AI that changes everything."*
* ❌ *"Work smarter. Live better."*
* ❌ *"Your productivity, reimagined."*

**Use concrete, specific language:**
> *"Speak anywhere on Windows. FLOW turns your voice into text, prompts, replies, and actions while keeping your original intent intact."*

Every section must answer:
1. What FLOW is.
2. Who it is for.
3. What specific problem it solves.
4. Why it is architecturally different.
5. How it actually works.
6. What the user can concretely do with it.

### No Em-Dash Marketing Style
Avoid lazy em-dash bulletizing in prose:
* ❌ *"FLOW is fast — intelligent — private."*
* ✔️ *"FLOW is fast, intelligent, and private."*

### No Fabricated Social Proof or Fake Metrics
* **Zero Fake Testimonials**: Never invent customer quotes, avatars, LinkedIn/GitHub profiles, or fake company logos.
* **Zero Fake Metrics**: Never display unmeasured claims (*"10,000+ users"*, *"99.9% accuracy"*, *"50M words processed"*, *"99.99% uptime"*).
* **Real Benchmarks Only**: If metrics are presented, they must be derived from verified empirical benchmarks (P50/P95/P99 latency, WER, CPU %, RAM MB). If not yet measured, state: *"Benchmarking in progress."*
* **Credibility Rule**: An empty section is far more credible than fabricated social proof.

---

## 3. Visual Assets, Imagery & Iconography

* **NO AI Stock Imagery**: Never use AI-generated stock photos, glowing brains, humanoid robots, holographic screens, futuristic offices, or smiling corporate teams.
* **Real Product Assets Only**: Imagery must consist of actual product UI, real screenshots, real recorded workflows, architectural diagrams, and purposeful technical illustrations.
* **NO Emoji as UI Design**: Emojis must NEVER serve as feature icons, navigation markers, or CTA bullets. Use a coherent, vector-based icon system with consistent stroke weight, optical size, and dimensions.

---

## 4. Animation & Interaction Discipline

* **NO Gratuitous Motion**: Do not add animations simply because a framework makes it easy.
* **Prohibited**:
  * Excessive scroll animations and parallax on every container.
  * Spinning objects, floating cards, bouncing elements.
  * Animated gradients or glowing borders.
  * Cursor-following trails, fake mouse pointers, or simulated typing.
* **Permitted**: Motion that communicates state change, layout transition, or user interaction with reduced-motion media query support (`prefers-reduced-motion: reduce`).

---

## 5. Design System Specifications

### Spacing Scale
Use a strict, consistent spacing scale:
$$4\text{px}, 8\text{px}, 12\text{px}, 16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 96\text{px}, 128\text{px}$$

### Component Radius Hierarchy
* Buttons: Moderate radius ($4\text{px} - 8\text{px}$)
* Cards: Moderate radius ($8\text{px} - 12\text{px}$)
* Inputs: Moderate radius ($4\text{px} - 6\text{px}$)
* Tags / Badges: Pill radius allowed only where semantically appropriate.

---

## 6. Technical, SEO & Performance Standards

* **Core Web Vitals**: Test and measure LCP, CLS, INP, and TTFB. Keep JavaScript and image payloads minimal.
* **Responsive Breakpoints**: Test explicitly at 320px, 375px, 768px, 1024px, 1280px, 1440px, and 1920px. Mobile layouts must be intentionally structured, not just scaled down.
* **Accessibility (WCAG AA)**: Semantic HTML5, full keyboard navigability, high contrast ratios, visible focus indicators, screen-reader labels.
* **Mandatory Brand Assets**: Favicon, Apple touch icon, Open Graph preview image, and clean SVG logo. Never ship placeholder SVGs or missing favicon assets.
* **Mandatory Legal Pages**: Dedicated `/privacy` and `/terms` routes. No fabricated compliance badges (GDPR, SOC-2, HIPAA) unless legally verified.
* **No Generator Watermarks**: No *"Made with AI"*, template credits, or builder branding.

---

## 7. The 6 Design Review Questions

Before approving any page, modal, or visual section, ask:

1. **Would this page still look credible if every animation were removed?**  
   *If no, the design is too dependent on gimmicks.*
2. **Is this element communicating something useful?**  
   *If no, remove it.*
3. **Is this claim empirically verified?**  
   *If no, remove or qualify it.*
4. **Does this look like FLOW or like a generic AI website?**  
   *If generic, redesign it.*
5. **Are we showing the real product?**  
   *If not, replace the mockup with actual product evidence.*
6. **Could a serious, professional software company publish this today?**  
   *If no, continue refining.*

---

## 8. Summary: What to Optimize For

| Do NOT Optimize For | ALWAYS Optimize For |
| :--- | :--- |
| "Wow" factor | **Credibility** |
| Flashy effects | **Clarity** |
| Trendiness | **Speed & Usability** |
| Artificial complexity | **Trust & Truthfulness** |
| Marketing hyperbole | **Engineering Precision** |
