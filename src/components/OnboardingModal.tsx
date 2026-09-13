import React, { useState, useEffect, useRef } from 'react';

interface OnboardingModalProps {
  isOpen: boolean;
  onClose: () => void;
  onFinish: () => void;
  activeMic: string;
  onSelectMic: (mic: string) => void;
  onStartTestDictation: () => void;
  onStopTestDictation: () => void;
  isListening: boolean;
  audioLevel: number;
  previewText: string;
}

export const OnboardingModal: React.FC<OnboardingModalProps> = ({
  isOpen,
  onClose,
  onFinish,
  activeMic,
  onSelectMic,
  onStartTestDictation,
  onStopTestDictation,
  isListening,
  audioLevel,
  previewText,
}) => {
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
  const [availableDevices, setAvailableDevices] = useState<string[]>([]);
  const [shortcutTestTriggered, setShortcutTestTriggered] = useState<string | null>(null);
  const [testSpeechResult, setTestSpeechResult] = useState<string>('');

  // Enumerate actual connected audio input devices
  useEffect(() => {
    if (!isOpen) return;
    const fetchDevices = async () => {
      try {
        if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
          const devices = await navigator.mediaDevices.enumerateDevices();
          const audioInputs = devices
            .filter(d => d.kind === 'audioinput')
            .map((d, i) => d.label || `Microphone ${i + 1} (WASAPI)`);
          
          if (audioInputs.length > 0) {
            setAvailableDevices(audioInputs);
            if (!activeMic || activeMic === 'Default Microphone (WASAPI)') {
              onSelectMic(audioInputs[0]);
            }
          } else {
            setAvailableDevices([
              'Built-in Microphone (WASAPI)',
              'Headset Microphone',
              'Realtek High Definition Audio',
            ]);
          }
        }
      } catch {
        setAvailableDevices([
          'Built-in Microphone (WASAPI)',
          'Headset Microphone',
          'Realtek High Definition Audio',
        ]);
      }
    };
    fetchDevices();
  }, [isOpen]);

  // Listen for shortcuts during step 3 to give immediate feedback
  useEffect(() => {
    if (!isOpen || step !== 3) return;

    const handleKey = (e: KeyboardEvent) => {
      if (e.altKey && (e.code === 'Space' || e.key === ' ')) {
        e.preventDefault();
        setShortcutTestTriggered('Alt + Space (Hold-to-Talk Detected)');
        setTimeout(() => setShortcutTestTriggered(null), 2500);
      } else if (e.altKey && (e.code === 'KeyB' || e.key.toLowerCase() === 'b')) {
        e.preventDefault();
        setShortcutTestTriggered('Alt + B (Toggle Recording Detected)');
        setTimeout(() => setShortcutTestTriggered(null), 2500);
      }
    };

    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [isOpen, step]);

  // Update test speech result when dictation produces text
  useEffect(() => {
    if (previewText) {
      setTestSpeechResult(previewText);
    }
  }, [previewText]);

  if (!isOpen) return null;

  return (
    <div
      id="flow-onboarding-overlay"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200"
    >
      <div
        id="flow-onboarding-dialog"
        className="w-full max-w-xl bg-white rounded-3xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col transition-all duration-300"
      >
        {/* Progress Dots Header */}
        <div className="px-8 pt-6 pb-2 flex items-center justify-between border-b border-slate-100">
          <div className="flex items-center gap-2.5">
            <div className="w-7 h-7 rounded-lg bg-[#0284c7] flex items-center justify-center text-white shadow-xs">
              <span className="material-symbols-outlined text-[17px]">mic</span>
            </div>
            <span className="font-bold text-sm text-slate-900 tracking-tight">FLOW Setup</span>
          </div>

          {/* 4-Step Indicator */}
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

          <button
            onClick={onClose}
            className="w-7 h-7 rounded-full text-slate-400 hover:text-slate-600 hover:bg-slate-100 flex items-center justify-center transition"
            title="Skip Setup"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>

        {/* Content Body */}
        <div className="p-8 flex-1">
          {/* STEP 1: Welcome */}
          {step === 1 && (
            <div className="flex flex-col items-center text-center space-y-6 animate-in fade-in slide-in-from-right-4 duration-200">
              <div className="w-20 h-20 rounded-3xl bg-[#0284c7]/10 border border-[#0284c7]/20 flex items-center justify-center text-[#0284c7] shadow-inner">
                <span className="material-symbols-outlined text-[44px]">graphic_eq</span>
              </div>

              <div className="space-y-2 max-w-md">
                <span className="text-xs font-bold uppercase tracking-widest text-[#0284c7]">
                  Windows Native Voice Productivity
                </span>
                <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">
                  FLOW
                </h1>
                <p className="text-base font-semibold text-slate-700">
                  Your voice, everywhere.
                </p>
                <p className="text-xs text-slate-500 leading-relaxed pt-1">
                  Dictate at 3x typing speed directly into any Windows application—Slack, VS Code, Word, Chrome, or Teams—with zero cloud audio transmission.
                </p>
              </div>

              <div className="grid grid-cols-3 gap-3 w-full pt-2">
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 flex flex-col items-center text-center gap-1">
                  <span className="material-symbols-outlined text-[20px] text-emerald-600">lock</span>
                  <span className="text-xs font-bold text-slate-800">100% Offline</span>
                  <span className="text-[10px] text-slate-500">Audio stays on device</span>
                </div>
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 flex flex-col items-center text-center gap-1">
                  <span className="material-symbols-outlined text-[20px] text-[#0284c7]">bolt</span>
                  <span className="text-xs font-bold text-slate-800">&lt;200ms Latency</span>
                  <span className="text-[10px] text-slate-500">Instant local inference</span>
                </div>
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 flex flex-col items-center text-center gap-1">
                  <span className="material-symbols-outlined text-[20px] text-amber-600">shield</span>
                  <span className="text-xs font-bold text-slate-800">Zero Enter</span>
                  <span className="text-[10px] text-slate-500">Never sends messages</span>
                </div>
              </div>

              <button
                id="onboarding-step1-btn"
                onClick={() => setStep(2)}
                className="w-full h-11 rounded-xl bg-[#0284c7] hover:bg-[#0369a1] text-white font-semibold text-sm shadow-md transition-all flex items-center justify-center gap-2 hover:gap-3"
              >
                <span>Get Started</span>
                <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
              </button>
            </div>
          )}

          {/* STEP 2: Microphone Setup */}
          {step === 2 && (
            <div className="flex flex-col space-y-6 animate-in fade-in slide-in-from-right-4 duration-200">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 2 of 4</span>
                <h2 className="text-xl font-bold text-slate-900 mt-1">Configure Microphone</h2>
                <p className="text-xs text-slate-500 mt-1">
                  Select your physical audio input device and verify live voice energy.
                </p>
              </div>

              {/* Device Selector */}
              <div className="space-y-2">
                <label className="block text-xs font-medium text-slate-700">Audio Input Device</label>
                <div className="relative">
                  <select
                    value={activeMic}
                    onChange={e => onSelectMic(e.target.value)}
                    className="w-full h-11 bg-slate-50 border border-slate-200 rounded-xl px-3.5 pr-10 text-xs font-medium text-slate-900 focus:outline-none focus:border-[#0284c7] appearance-none cursor-pointer"
                  >
                    {availableDevices.map(d => (
                      <option key={d} value={d}>
                        {d}
                      </option>
                    ))}
                  </select>
                  <span className="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px] pointer-events-none">
                    expand_more
                  </span>
                </div>
              </div>

              {/* Live Audio Energy Meter */}
              <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className={`w-2.5 h-2.5 rounded-full ${audioLevel > 15 ? 'bg-emerald-500 animate-ping' : 'bg-slate-300'}`}></span>
                    <span className="text-xs font-bold text-slate-800">Physical Microphone Signal</span>
                  </div>
                  <span className="font-mono text-xs font-semibold text-[#0284c7]">{audioLevel}%</span>
                </div>

                <div className="h-3 w-full bg-slate-200 rounded-full overflow-hidden p-0.5">
                  <div
                    className="h-full rounded-full transition-all duration-75 bg-gradient-to-r from-[#0284c7] via-emerald-500 to-amber-500"
                    style={{ width: `${Math.max(5, audioLevel)}%` }}
                  />
                </div>

                <p className="text-[11px] text-slate-500">
                  Speak out loud: the bar should illuminate in response to your voice.
                </p>
              </div>

              {/* Navigation Buttons */}
              <div className="flex items-center gap-3 pt-2">
                <button
                  onClick={() => setStep(1)}
                  className="h-11 px-5 rounded-xl border border-slate-200 text-slate-700 text-xs font-medium hover:bg-slate-50 transition"
                >
                  Back
                </button>
                <button
                  id="onboarding-step2-btn"
                  onClick={() => setStep(3)}
                  className="flex-1 h-11 rounded-xl bg-[#0284c7] hover:bg-[#0369a1] text-white font-semibold text-xs shadow-md transition flex items-center justify-center gap-2"
                >
                  <span>Continue</span>
                  <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                </button>
              </div>
            </div>
          )}

          {/* STEP 3: Shortcuts */}
          {step === 3 && (
            <div className="flex flex-col space-y-6 animate-in fade-in slide-in-from-right-4 duration-200">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 3 of 4</span>
                <h2 className="text-xl font-bold text-slate-900 mt-1">Global Voice Shortcuts</h2>
                <p className="text-xs text-slate-500 mt-1">
                  FLOW works in the background. Memorize these two primary Windows shortcuts:
                </p>
              </div>

              <div className="space-y-3">
                {/* Hold to Talk */}
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 flex items-start justify-between gap-4">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-bold text-slate-900">Primary Hold-to-Talk</span>
                      <span className="px-2 py-0.5 rounded bg-sky-100 text-[#0284c7] text-[10px] font-bold">Default</span>
                    </div>
                    <p className="text-xs text-slate-500 leading-normal">
                      Hold while speaking. As soon as you release the keys, FLOW automatically formats and inserts your words.
                    </p>
                  </div>
                  <kbd className="px-3 py-1.5 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-xs whitespace-nowrap shrink-0">
                    Alt + Space
                  </kbd>
                </div>

                {/* Toggle Recording */}
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 flex items-start justify-between gap-4">
                  <div className="space-y-1">
                    <span className="text-xs font-bold text-slate-900">Toggle Mode (Hands-Free)</span>
                    <p className="text-xs text-slate-500 leading-normal">
                      Press once to start recording; press again when you finish speaking.
                    </p>
                  </div>
                  <kbd className="px-3 py-1.5 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-xs whitespace-nowrap shrink-0">
                    Alt + B
                  </kbd>
                </div>
              </div>

              {/* Interactive Keystroke Feedback */}
              <div className="p-3.5 rounded-xl bg-amber-50/70 border border-amber-200 flex items-center justify-between text-xs text-amber-900">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-amber-600 text-[18px]">keyboard</span>
                  <span>{shortcutTestTriggered || 'Try pressing Alt + Space or Alt + B now'}</span>
                </div>
                {shortcutTestTriggered && (
                  <span className="material-symbols-outlined text-emerald-600 text-[18px]">check_circle</span>
                )}
              </div>

              {/* Navigation */}
              <div className="flex items-center gap-3 pt-2">
                <button
                  onClick={() => setStep(2)}
                  className="h-11 px-5 rounded-xl border border-slate-200 text-slate-700 text-xs font-medium hover:bg-slate-50 transition"
                >
                  Back
                </button>
                <button
                  id="onboarding-step3-btn"
                  onClick={() => setStep(4)}
                  className="flex-1 h-11 rounded-xl bg-[#0284c7] hover:bg-[#0369a1] text-white font-semibold text-xs shadow-md transition flex items-center justify-center gap-2"
                >
                  <span>Continue</span>
                  <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                </button>
              </div>
            </div>
          )}

          {/* STEP 4: Try FLOW */}
          {step === 4 && (
            <div className="flex flex-col space-y-5 animate-in fade-in slide-in-from-right-4 duration-200">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-[#0284c7]">Step 4 of 4</span>
                <h2 className="text-xl font-bold text-slate-900 mt-1">Try FLOW Live</h2>
                <p className="text-xs text-slate-500 mt-1">
                  Hold <kbd className="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-300 font-mono text-[11px] text-slate-700">Alt + Space</kbd> or click the microphone below and speak a sentence.
                </p>
              </div>

              {/* Live Interactive Microphone Button */}
              <div className="flex flex-col items-center justify-center p-5 rounded-2xl bg-slate-50 border border-slate-200 gap-3">
                <button
                  onClick={() => {
                    if (isListening) onStopTestDictation();
                    else onStartTestDictation();
                  }}
                  className={`w-16 h-16 rounded-full flex items-center justify-center transition-all duration-200 shadow-md ${
                    isListening
                      ? 'bg-rose-600 hover:bg-rose-700 text-white scale-105 shadow-rose-600/40 animate-pulse'
                      : 'bg-[#0284c7] hover:bg-[#0369a1] text-white shadow-[#0284c7]/30 hover:scale-102 active:scale-95'
                  }`}
                  title={isListening ? 'Click to Stop' : 'Click to Speak'}
                >
                  <span className="material-symbols-outlined text-[32px]">
                    {isListening ? 'mic_off' : 'mic'}
                  </span>
                </button>

                <div className="flex flex-col items-center text-center">
                  <span className="text-xs font-bold text-slate-800">
                    {isListening ? 'Listening to your voice...' : 'Click to test your voice'}
                  </span>
                  <span className="text-[11px] text-slate-500">
                    {isListening ? 'Say: "FLOW is transcribing my speech smoothly."' : 'Or hold Alt + Space'}
                  </span>
                </div>

                {isListening && (
                  <div className="w-32 bg-slate-200 h-2 rounded-full overflow-hidden">
                    <div
                      className="bg-[#0284c7] h-full transition-all duration-75"
                      style={{ width: `${Math.max(15, audioLevel)}%` }}
                    />
                  </div>
                )}
              </div>

              {/* Result Preview Box */}
              <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-inner min-h-[70px] flex items-center">
                {testSpeechResult ? (
                  <div className="space-y-1 w-full">
                    <div className="flex items-center gap-1.5 text-[11px] font-semibold text-emerald-700">
                      <span className="material-symbols-outlined text-[14px]">check_circle</span>
                      <span>Realtime Transcript Captured:</span>
                    </div>
                    <p className="text-xs font-mono text-slate-800 font-medium select-text">
                      "{testSpeechResult}"
                    </p>
                  </div>
                ) : (
                  <p className="text-xs text-slate-400 italic text-center w-full">
                    Your transcribed words will appear here...
                  </p>
                )}
              </div>

              {/* Finish Setup */}
              <div className="flex items-center gap-3 pt-2">
                <button
                  onClick={() => setStep(3)}
                  className="h-11 px-5 rounded-xl border border-slate-200 text-slate-700 text-xs font-medium hover:bg-slate-50 transition"
                >
                  Back
                </button>
                <button
                  id="onboarding-finish-btn"
                  onClick={onFinish}
                  className="flex-1 h-11 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-md transition flex items-center justify-center gap-2"
                >
                  <span className="material-symbols-outlined text-[18px]">check</span>
                  <span>You're Ready — Launch FLOW</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
