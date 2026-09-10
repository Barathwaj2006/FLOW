# SECURITY.md — Security & Privacy Architecture

> **Status**: Inviolable Policy  
> **Classification**: Security Architecture Baseline  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  

---

## 1. Security Philosophy & Threat Model

FLOW operates at the intersection of sensitive audio capture, system-wide Accessibility APIs, screen recording, and cloud AI reasoning. A compromise in any of these surfaces could expose private conversations, passwords, or intellectual property.

### Threat Vectors & Mitigations Matrix

| Threat Vector | Severity | Attack Scenario | FLOW Defense Strategy |
| :--- | :--- | :--- | :--- |
| **Audio Snooping / Eavesdropping** | Critical | Malware or rogue network requests intercept microphone audio. | **Hard Local Quarantine**: Raw audio buffers are processed in volatile memory only and never written to persistent disk or transmitted across network interfaces. |
| **Unintended Message Sending** | Critical | App accidentally simulates `Return`/`Enter` in Slack or Mail, sending an unreviewed draft. | **Strict Keycode Blacklist**: The text injection engine is strictly prohibited from emitting keycode `0x24` (`kVK_Return`) or Keypad Enter under any circumstance. |
| **Screen Content Leakage** | High | Screen AI captures confidential credentials or adjacent private windows. | **User-Defined Bounding Box Only**: Capture is strictly limited to the user's manual rectangular selection via `ScreenCaptureKit`. Full-screen capture without explicit user crop is impossible. |
| **Clipboard Data Corruption / Theft** | High | App reads or overwrites user's private passwords stored in clipboard. | **Pasteboard Isolation & Quick Restore**: Clipboard fallback backs up the existing pasteboard, posts `Cmd+V`, and restores the previous clipboard state within 150ms. Clipboard contents are never logged. |
| **AWS Credential Compromise** | Critical | AWS API keys or Bedrock tokens exposed on disk or git. | **Secure Keychain Storage & SigV4**: Credentials are stored in the macOS Keychain (`kSecClassGenericPassword`). API requests use short-lived SigV4 signed tokens. |
| **AI Intent Corruption (Hallucination)** | High | AI silently alters a negative constraint (*"Do not use Firebase"*) into an unwanted action. | **Content Lock Engine**: Mathematical and token-level bi-directional verification blocks insertion if protected constraints or entities are mutated. |

---

## 2. macOS Permissions Architecture

FLOW requires three privileged macOS system permissions. Each permission is audited and managed through a centralized `PermissionManager`:

```
┌─────────────────────────────────────────────────────────────┐
│                    FLOW Permission Guard                    │
├──────────────────────────┬──────────────────────────────────┤
│ Permission               │ Purpose & Scope                  │
├──────────────────────────┼──────────────────────────────────┤
│ 1. Microphone Access     │ Capture local audio stream for   │
│    (AVCaptureDevice)     │ local VAD and Whisper ASR.       │
├──────────────────────────┼──────────────────────────────────┤
│ 2. Accessibility         │ Read active cursor position and  │
│    (AXUIElement)         │ inject text directly into fields.│
├──────────────────────────┼──────────────────────────────────┤
│ 3. Screen Recording      │ Capture user-selected rectangle  │
│    (ScreenCaptureKit)    │ for Screen AI features.          │
└──────────────────────────┴──────────────────────────────────┘
```

### Permission Handling Guidelines
* **No Crashing on Missing Rights**: If a user revokes permissions, FLOW must gracefully display a clear status indicator in the menu bar and guide the user to the macOS System Settings pane (`x-apple.systempreferences:com.apple.preference.security`).
* **Just-In-Time Prompts**: Screen recording permissions are never requested on initial app launch; they are only requested when the user triggers Screen AI for the first time.

---

## 3. Local Audio Sovereignty & Data Minimization

### Audio Buffer Quarantine
1. **Volatile Memory Only**: Microphone audio captured via `AVAudioEngine` resides in pre-allocated circular RAM buffers (`AudioRingBuffer`).
2. **Immediate Zeroing**: Audio memory is zeroed out as soon as Whisper ASR finishes processing the utterance.
3. **No Audio Persistence**: FLOW does not offer an "audio playback" feature for dictation history. The SQLite database stores only the resulting cleaned text, word count, and latency metrics—never the audio waveforms.

### Cloud Data Minimization
When the user explicitly invokes an AWS Bedrock feature (Prompt Engineer, Reply Generator, Screen AI):
* Audio is **NEVER** sent to AWS. Only the local transcribed text or the cropped screenshot is transmitted.
* The API payload contains strictly the text necessary to fulfill the user's prompt.
* Background system windows, unseen clipboard items, and surrounding screen contents are excluded.

---

## 4. Text Injection Safety Protocol

Text injection is the mechanism by which FLOW inserts processed speech into external applications.

### Inviolable Injection Rules:
1. **Never Send**: Under no circumstance shall FLOW programmatically trigger an action that commits, sends, or executes text (e.g., `Return`, `Shift+Return` with auto-send triggers, or mouse clicks on UI buttons).
2. **Focus Verification**: Prior to injecting text, FLOW verifies that the active target element is an editable text field (`kAXRoleAttribute == kAXTextAreaRole` or `kAXTextFieldRole`).
3. **Clipboard Hygiene**:
   * If `AXUIElementSetValue` fails, FLOW employs the clipboard injection fallback (`NSPasteboard`).
   * The fallback immediately preserves existing pasteboard contents, types, and metadata.
   * After emitting `Cmd+V`, FLOW restores the original pasteboard contents within 150ms.

---

## 5. Cloud Security & AWS Integration

### Identity & Access Management (IAM)
* AWS credentials for developers or enterprise clients must use IAM policies restricted strictly to `bedrock:InvokeModel` and `bedrock:InvokeModelWithResponseStream`.
* No `admin`, S3, DynamoDB, or unrestricted Bedrock access permissions are allowed.

### Minimum Privilege Policy Example:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowBedrockInferenceOnly",
      "Effect": "Allow",
      "Action": [
        "bedrock:InvokeModel",
        "bedrock:InvokeModelWithResponseStream"
      ],
      "Resource": [
        "arn:aws:bedrock:*:*:foundation-model/anthropic.claude-3-5-sonnet-*",
        "arn:aws:bedrock:*:*:foundation-model/anthropic.claude-3-5-haiku-*",
        "arn:aws:bedrock:*:*:foundation-model/amazon.nova-*"
      ]
    }
  ]
}
```

---

## 6. Local Storage Security

* **SQLite Database**: Stored in the application's sandboxed support directory (`~/Library/Application Support/com.flow.mac/flow.sqlite`).
* **Database Encryption**: Sensitive tables (user history, dictionary, preferences) are encrypted at rest using AES-256 via SQLCipher, with the encryption key securely stored in the macOS Keychain.
* **Full Data Wipe**: The application must provide a one-click **"Clear All History & Cache"** setting that purges the SQLite database and destroys all cached assets.

---

## 7. Vulnerability Disclosure Policy

If you discover a security or privacy vulnerability in FLOW:
1. Do not open a public GitHub issue.
2. Submit a confidential report to `security@flow.ai` (or repository maintainers).
3. Include detailed reproduction steps, target macOS version, and impact analysis.
4. Maintainers commit to acknowledging reports within 24 hours and providing a remediation timeline within 72 hours.
