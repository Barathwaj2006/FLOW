/**
 * FLOW Local HTTP Bridge Client
 * Connects the React UI to the native .NET 9 Host (Flow.Host.Windows) on http://127.0.0.1:5005
 * Enables 100% offline local Whisper speech-to-text inference with AVX2/DirectML acceleration.
 */

import { DictationEntry, DictionaryEntry, HardwareProfile, ModelStatusInfo, ModelDownloadProgress } from '../types';

let activePort = 5005;

export interface EngineStatus {
  connected: boolean;
  modelInstalled: boolean;
  modelName: string;
  engine: string;
  port: number;
  offlineSovereignty: string;
}

/**
 * Checks if the local FLOW .NET Host server is running on 127.0.0.1:5005 (or 5006).
 */
export async function checkLocalEngineHealth(): Promise<EngineStatus> {
  const ports = [activePort, 5005, 5006];
  const tried = new Set<number>();

  for (const port of ports) {
    if (tried.has(port)) continue;
    tried.add(port);

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 800);

      const res = await fetch(`http://127.0.0.1:${port}/api/status`, {
        signal: controller.signal,
        headers: { Accept: 'application/json' },
      });
      clearTimeout(timeoutId);

      if (res.ok) {
        const data = await res.json();
        activePort = port;
        return {
          connected: true,
          modelInstalled: data.modelInstalled ?? true,
          modelName: data.modelName ?? 'ggml-tiny.en.bin',
          engine: data.engine ?? 'Whisper.net Native Local Engine',
          port,
          offlineSovereignty: data.offlineSovereignty ?? '100% Offline (Zero Cloud Audio)',
        };
      }
    } catch {
      // Port unavailable, continue trying
    }
  }

  return {
    connected: false,
    modelInstalled: false,
    modelName: 'None',
    engine: 'None',
    port: 5005,
    offlineSovereignty: 'Standalone Web Mode (Host not detected)',
  };
}

/**
 * Transcribes audio via the local .NET Whisper engine over HTTP.
 */
export async function transcribeLocalAudio(
  audioBlob: Blob,
  language = 'en'
): Promise<{
  text: string;
  rawText: string;
  durationMs: number;
  inferenceDurationMs: number;
  confidence: number;
  engine: string;
}> {
  const res = await fetch(`http://127.0.0.1:${activePort}/api/transcribe`, {
    method: 'POST',
    headers: {
      'Content-Type': 'audio/wav',
      'X-Language': language,
    },
    body: audioBlob,
  });

  if (!res.ok) {
    const errText = await res.text().catch(() => 'Unknown server error');
    throw new Error(`Local Whisper transcription failed (${res.status}): ${errText}`);
  }

  return await res.json();
}

/**
 * Retrieves persisted dictation history from the local SQLite database.
 */
export async function fetchLocalHistory(): Promise<DictationEntry[]> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/history`, {
      headers: { Accept: 'application/json' },
    });
    if (res.ok) {
      return await res.json();
    }
  } catch {}
  return [];
}

/**
 * Retrieves personal dictionary entries from the local SQLite database.
 */
export async function fetchLocalDictionary(): Promise<DictionaryEntry[]> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/dictionary`, {
      headers: { Accept: 'application/json' },
    });
    if (res.ok) {
      return await res.json();
    }
  } catch {}
  return [];
}

/**
 * Adds or updates a dictionary entry in the local SQLite database.
 */
export async function saveLocalDictionaryEntry(entry: {
  term: string;
  replacement?: string;
  isStarred?: boolean;
  category?: string;
}): Promise<boolean> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/dictionary`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(entry),
    });
    return res.ok;
  } catch {
    return false;
  }
}

/**
 * Verifies email and authentication code using the local host DPAPI security module.
 */
export async function verifyLocalAuth(
  email: string,
  code: string
): Promise<{ token: string; email: string; credits: number; plan: string }> {
  const res = await fetch(`http://127.0.0.1:${activePort}/api/auth/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, code }),
  });

  if (!res.ok) {
    throw new Error(`Authentication verification failed: ${res.statusText}`);
  }

  return await res.json();
}

/**
 * Encodes raw Float32 audio samples into a canonical 16kHz mono 16-bit PCM WAV Blob.
 */
export function encodeWavFromFloat32(samples: Float32Array, sampleRate = 16000): Blob {
  const buffer = new ArrayBuffer(44 + samples.length * 2);
  const view = new DataView(buffer);

  function writeString(offset: number, str: string) {
    for (let i = 0; i < str.length; i++) {
      view.setUint8(offset + i, str.charCodeAt(i));
    }
  }

  // RIFF header
  writeString(0, 'RIFF');
  view.setUint32(4, 36 + samples.length * 2, true);
  writeString(8, 'WAVE');

  // "fmt " chunk
  writeString(12, 'fmt ');
  view.setUint32(16, 16, true); // Subchunk1Size (16 for PCM)
  view.setUint16(20, 1, true); // AudioFormat (1 = PCM)
  view.setUint16(22, 1, true); // NumChannels (1 = mono)
  view.setUint32(24, sampleRate, true); // SampleRate
  view.setUint32(28, sampleRate * 2, true); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
  view.setUint16(32, 2, true); // BlockAlign (NumChannels * BitsPerSample/8)
  view.setUint16(34, 16, true); // BitsPerSample (16 bits)

  // "data" chunk
  writeString(36, 'data');
  view.setUint32(40, samples.length * 2, true);

  // Write 16-bit PCM samples
  let offset = 44;
  for (let i = 0; i < samples.length; i++, offset += 2) {
    const s = Math.max(-1, Math.min(1, samples[i]));
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true);
  }

  return new Blob([view], { type: 'audio/wav' });
}

/**
 * Fetches the detected hardware acceleration profile (DirectML GPU/NPU/CPU) from the local host.
 */
export async function fetchHardwareProfile(): Promise<HardwareProfile | null> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/hardware`, {
      headers: { Accept: 'application/json' },
    });
    if (res.ok) {
      return await res.json();
    }
  } catch {}
  return null;
}

/**
 * Retrieves the status and integrity of all available local Whisper models.
 */
export async function fetchModelsList(): Promise<ModelStatusInfo[]> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/models`, {
      headers: { Accept: 'application/json' },
    });
    if (res.ok) {
      return await res.json();
    }
  } catch {}
  return [];
}

/**
 * Triggers background resumable download for the specified Whisper model.
 */
export async function startModelDownload(modelName: string): Promise<boolean> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/models/download`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ modelName }),
    });
    return res.ok;
  } catch {
    return false;
  }
}

/**
 * Fetches real-time download progress, percent, and transfer speed (MB/s).
 */
export async function fetchDownloadProgress(): Promise<ModelDownloadProgress | null> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/models/progress`, {
      headers: { Accept: 'application/json' },
    });
    if (res.ok) {
      return await res.json();
    }
  } catch {}
  return null;
}

/**
 * Cancels active model download.
 */
export async function cancelModelDownload(): Promise<boolean> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/models/cancel`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
    });
    return res.ok;
  } catch {
    return false;
  }
}

/**
 * Selects active local Whisper model profile.
 */
export async function selectActiveModel(modelName: string): Promise<boolean> {
  try {
    const res = await fetch(`http://127.0.0.1:${activePort}/api/models/select`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ modelName }),
    });
    return res.ok;
  } catch {
    return false;
  }
}

