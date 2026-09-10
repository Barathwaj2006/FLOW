# THIRD_PARTY_NOTICES.md — Open Source Licenses & Model Provenance (Windows Native)

> **Status**: Windows Realignment Baseline  
> **Project**: FLOW — AI Voice Productivity Platform for Windows  

This document lists third-party software components, libraries, and machine learning models evaluated or incorporated into FLOW for Windows, along with their respective licenses and provenance notices.

---

## 1. Machine Learning Models & Inference Engines

### OpenAI Whisper (Weights & Architecture)
* **Creator / Maintainer**: OpenAI
* **License**: MIT License
* **Source**: [https://github.com/openai/whisper](https://github.com/openai/whisper)
* **Evaluated Quantizations**: GGML (`q4_0`, `q5_0`) and ONNX int8/fp16 for DirectML.
* **Provenance**: Models are converted using audited scripts or downloaded from verified official releases with cryptographic SHA-256 validation.

### Silero VAD (Voice Activity Detection)
* **Creator / Maintainer**: Silero Team
* **License**: MIT License
* **Source**: [https://github.com/snakers4/silero-vad](https://github.com/snakers4/silero-vad)
* **Version**: Silero VAD v5 ONNX
* **Usage**: On-device real-time voice activity detection, chunk segmentation, and silence gating.

### whisper.cpp
* **Creator / Maintainer**: Georgi Gerganov
* **License**: MIT License
* **Source**: [https://github.com/ggerganov/whisper.cpp](https://github.com/ggerganov/whisper.cpp)
* **Usage**: High-performance C++ implementation of Whisper for Windows x64 with AVX2 CPU and GPU backends.

---

## 2. Core Libraries & Dependencies

### ONNX Runtime & DirectML
* **Creator / Maintainer**: Microsoft Corporation
* **License**: MIT License
* **Source**: [https://github.com/microsoft/onnxruntime](https://github.com/microsoft/onnxruntime)
* **Usage**: Hardware-accelerated inference across DirectX 12 GPUs and Windows Copilot+ NPUs.

### .NET 9 & Windows App SDK (WinUI 3)
* **Creator / Maintainer**: Microsoft Corporation / .NET Foundation
* **License**: MIT License
* **Source**: [https://github.com/dotnet/runtime](https://github.com/dotnet/runtime) / [https://github.com/microsoft/WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK)
* **Usage**: Application runtime, presentation framework, and native Windows desktop integration.

### Microsoft.Data.Sqlite
* **Creator / Maintainer**: Microsoft Corporation
* **License**: MIT License
* **Source**: [https://github.com/dotnet/efcore](https://github.com/dotnet/efcore)
* **Usage**: Embedded local SQLite database layer for user dictionary, snippets, and history.

### AWS SDK for .NET
* **Creator / Maintainer**: Amazon Web Services
* **License**: Apache License 2.0
* **Source**: [https://github.com/aws/aws-sdk-net](https://github.com/aws/aws-sdk-net)
* **Usage**: Client for AWS Bedrock Runtime invocation for opt-in cloud AI features.

---

## 3. License Texts

### MIT License
```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Apache License 2.0
```text
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

---

## 4. Compliance & Distribution Guidelines

1. **Commercial Distribution**: All evaluated libraries and models carry permissive licenses (MIT and Apache 2.0) permitting commercial closed-source distribution.
2. **Attribution**: The final Windows distribution package will bundle this notice document in the installation directory and surface open-source notices in the Settings window.
3. **Copyleft Protection**: No GPL, AGPL, or restrictive copyleft dependencies are permitted in the client distribution.
