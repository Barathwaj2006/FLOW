# THIRD_PARTY_NOTICES.md — Open Source Licenses & Model Provenance

> **Status**: Active Provenance Record  
> **Project**: FLOW — AI Voice Productivity Platform for macOS  

This document lists third-party software libraries, runtimes, and machine learning models integrated into or bundled with FLOW, along with their respective licenses and provenance details.

---

## 1. Machine Learning Models & Weights

### OpenAI Whisper (Weights & Architecture)
* **Creator / Maintainer**: OpenAI
* **License**: MIT License
* **Source**: [https://github.com/openai/whisper](https://github.com/openai/whisper)
* **Model Versions Evaluated / Supported**:
  * `whisper-tiny.en` / `whisper-tiny`
  * `whisper-base.en` / `whisper-base`
  * `whisper-small` (Multilingual / Tamil)
  * `whisper-large-v3-turbo`
* **Provenance**: Models are downloaded from verified Hugging Face / OpenAI official releases or compiled into CoreML `.mlmodelc` bundles using validated scripts in `tools/scripts/`.

### Silero VAD (Voice Activity Detection)
* **Creator / Maintainer**: Silero Team
* **License**: MIT License
* **Source**: [https://github.com/snakers4/silero-vad](https://github.com/snakers4/silero-vad)
* **Version**: Silero VAD v4 / v5 ONNX / CoreML
* **Usage**: On-device real-time voice activity detection, chunk segmentation, and silence gating.

---

## 2. Core Libraries & Dependencies

### WhisperKit
* **Creator / Maintainer**: Argmax, Inc.
* **License**: MIT License
* **Source**: [https://github.com/argmaxinc/WhisperKit](https://github.com/argmaxinc/WhisperKit)
* **Purpose**: Native Swift implementation of Whisper optimized for Apple Silicon (CoreML, Apple Neural Engine, and Metal).

### whisper.cpp
* **Creator / Maintainer**: Georgi Gerganov
* **License**: MIT License
* **Source**: [https://github.com/ggerganov/whisper.cpp](https://github.com/ggerganov/whisper.cpp)
* **Purpose**: High-performance C/C++ port of OpenAI Whisper with Metal acceleration for universal macOS compatibility.

### GRDB.swift
* **Creator / Maintainer**: Gwendal Roué
* **License**: MIT License
* **Source**: [https://github.com/groue/GRDB.swift](https://github.com/groue/GRDB.swift)
* **Purpose**: Type-safe SQLite database toolkit for Swift managing local history, user dictionary, snippets, and app profiles.

### AWS SDK for Swift
* **Creator / Maintainer**: Amazon Web Services
* **License**: Apache License 2.0
* **Source**: [https://github.com/awslabs/aws-sdk-swift](https://github.com/awslabs/aws-sdk-swift)
* **Purpose**: Native Swift client for AWS Bedrock Runtime invocation (`bedrock-runtime`).

### Sparkle
* **Creator / Maintainer**: Sparkle Project
* **License**: MIT License
* **Source**: [https://github.com/sparkle-project/Sparkle](https://github.com/sparkle-project/Sparkle)
* **Purpose**: Secure, sandboxed software update framework for macOS applications.

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

1. **Commercial Distribution**: All bundled libraries and models are licensed under permissive open-source licenses (MIT and Apache 2.0) that permit commercial distribution and closed-source linking.
2. **Attribution Requirements**: The final application bundle (`FLOW.app`) must include a copy of this `THIRD_PARTY_NOTICES.md` file within its `Contents/Resources/` directory and surface an "Open Source Licenses" menu item in the About window.
3. **No GPL / AGPL Contamination**: No GPL, AGPL, or copyleft-licensed dependencies are permitted in the client application without explicit architectural review.
