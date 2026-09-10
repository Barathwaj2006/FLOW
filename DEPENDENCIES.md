# DEPENDENCIES.md — Dependency Strategy & Supply Chain Provenance

> **Status**: Active Baseline  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  
> **Mandate**: Strict supply chain security, deterministic builds, and zero unauthorized runtime bloat.  

---

## 1. Dependency Philosophy & Architectural Rules

To guarantee enterprise-grade security, instant cold-start times ($< 300\text{ ms}$), and minimal memory footprints, FLOW adheres to strict dependency governance:

1. **Native-First Policy**: Prefer native Apple frameworks (`AVFoundation`, `AppKit`, `ScreenCaptureKit`, `Vision`, `Accelerate`, `Security`) before introducing third-party code.
2. **Zero Bulky Runtime Wrappers**: **NO Python runtimes**, Electron containers, or embedded Node.js engines are permitted in the client application bundle. All core logic must run as native compiled Swift or C/C++ machine code.
3. **Strict Version Pinning**: All Swift Package Manager (SPM) dependencies must be pinned to exact semantic versions or immutable commit SHAs. Dynamic branch tracking (`main`, `master`) is prohibited.
4. **Cryptographic Integrity**: Binary artifacts, neural network weights, and FFI targets must have verifiable SHA-256 checksums documented in this manifest.
5. **Permissive Licensing Only**: Every dependency must carry a verified permissive license (MIT, Apache 2.0, BSD-3-Clause). No GPL, AGPL, or ambiguous non-commercial licenses are permitted.

---

## 2. Pinned Dependency Inventory

### Core Engine Dependencies (`packages/FlowCore`)

| Package / Library | Provider / Repository | Version / Tag | License | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **WhisperKit** | `argmaxinc/WhisperKit` | `0.9.0` | MIT | Apple Silicon CoreML Whisper runner (ANE/Metal). |
| **GRDB.swift** | `groue/GRDB.swift` | `6.29.0` | MIT | Thread-safe, compile-time verified SQLite database layer. |
| **AWS SDK for Swift** | `awslabs/aws-sdk-swift` | `0.38.0` | Apache 2.0 | AWS Bedrock Runtime client (`bedrock-runtime`). |

### macOS Host Dependencies (`packages/FlowMacOS`)

| Package / Library | Provider / Repository | Version / Tag | License | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Sparkle** | `sparkle-project/Sparkle` | `2.6.4` | MIT | Cryptographically verified in-app software updates. |

### Native C/C++ FFI Targets (Embedded or Static)

| Subsystem | Source Repository | Commit SHA | License | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **whisper.cpp** | `ggerganov/whisper.cpp` | `v1.7.1` (`8d4c...`) | MIT | Universal Metal/CPU ASR fallback for Intel/non-CoreML Macs. |

---

## 3. Machine Learning Models & Weights Provenance

All neural network weights are downloaded through audited scripts (`tools/scripts/download_models.sh`) and verified against cryptographic hashes before being loaded into memory:

| Model Name | Upstream Source | Format | SHA-256 Checksum | Footprint |
| :--- | :--- | :--- | :--- | :--- |
| **Silero VAD v5** | `snakers4/silero-vad` | `.onnx` / `.mlmodelc` | `9b5a...` | $1.8\text{ MB}$ |
| **Whisper Tiny (EN)** | `openai/whisper-tiny.en` | CoreML `.mlmodelc` | `4c8d...` | $75\text{ MB}$ |
| **Whisper Base (EN)** | `openai/whisper-base.en` | CoreML `.mlmodelc` | `1a2f...` | $145\text{ MB}$ |
| **Whisper Small (Multilingual)** | `openai/whisper-small` | CoreML `.mlmodelc` | `7e3b...` | $465\text{ MB}$ |
| **Whisper Large v3 Turbo** | `openai/whisper-large-v3-turbo`| CoreML `.mlmodelc` | `f6a9...` | $1.6\text{ GB}$ |

---

## 4. Supply Chain Security & Review Workflow

### Adding or Updating Dependencies
1. **Request Proposal**: Submit an architectural request documenting the necessity, alternatives considered, license, and binary footprint impact.
2. **License Check**: Automated verification that the license is MIT, Apache 2.0, or BSD.
3. **Audit for Known CVEs**: Check using GitHub Advisory Database and automated SPM audit tools.
4. **Pinning Verification**: Commit hashes and checksums updated in `Package.resolved` and `DEPENDENCIES.md`.
