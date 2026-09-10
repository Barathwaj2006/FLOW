# SECURITY.md — Windows Security & Privacy Architecture

> **Status**: Windows Realignment Baseline  
> **Target Platform**: Windows 10/11 x64 Native Desktop Application  
> **Mandate**: End-to-end defense-in-depth, local audio isolation, Windows UIPI boundary respect, and zero automated submission.  

---

## 1. Windows Threat Model & Defense Matrix

FLOW operates across privileged Windows boundaries including global keyboard hooks, WASAPI audio capture, UI Automation, screen capture, and optional cloud AI.

| Threat Vector | Severity | Attack Scenario | FLOW Defense Strategy |
| :--- | :--- | :--- | :--- |
| **Audio Snooping / Persistent Leakage** | Critical | Malware or rogue background threads intercept or exfiltrate microphone audio. | **Volatile RAM Quarantine**: Audio buffers reside exclusively in pre-allocated non-paged RAM and are zeroed immediately post-transcription. Zero audio files are written to disk. |
| **Accidental Command Execution / Send** | Critical | FLOW simulates `Enter` in Slack, Word, or an elevated PowerShell terminal, sending unreviewed text or executing a command. | **Strict Virtual Keycode Blacklist**: The text insertion engine hard-filters `VK_RETURN` (`0x0D`), `VK_SEPARATOR`, and keypad enter. Text insertion places characters only; dispatching remains strictly manual. |
| **UIPI & Privilege Elevation Boundary** | High | A standard-user FLOW instance attempts to inspect or inject into an elevated (Administrator) window, causing silent failure or shatter attacks. | **User Interface Privilege Isolation (UIPI) Guard**: FLOW detects the integrity level of the focused window. If the target is elevated, FLOW alerts the user rather than hanging or attempting unauthorized injection. |
| **Clipboard Data Theft / Corruption** | High | FLOW overwrites the user's password or sensitive token currently stored on the Windows clipboard. | **Atomic Backup & Fast Restore**: The clipboard fallback backs up all formats via Win32 `OpenClipboard`, emits `Ctrl+V`, and restores the exact original clipboard state within 150ms. |
| **Screen Content Privacy Overreach** | High | Screen AI captures confidential browser tabs, passwords, or background desktop windows. | **Strict Crop Bounding Box**: Uses `Windows.Graphics.Capture` to capture strictly the user's manual rectangular drag selection. Full desktop capture without explicit selection is architecturally prohibited. |
| **Prompt Injection via Screen AI** | High | Malicious text in a screenshot attempts to hijack LLM system prompts. | **System Prompt Hardening & Content Lock**: Prompt templates encapsulate OCR text within strict XML delimiters (`<screen_content>...<screen_content>`). Content Lock verifies that no unauthorized commands are executed. |
| **Local Database Exposure** | Medium | Another local application inspects the user's local dictionary, snippets, or history. | **Windows DPAPI Encryption**: SQLite databases stored in `%LOCALAPPDATA%\Flow` are encrypted using the Windows Data Protection API (DPAPI) keyed to the logged-in Windows user account. |

---

## 2. Windows Privacy & Permissions Model

### Microphone Access
* **Windows Settings**: FLOW monitors the Windows 10/11 microphone capability status (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone`).
* **Graceful Degradation**: If microphone access is denied by Windows privacy settings or group policy, FLOW surfaces a non-blocking toast notification with a direct link to `ms-settings:privacy-microphone`.

### User Interface Automation (UIA) Boundary
* Standard user applications cannot inject keystrokes or inspect applications running at higher integrity levels (Integrity Levels: Low $\rightarrow$ Medium $\rightarrow$ High $\rightarrow$ System).
* When FLOW encounters an elevated window (e.g. Administrator Command Prompt), it checks if `uiAccess="true"` is configured or prompts the user to launch FLOW with appropriate permissions.

---

## 3. Data Minimization Principle: "Minimum Necessary Context"

When utilizing Screen AI or Context Intelligence:
1. **Never Scrape the Entire Desktop**: Capture is mathematically bounded to the bounding rectangle $R = [x, y, w, h]$ selected by the user.
2. **Never Collect Unfocused Windows**: Context extraction queries solely `IUIAutomation::GetFocusedElement` and its immediate ancestors. Background application windows are never scanned.
3. **Sensitive Field Detection**: If a target UI element exposes `UIA_IsPasswordPropertyId == true`, text inspection is automatically disabled.

---

## 4. Cloud Security & AWS Bedrock Boundary

When the user explicitly invokes an Amazon Bedrock feature:
* **Audio Is Never Sent**: Only local transcribed text or the cropped screenshot is transmitted.
* **Encryption in Transit**: Strict TLS 1.3 with modern cipher suites.
* **Credential Hygiene**: In developer/BYOK mode, AWS credentials are read from standard environment variables or `%USERPROFILE%\.aws\credentials`. No secrets are hardcoded in application binaries.
* **Enterprise Model Privacy**: Amazon Bedrock guarantees that customer prompt payloads are never stored or used to train base foundation models.

---

## 5. Security Incident Reporting

If you identify a security or privacy vulnerability:
1. Do not report it publicly via GitHub Issues.
2. Email a detailed disclosure to `security@flow.ai` (or repository maintainers).
3. Include target Windows build, reproduction steps, and impact assessment.
