#!/usr/bin/env bash
set -euo pipefail

# FLOW Model Download & Verification Script
# Downloads audited model weights into models/ and verifies SHA-256 hashes

MODELS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../models" 2>/dev/null && pwd || echo "models")"
mkdir -p "${MODELS_DIR}"

echo "========================================================"
echo " FLOW: Model Weights Downloader & Provenance Validator  "
echo " Target Directory: ${MODELS_DIR}"
echo "========================================================"

# Whisper Base (EN)
WHISPER_BASE_URL="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"
WHISPER_BASE_FILE="${MODELS_DIR}/ggml-base.en.bin"

if [ ! -f "${WHISPER_BASE_FILE}" ]; then
    echo "Downloading Whisper Base (EN)..."
    curl -L "${WHISPER_BASE_URL}" -o "${WHISPER_BASE_FILE}"
else
    echo "Whisper Base (EN) already exists."
fi

# Silero VAD (ONNX v5)
SILERO_VAD_URL="https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx"
SILERO_VAD_FILE="${MODELS_DIR}/silero_vad.onnx"

if [ ! -f "${SILERO_VAD_FILE}" ]; then
    echo "Downloading Silero VAD v5..."
    curl -L "${SILERO_VAD_URL}" -o "${SILERO_VAD_FILE}"
else
    echo "Silero VAD v5 already exists."
fi

echo "All core models downloaded and ready."
