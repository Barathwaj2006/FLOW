/**
 * FLOW Local HTTP Bridge Client
 * Connects the React UI to the native .NET 9 Host (Flow.Host.Windows) on http://127.0.0.1:5005
 * Enables 100% offline local Whisper speech-to-text inference with AVX2/DirectML acceleration.
 */

import { DictationEntry, DictionaryEntry, HardwareProfile, ModelStatusInfo, ModelDownloadProgress, UpdateStatusInfo, DiagnosticsInfo } from '../types';
import { isNativeShell, sendNativeRequest, onNativeEvent } from './nativeBridge';

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
 * Checks if the local FLOW .NET Host server is running on 127.0.0.1:5005 (or 5006),
 * or directly via Microsoft.Web.WebView2 Native Host IPC.
 */
export async function checkLocalEngineHealth(): Promise<EngineStatus> {
  if (isNativeShell()) {
    try {
      const data = await sendNativeRequest<any>('get-status', null, 2000);
      return {
        connected: true,
        modelInstalled: data?.modelInstalled ?? true,
        modelName: data?.modelName ?? 'ggml-tiny.en.bin',
        engine: data?.engine ?? 'Whisper.net Native Local Engine (DirectML IPC)',
        port: 0,
        offlineSovereignty: data?.offlineSovereignty ?? '100% Offline (Zero Cloud Audio / Native Host IPC)',
      };
    } catch {
      // Fall through to port probe if native bridge did not reply
    }
  }

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
 * Transcribes audio via the local .NET Whisper engine over HTTP or Native IPC.
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
  if (isNativeShell()) {
    try {
      const arrayBuffer = await audioBlob.arrayBuffer();
      const bytes = new Uint8Array(arrayBuffer);
      let binary = '';
      const len = bytes.byteLength;
      for (let i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
      }
      const audioBase64 = btoa(binary);
      const res = await sendNativeRequest<any>('transcribe', { audioBase64, language }, 30000);
      if (res) return res;
    } catch (err) {
      console.warn('[FLOW] Native IPC transcription failed, falling back to HTTP:', err);
    }
  }

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
  if (isNativeShell()) {
    try {
      const items = await sendNativeRequest<DictationEntry[]>('get-history');
      if (items && Array.isArray(items)) {
        return items;
      }
    } catch {}
  }
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
  if (isNativeShell()) {
    try {
      const items = await sendNativeRequest<DictionaryEntry[]>('get-dictionary');
      if (items && Array.isArray(items)) {
        return items;
      }
    } catch {}
  }
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
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<any>('add-dictionary', entry);
      return res?.success !== false;
    } catch {
      return false;
    }
  }
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
  if (isNativeShell()) {
    try {
      const hw = await sendNativeRequest<HardwareProfile>('get-hardware');
      if (hw) return hw;
    } catch {}
  }
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
  if (isNativeShell()) {
    try {
      const models = await sendNativeRequest<ModelStatusInfo[]>('get-models');
      if (models && Array.isArray(models)) {
        return models;
      }
    } catch {}
  }
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
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<any>('download-model', { modelName });
      return res?.success !== false;
    } catch {
      return false;
    }
  }
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
  if (isNativeShell()) {
    try {
      const prog = await sendNativeRequest<ModelDownloadProgress | null>('get-download-progress');
      if (prog) return prog;
    } catch {}
  }
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
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<any>('cancel-download');
      return res?.success !== false;
    } catch {
      return false;
    }
  }
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
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<any>('select-model', { modelName });
      return res?.success !== false;
    } catch {
      return false;
    }
  }
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

export interface SessionStreamCallbacks {
  onState?: (state: string, detail?: string) => void;
  onAudioLevel?: (level: number) => void;
  onPartial?: (text: string) => void;
  onFinal?: (text: string) => void;
  onCompleted?: (text: string) => void;
  onError?: (err: any) => void;
}

/**
 * Subscribes to the native Host's session stream via Native IPC or Server-Sent Events (SSE).
 * Dispatches real-time session state transitions, audio levels, partial transcripts, and final text.
 * Returns an unsubscribe function to close the stream cleanly.
 */
