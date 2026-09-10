# DEPENDENCIES.md — Dependency Strategy & Supply Chain Provenance (Windows Native)

> **Status**: Windows Realignment Baseline  
> **Target Platform**: Windows 10/11 x64 Native Desktop Application  
> **Mandate**: Strict supply chain security, deterministic builds, permissive licensing, and zero unauthorized runtime bloat.  

---

## 1. Dependency Philosophy & Architectural Rules

1. **Windows Native First**: Prefer native Windows platform APIs (`WASAPI`, `Windows UI Automation`, `Windows.Graphics.Capture`, `Direct3D11`) before introducing external third-party libraries.
2. **Zero Bulky Runtime Wrappers**: **NO Python runtimes**, Electron wrappers, or heavy Node.js engines are permitted in the client desktop distribution.
3. **Deterministic Version Pinning**: All NuGet and native dependencies must be pinned to exact versions. Dynamic version wildcards (`*`) are strictly prohibited.
4. **Cryptographic Provenance**: Model weights and native FFI binaries must have verifiable SHA-256 checksums documented in this manifest.
5. **Permissive Licensing**: Every dependency must carry a verified permissive license (MIT, Apache 2.0, BSD-3-Clause). No GPL, AGPL, or ambiguous non-commercial licenses are permitted.

---

## 2. Proposed & Evaluated Dependencies Inventory

### Host Application & Core Runtime

#### 1. .NET 9 Runtime & SDK
* **Provider**: Microsoft / .NET Foundation
* **Version**: `9.0.x` (LTS/Current)
* **License**: MIT License
* **Repository**: [https://github.com/dotnet/runtime](https://github.com/dotnet/runtime)
* **Type**: Runtime & Build framework
* **Platform**: Windows 10/11 x64, ARM64
* **GPU Requirements**: None
* **Redistribution**: Permitted under MIT. Bundled via self-contained or Native AOT publishing.
* **Security & Commercial Considerations**: Fully supported enterprise framework with monthly security patches.

#### 2. Windows App SDK / WinUI 3
* **Provider**: Microsoft
* **Version**: `1.5.x` / `1.6.x`
* **License**: MIT License
* **Repository**: [https://github.com/microsoft/WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK)
* **Type**: UI Framework & Desktop Integration
* **Platform**: Windows 10 (1809+) and Windows 11
* **GPU Requirements**: DirectX 11+ capable GPU or Windows WARP software rasterizer.
* **Redistribution**: Permitted under MIT.
* **Security & Commercial Considerations**: Official modern UI stack for Windows desktop applications.

#### 3. Microsoft.Data.Sqlite
* **Provider**: Microsoft
* **Version**: `9.0.x`
* **License**: MIT License
* **Repository**: [https://github.com/dotnet/efcore](https://github.com/dotnet/efcore)
* **Type**: Embedded Database Client
* **Platform**: Windows x64 / ARM64
* **GPU Requirements**: None
* **Redistribution**: Permitted under MIT. Includes embedded SQLite engine.
* **Security & Commercial Considerations**: Local encrypted storage backed by DPAPI.

---

### Local AI & Inference Engines

#### 4. ONNX Runtime with DirectML (`Microsoft.ML.OnnxRuntime.DirectML`)
* **Provider**: Microsoft
* **Version**: `1.18.x` / `1.19.x`
* **License**: MIT License
* **Repository**: [https://github.com/microsoft/onnxruntime](https://github.com/microsoft/onnxruntime)
* **Type**: High-Performance ML Inference Runtime
* **Platform**: Windows 10/11 x64 / ARM64
* **GPU Requirements**: DirectX 12 capable GPU (NVIDIA GeForce/RTX, AMD Radeon, Intel Arc) or NPU (Copilot+).
* **Redistribution**: Permitted under MIT.
* **Security & Commercial Considerations**: Highly optimized by Microsoft for Windows hardware acceleration.

#### 5. whisper.cpp (Native C++ Inference)
* **Provider**: Georgi Gerganov
* **Version**: `v1.7.x`
* **License**: MIT License
* **Repository**: [https://github.com/ggerganov/whisper.cpp](https://github.com/ggerganov/whisper.cpp)
* **Type**: Lightweight Whisper C++ engine
* **Platform**: Windows x64 (AVX2, AVX-512, CUDA, or OpenVINO)
* **GPU Requirements**: Optional (falls back gracefully to multi-threaded CPU AVX2).
* **Redistribution**: Permitted under MIT.
* **Security & Commercial Considerations**: Self-contained, zero external network calls, widely battle-tested.

#### 6. Silero VAD (ONNX Model)
* **Provider**: Silero Team
* **Version**: `v5.0`
* **License**: MIT License
* **Repository**: [https://github.com/snakers4/silero-vad](https://github.com/snakers4/silero-vad)
* **Type**: Neural Voice Activity Detector
* **Platform**: Universal ONNX
* **Footprint**: $\approx 1.8\text{ MB}$
* **Redistribution**: Permitted under MIT.
* **Security & Commercial Considerations**: Offline voice boundary detection with negligible CPU load.

---

### Cloud Integration (Opt-In)

#### 7. AWS SDK for .NET (`AWSSDK.BedrockRuntime`)
* **Provider**: Amazon Web Services
* **Version**: `3.7.x`
* **License**: Apache License 2.0
* **Repository**: [https://github.com/aws/aws-sdk-net](https://github.com/aws/aws-sdk-net)
* **Type**: Cloud Client Library
* **Platform**: .NET Standard 2.0 / .NET 8 / .NET 9
* **Redistribution**: Permitted under Apache 2.0.
* **Security & Commercial Considerations**: Used solely for on-demand cloud features (Prompt Engineering, Screen AI). Zero usage during core local voice dictation.
