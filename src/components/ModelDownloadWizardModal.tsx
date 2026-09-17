import React, { useState, useEffect, useRef } from 'react';
import { HardwareProfile, ModelStatusInfo, ModelDownloadProgress } from '../types';
import {
  fetchHardwareProfile,
  fetchModelsList,
  startModelDownload,
  fetchDownloadProgress,
  cancelModelDownload,
  selectActiveModel,
} from '../lib/flowApiClient';

interface ModelDownloadWizardModalProps {
  isOpen: boolean;
  onClose: () => void;
  onModelReady?: (modelName: string) => void;
}

export const ModelDownloadWizardModal: React.FC<ModelDownloadWizardModalProps> = ({
  isOpen,
  onClose,
  onModelReady,
}) => {
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
  const [hardware, setHardware] = useState<HardwareProfile | null>(null);
  const [models, setModels] = useState<ModelStatusInfo[]>([]);
  const [selectedModel, setSelectedModel] = useState<string>('ggml-tiny.en.bin');
  const [downloadProgress, setDownloadProgress] = useState<ModelDownloadProgress | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [isScanningHardware, setIsScanningHardware] = useState(false);

  const pollIntervalRef = useRef<any>(null);

  // Load hardware & models when opened
  useEffect(() => {
    if (!isOpen) return;

    const loadData = async () => {
      setIsScanningHardware(true);
      try {
        const [hw, mdls] = await Promise.all([
          fetchHardwareProfile(),
          fetchModelsList(),
        ]);
        setHardware(hw);
        if (mdls.length > 0) {
          setModels(mdls);
          // Set recommended default based on hardware
          if (hw && (hw.isDiscreteGpu || hw.dedicatedVramMB >= 4096)) {
            const base = mdls.find(m => m.name === 'ggml-base.en.bin');
            if (base) setSelectedModel(base.name);
          } else {
            const tiny = mdls.find(m => m.name === 'ggml-tiny.en.bin');
            if (tiny) setSelectedModel(tiny.name);
          }
        }
      } finally {
        setIsScanningHardware(false);
      }
    };

    loadData();

    return () => {
      if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
    };
  }, [isOpen]);

  // Clean up polling on unmount
  useEffect(() => {
    return () => {
      if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
    };
  }, []);

  const handleStartDownload = async () => {
    setDownloadError(null);
    setIsDownloading(true);
    setStep(3);

    const success = await startModelDownload(selectedModel);
    if (!success) {
      setDownloadError('Failed to initiate model download. Please ensure FLOW Host is running.');
      setIsDownloading(false);
      return;
    }

    // Poll download progress every 350ms
    pollIntervalRef.current = setInterval(async () => {
      const prog = await fetchDownloadProgress();
      if (!prog) return;

      setDownloadProgress(prog);

      if (prog.status === 'Ready' || (prog.percent >= 100 && !prog.isActive)) {
        clearInterval(pollIntervalRef.current);
        setIsDownloading(false);
        await selectActiveModel(selectedModel);
        setStep(4);
        onModelReady?.(selectedModel);
      } else if (prog.status === 'Failed' || prog.errorMessage) {
        clearInterval(pollIntervalRef.current);
        setIsDownloading(false);
        setDownloadError(prog.errorMessage || 'Model download failed or checksum mismatch occurred.');
      }
    }, 350);
  };

  const handleCancel = async () => {
    if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
    await cancelModelDownload();
    setIsDownloading(false);
    setDownloadProgress(null);
    setStep(2);
  };

  if (!isOpen) return null;

  const currentModelInfo = models.find(m => m.name === selectedModel);

  return (
    <div
      id="flow-model-wizard-overlay"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200"
    >
      <div
        id="flow-model-wizard-dialog"
        className="w-full max-w-2xl bg-white rounded-3xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col transition-all duration-300"
      >
        {/* Header */}
        <div className="px-8 pt-6 pb-4 flex items-center justify-between border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-xl bg-[#0284c7] flex items-center justify-center text-white shadow-xs">
              <span className="material-symbols-outlined text-[19px]">neurology</span>
            </div>
            <div>
              <h2 className="font-bold text-sm text-slate-900 tracking-tight">Whisper Neural Model Setup</h2>
              <p className="text-[11px] text-slate-400">100% Offline Speech Recognition Engine</p>
            </div>
          </div>

          {/* Stepper */}
          <div className="flex items-center gap-2">
            {[1, 2, 3, 4].map(s => (
              <div
                key={s}
                className={`h-1.5 rounded-full transition-all duration-300 ${
                  step === s
                    ? 'w-8 bg-[#0284c7]'
                    : step > s
                    ? 'w-3 bg-emerald-500'
                    : 'w-3 bg-slate-200'
                }`}
              />
            ))}
          </div>

          {!isDownloading && (
            <button
              onClick={onClose}
              className="w-7 h-7 rounded-full text-slate-400 hover:text-slate-600 hover:bg-slate-100 flex items-center justify-center transition"
              title="Close"
            >
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          )}
        </div>

        {/* Body */}
        <div className="p-8 flex-1">
          {/* STEP 1: Hardware Scan */}
          {step === 1 && (
            <div className="flex flex-col space-y-6 animate-in fade-in slide-in-from-right-4 duration-200">
              <div className="space-y-1">
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 1 of 4</span>
                <h3 className="text-xl font-bold text-slate-900">Hardware Acceleration Probe</h3>
                <p className="text-xs text-slate-500">
                  FLOW inspected your graphics adapters and CPU intrinsics to select the fastest inference pipeline.
                </p>
              </div>

              {isScanningHardware ? (
                <div className="p-8 rounded-2xl bg-slate-50 border border-slate-200 flex flex-col items-center justify-center gap-3">
                  <div className="w-8 h-8 border-3 border-[#0284c7] border-t-transparent rounded-full animate-spin"></div>
                  <span className="text-xs font-semibold text-slate-600">Probing Windows Display Adapters &amp; SIMD extensions...</span>
                </div>
              ) : hardware ? (
                <div className="space-y-3">
                  <div className="p-4 rounded-2xl bg-sky-50/70 border border-sky-200 space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="material-symbols-outlined text-[#0284c7] text-[20px]">
                          {hardware.isDiscreteGpu ? 'developer_board' : 'memory'}
                        </span>
                        <span className="text-xs font-bold text-slate-900">{hardware.primaryGpuName}</span>
                      </div>
                      <span className="px-2 py-0.5 rounded bg-sky-100 text-[#0284c7] text-[10px] font-bold uppercase">
                        {hardware.recommendedBackend.replace('_', ' ')}
                      </span>
                    </div>
                    <div className="grid grid-cols-3 gap-2 pt-1 text-[11px] text-slate-600">
                      <div>
                        <span className="text-slate-400 block">Dedicated VRAM:</span>
                        <span className="font-semibold text-slate-800">
                          {hardware.dedicatedVramMB > 0 ? `${hardware.dedicatedVramMB} MB` : 'Shared System RAM'}
                        </span>
                      </div>
                      <div>
                        <span className="text-slate-400 block">DirectML GPU:</span>
                        <span className="font-semibold text-emerald-600">
                          {hardware.directMLSupported ? 'Supported' : 'CPU Mode'}
                        </span>
                      </div>
                      <div>
                        <span className="text-slate-400 block">Optimal Threads:</span>
                        <span className="font-semibold text-slate-800">{hardware.optimalCpuThreads} Worker Cores</span>
                      </div>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-xs">
                    <div className="p-3 rounded-xl bg-slate-50 border border-slate-200 flex items-center gap-2.5">
                      <span className="material-symbols-outlined text-emerald-600 text-[18px]">verified</span>
                      <div>
                        <span className="font-semibold text-slate-800 block">Offline Sovereignty</span>
                        <span className="text-[10px] text-slate-500">Zero audio leaves this PC</span>
                      </div>
                    </div>
                    <div className="p-3 rounded-xl bg-slate-50 border border-slate-200 flex items-center gap-2.5">
                      <span className="material-symbols-outlined text-[#0284c7] text-[18px]">bolt</span>
                      <div>
                        <span className="font-semibold text-slate-800 block">Inference Engine</span>
                        <span className="text-[10px] text-slate-500">Native whisper.cpp with AVX2</span>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="p-4 rounded-xl bg-amber-50 border border-amber-200 text-xs text-amber-800">
                  Hardware probe fallback: Using CPU SIMD AVX2 acceleration.
                </div>
              )}

              <div className="flex justify-end pt-2">
                <button
                  onClick={() => setStep(2)}
                  className="h-11 px-6 rounded-xl bg-[#0284c7] hover:bg-[#0369a1] text-white font-semibold text-xs shadow-md transition flex items-center gap-2"
                >
                  <span>Select Speech Model</span>
                  <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                </button>
              </div>
            </div>
          )}

          {/* STEP 2: Model Selection */}
          {step === 2 && (
            <div className="flex flex-col space-y-5 animate-in fade-in slide-in-from-right-4 duration-200">
              <div className="space-y-1">
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 2 of 4</span>
                <h3 className="text-xl font-bold text-slate-900">Select Whisper Model</h3>
                <p className="text-xs text-slate-500">
                  Select a pre-trained GGML neural model. Models download once and run locally forever.
                </p>
              </div>

              <div className="space-y-2.5 max-h-[300px] overflow-y-auto pr-1">
                {models.map(m => {
                  const isSelected = selectedModel === m.name;
                  return (
                    <div
                      key={m.name}
                      onClick={() => setSelectedModel(m.name)}
                      className={`p-3.5 rounded-2xl border cursor-pointer transition flex items-center justify-between gap-3 ${
                        isSelected
                          ? 'bg-sky-50/80 border-[#0284c7] ring-1 ring-[#0284c7] shadow-xs'
                          : 'bg-slate-50/60 border-slate-200 hover:bg-slate-100/70'
                      }`}
                    >
                      <div className="flex items-start gap-3">
                        <div
                          className={`w-5 h-5 rounded-full border-2 mt-0.5 flex items-center justify-center transition ${
                            isSelected ? 'border-[#0284c7] bg-[#0284c7]' : 'border-slate-300'
                          }`}
                        >
                          {isSelected && <div className="w-2 h-2 rounded-full bg-white"></div>}
                        </div>
                        <div className="space-y-0.5">
                          <div className="flex items-center gap-2">
                            <span className="text-xs font-bold text-slate-900">{m.displayName}</span>
                            <span className="text-[10px] px-1.5 py-0.2 rounded bg-slate-200 text-slate-700 font-mono">
                              {m.sizeMB} MB
                            </span>
                            {m.isInstalled && (
                              <span className="px-1.5 py-0.2 rounded bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                                Installed
                              </span>
                            )}
                          </div>
                          <p className="text-[11px] text-slate-500 leading-normal">
                            {m.name === 'ggml-tiny.en.bin' && 'Ultra-low latency (~150ms). Lowest memory usage. Ideal for fast English dictation.'}
                            {m.name === 'ggml-tiny.bin' && 'Multi-language support for 99 languages + auto-detection + code-switching.'}
                            {m.name === 'ggml-base.en.bin' && 'Recommended for modern laptops: High English accuracy with crisp punctuation.'}
                            {m.name === 'ggml-small.bin' && 'Maximum accuracy across accents and noisy environments. Requires ~1GB RAM.'}
                          </p>
                        </div>
                      </div>

                      <div className="shrink-0 text-right">
                        <span className="text-[10px] font-mono text-slate-400 block">SHA-256 Verified</span>
                        <span className="text-[10px] font-mono text-slate-500 block truncate max-w-[90px]" title={m.sha256}>
                          {m.sha256.substring(0, 10)}...
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="flex items-center justify-between pt-2">
                <button
                  onClick={() => setStep(1)}
                  className="h-11 px-5 rounded-xl border border-slate-200 text-slate-700 text-xs font-medium hover:bg-slate-50 transition"
                >
                  Back
                </button>
                <button
                  onClick={handleStartDownload}
                  className="h-11 px-6 rounded-xl bg-[#0284c7] hover:bg-[#0369a1] text-white font-semibold text-xs shadow-md transition flex items-center gap-2"
                >
                  <span className="material-symbols-outlined text-[18px]">download</span>
                  <span>{currentModelInfo?.isInstalled ? 'Verify & Activate Model' : 'Download Model'}</span>
                </button>
              </div>
            </div>
          )}

          {/* STEP 3: Download & Verification */}
          {step === 3 && (
            <div className="flex flex-col space-y-6 animate-in fade-in slide-in-from-right-4 duration-200">
              <div className="space-y-1">
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 3 of 4</span>
                <h3 className="text-xl font-bold text-slate-900">Downloading Model Weights</h3>
                <p className="text-xs text-slate-500">
                  Streaming {currentModelInfo?.displayName} with resumable HTTP range chunks and SHA-256 hashing.
                </p>
              </div>

              {/* Progress Card */}
              <div className="p-6 rounded-2xl bg-slate-50 border border-slate-200 space-y-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="w-2.5 h-2.5 rounded-full bg-[#0284c7] animate-ping"></span>
                    <span className="text-xs font-bold text-slate-800">
                      {downloadProgress?.status || 'Initiating connection...'}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 font-mono text-xs">
                    {downloadProgress && downloadProgress.speedMBps > 0 && (
                      <span className="px-2 py-0.5 rounded bg-sky-100 text-[#0284c7] font-semibold">
                        ⚡ {downloadProgress.speedMBps} MB/s
                      </span>
                    )}
                    <span className="font-bold text-[#0284c7]">
                      {downloadProgress?.percent ?? 0}%
                    </span>
                  </div>
                </div>

                {/* Progress Bar */}
                <div className="h-3 w-full bg-slate-200 rounded-full overflow-hidden p-0.5">
                  <div
                    className="h-full rounded-full transition-all duration-200 bg-gradient-to-r from-[#0284c7] to-sky-400"
                    style={{ width: `${Math.max(2, downloadProgress?.percent ?? 0)}%` }}
                  />
                </div>

                <div className="flex items-center justify-between text-[11px] text-slate-500">
                  <span>
                    {downloadProgress && downloadProgress.bytesDownloaded > 0
                      ? `${(downloadProgress.bytesDownloaded / (1024 * 1024)).toFixed(1)} MB / ${(downloadProgress.totalBytes / (1024 * 1024)).toFixed(1)} MB`
                      : 'Connecting to Hugging Face CDN...'}
                  </span>
                  <span className="flex items-center gap-1 font-mono text-[10px]">
                    <span className="material-symbols-outlined text-[13px] text-emerald-600">lock</span>
                    <span>Continuous SHA-256</span>
                  </span>
                </div>
              </div>

              {downloadError && (
                <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-xs text-rose-800 space-y-1">
                  <div className="font-bold flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[16px]">error</span>
                    <span>Download Failed</span>
                  </div>
                  <p>{downloadError}</p>
                </div>
              )}

              <div className="flex items-center justify-between pt-2">
                <button
                  onClick={handleCancel}
                  className="h-11 px-5 rounded-xl border border-rose-200 text-rose-600 hover:bg-rose-50 text-xs font-semibold transition"
                >
                  Cancel Download
                </button>
                <span className="text-[11px] text-slate-400 italic">
                  Downloads resume automatically if interrupted.
                </span>
              </div>
            </div>
          )}

          {/* STEP 4: Ready */}
          {step === 4 && (
            <div className="flex flex-col items-center text-center space-y-6 animate-in fade-in slide-in-from-right-4 duration-200 py-2">
              <div className="w-20 h-20 rounded-3xl bg-emerald-50 border border-emerald-200 flex items-center justify-center text-emerald-600 shadow-inner">
                <span className="material-symbols-outlined text-[44px]">verified</span>
              </div>

              <div className="space-y-1 max-w-md">
                <span className="text-xs font-bold uppercase tracking-widest text-emerald-600">
                  Integrity Verified &amp; Active
                </span>
                <h3 className="text-2xl font-extrabold text-slate-900">Speech Model Ready</h3>
                <p className="text-xs text-slate-500 leading-relaxed pt-1">
                  <strong>{currentModelInfo?.displayName}</strong> has passed 100% SHA-256 cryptographic verification and is pre-loaded for offline local dictation.
                </p>
              </div>

              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 w-full text-left font-mono text-[11px] text-slate-600 space-y-1">
                <div className="flex justify-between">
                  <span className="text-slate-400">File:</span>
                  <span className="font-semibold text-slate-800">{selectedModel}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">SHA-256 Fingerprint:</span>
                  <span className="text-emerald-700 font-semibold">{currentModelInfo?.sha256.substring(0, 16)}...</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Engine Backend:</span>
                  <span className="text-[#0284c7] font-semibold">{hardware?.recommendedBackend ?? 'DirectML_GPU'}</span>
                </div>
              </div>

              <button
                onClick={onClose}
                className="w-full h-11 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-md transition flex items-center justify-center gap-2"
              >
                <span className="material-symbols-outlined text-[18px]">check</span>
                <span>Start Dictating with FLOW</span>
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