export function subscribeToSessionStream(callbacks: SessionStreamCallbacks): () => void {
  if (isNativeShell()) {
    const unsubs = [
      onNativeEvent('session-state', (payload: any) => {
        callbacks.onState?.(payload?.state ?? '', payload?.detail);
      }),
      onNativeEvent('audio-level', (payload: any) => {
        callbacks.onAudioLevel?.(payload?.level ?? 0);
      }),
      onNativeEvent('partial-transcript', (payload: any) => {
        callbacks.onPartial?.(payload?.text ?? '');
      }),
      onNativeEvent('final-transcript', (payload: any) => {
        callbacks.onFinal?.(payload?.text ?? '');
      }),
      onNativeEvent('session-completed', (payload: any) => {
        callbacks.onCompleted?.(payload?.text ?? '');
      }),
      onNativeEvent('session-error', (payload: any) => {
        callbacks.onError?.(payload?.error);
      }),
    ];

    return () => {
      unsubs.forEach((u) => u());
    };
  }

  let eventSource: EventSource | null = null;
  let isClosed = false;

  const connect = () => {
    if (isClosed) return;
    try {
      eventSource = new EventSource(`http://127.0.0.1:${activePort}/api/session/stream`);

      eventSource.addEventListener('state', (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data);
          callbacks.onState?.(data.state, data.detail);
        } catch {}
      });

      eventSource.addEventListener('audioLevel', (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data);
          callbacks.onAudioLevel?.(data.level);
        } catch {}
      });

      eventSource.addEventListener('partial', (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data);
          callbacks.onPartial?.(data.text);
        } catch {}
      });

      eventSource.addEventListener('final', (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data);
          callbacks.onFinal?.(data.text);
        } catch {}
      });

      eventSource.addEventListener('completed', (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data);
          callbacks.onCompleted?.(data.text);
        } catch {}
      });

      eventSource.onerror = (err) => {
        callbacks.onError?.(err);
        eventSource?.close();
        if (!isClosed) {
          setTimeout(connect, 3000);
        }
      };
    } catch {
      if (!isClosed) {
        setTimeout(connect, 3000);
      }
    }
  };

  connect();

  return () => {
    isClosed = true;
    if (eventSource) {
      eventSource.close();
      eventSource = null;
    }
  };
}

/**
 * Fetches the current application update status from the host.
 */
export async function fetchUpdateStatus(): Promise<UpdateStatusInfo> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<UpdateStatusInfo>('get-update-status');
      if (res) return res;
    } catch {}
  }
  return {
    status: 'Idle',
    currentVersion: '1.0.0',
    downloadProgressPercent: 0,
    isInstalled: false,
  };
}

/**
 * Initiates an on-demand update check.
 */
export async function checkForUpdates(): Promise<UpdateStatusInfo> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<UpdateStatusInfo>('check-update');
      if (res) return res;
    } catch (err: any) {
      return {
        status: 'Failed',
        currentVersion: '1.0.0',
        downloadProgressPercent: 0,
        errorMessage: err?.message || 'Update check failed',
      };
    }
  }
  return {
    status: 'NoUpdateAvailable',
    currentVersion: '1.0.0',
    downloadProgressPercent: 0,
    lastCheckedUtc: new Date().toISOString(),
  };
}

/**
 * Downloads available application update package.
 */
export async function downloadUpdate(): Promise<boolean> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean }>('download-update');
      return res?.success !== false;
    } catch {
      return false;
    }
  }
  return false;
}

/**
 * Applies the downloaded update and restarts the application.
 */
export async function applyUpdateAndRestart(): Promise<boolean> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean }>('apply-update');
      return res?.success !== false;
    } catch {
      return false;
    }
  }
  return false;
}

/**
 * Retrieves diagnostic info, log directory, and crash counts from the host.
 */
export async function fetchDiagnosticsInfo(): Promise<DiagnosticsInfo> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<DiagnosticsInfo>('get-diagnostics-info');
      if (res) return res;
    } catch {}
  }
  return {
    totalCrashCount: 0,
    logsDirectory: '',
    logsTotalSizeBytes: 0,
    crashesDirectory: '',
    enableAnonymousTelemetry: false,
    uptimeSeconds: 0,
  };
}

/**
 * Opens the local logs directory in Windows Explorer.
 */
export async function openLogsFolder(): Promise<boolean> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean }>('open-logs-folder');
      return res?.success ?? false;
    } catch {
      return false;
    }
  }
  return false;
}

/**
 * Exports a redacted diagnostic bundle zip archive.
 */
export async function exportDiagnosticsBundle(): Promise<string | null> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean; exportPath?: string }>('export-diagnostics');
      return res?.exportPath ?? null;
    } catch {
      return null;
    }
  }
  return null;
}

/**
 * Clears old crash dumps and manifests.
 */
export async function clearCrashReports(): Promise<boolean> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean }>('clear-crash-reports');
      return res?.success ?? false;
    } catch {
      return false;
    }
  }
  return false;
}

/**
 * Sets anonymous diagnostic telemetry opt-in preference.
 */
export async function setTelemetryOptIn(enabled: boolean): Promise<boolean> {
  if (isNativeShell()) {
    try {
      const res = await sendNativeRequest<{ success: boolean }>('set-telemetry-opt-in', { enabled });
      return res?.success ?? false;
    } catch {
      return false;
    }
  }
  return false;
}


