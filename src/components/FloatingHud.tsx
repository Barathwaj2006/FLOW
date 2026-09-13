import React, { useState, useEffect, useRef } from 'react';
import { DictationEntry, SessionState } from '../types';

interface DynamicSvgWaveProps {
  audioLevel: number; // 0 - 100
  isListening: boolean;
  className?: string;
}

/**
 * High-performance, organic SVG soundwave visualization for the FLOW Obsidian Amber HUD.
 * Renders multi-harmonic undulating sine waves and reactive audio bars.
 * Waves are always alive and breathing when mic is active, surging dynamically when speech occurs.
 */
export const DynamicSvgWave: React.FC<DynamicSvgWaveProps> = ({
  audioLevel,
  isListening,
  className = '',
}) => {
  const [paths, setPaths] = useState<{
    wave1: string;
    wave2: string;
    wave3: string;
    fill1: string;
  }>({
    wave1: '',
    wave2: '',
    wave3: '',
    fill1: '',
  });

  const [ribs, setRibs] = useState<Array<{ x: number; height: number; opacity: number }>>([]);

  const phaseRef = useRef(0);
  const audioLevelRef = useRef(audioLevel);
  const smoothedLevelRef = useRef(audioLevel);
  const animFrameRef = useRef<number | null>(null);

  useEffect(() => {
    audioLevelRef.current = Math.max(0, Math.min(100, audioLevel));
  }, [audioLevel]);

  useEffect(() => {
    const width = 160;
    const height = 26;
    const midY = height / 2;

    if (!isListening) {
      const resting = `M 0 ${midY} Q ${width / 2} ${midY} ${width} ${midY}`;
      setPaths({
        wave1: resting,
        wave2: resting,
        wave3: resting,
        fill1: `M 0 ${midY} L ${width} ${midY} L ${width} ${height} L 0 ${height} Z`,
      });
      setRibs([]);
      return;
    }

    const pointCount = 48;
    const dx = width / pointCount;

    const animate = () => {
      // Smooth interpolation towards incoming audio level
      smoothedLevelRef.current += (audioLevelRef.current - smoothedLevelRef.current) * 0.22;
      const currentLevel = smoothedLevelRef.current;

      // Base ambient wave motion: always gently breathing even during pauses
      const ambientSpeed = 0.042;
      const speechSpeed = (currentLevel / 100) * 0.12;
      phaseRef.current += (ambientSpeed + speechSpeed);
      const phase = phaseRef.current;

      // Amplitude: baseline breathing wave (2.4px to 3.2px) + dynamic speech expansion (up to ~10px)
      const ambientAmp = 2.5 + Math.sin(phase * 0.75) * 0.7;
      const speechAmp = (currentLevel / 100) * 9.8;
      const baseAmp = ambientAmp + speechAmp;

      let d1 = '';
      let d2 = '';
      let d3 = '';

      for (let i = 0; i <= pointCount; i++) {
        const x = i * dx;
        // Smooth sine envelope so waves taper into zero smoothly at the left/right boundaries
        const envelope = Math.sin((i / pointCount) * Math.PI);

        // Wave 1: Primary energetic crest (amber/gold)
        const y1 = midY + Math.sin(x * 0.082 + phase * 1.25) * (baseAmp * envelope);
        // Wave 2: Harmonic counter-wave (champagne/warm peach)
        const y2 = midY + Math.sin(x * 0.125 - phase * 1.4 + 1.2) * (baseAmp * 0.76 * envelope);
        // Wave 3: Ambient resonant sub-wave (subtle bronze)
        const y3 = midY + Math.sin(x * 0.048 + phase * 0.7 + 2.5) * (baseAmp * 0.44 * envelope);

        const xStr = x.toFixed(1);
        if (i === 0) {
          d1 = `M ${xStr} ${y1.toFixed(1)}`;
          d2 = `M ${xStr} ${y2.toFixed(1)}`;
          d3 = `M ${xStr} ${y3.toFixed(1)}`;
        } else {
          d1 += ` L ${xStr} ${y1.toFixed(1)}`;
          d2 += ` L ${xStr} ${y2.toFixed(1)}`;
          d3 += ` L ${xStr} ${y3.toFixed(1)}`;
        }
      }

      const fill1 = `${d1} L ${width} ${height} L 0 ${height} Z`;

      // 7 Dynamic voice frequency ribs across the center wave
      const newRibs = [];
      const ribCount = 7;
      const startX = 48;
      const spacing = 11;
      for (let b = 0; b < ribCount; b++) {
        const bx = startX + b * spacing;
        const ribNorm = Math.sin((b / (ribCount - 1)) * Math.PI);
        const ribHeight = Math.max(
          2.5,
          (ambientAmp * 0.7 + (currentLevel / 100) * 14) * ribNorm * (0.75 + 0.25 * Math.sin(phase * 2.2 + b))
        );
        const ribOpacity = Math.min(0.85, 0.25 + (currentLevel / 100) * 0.55);
        newRibs.push({ x: bx, height: ribHeight, opacity: ribOpacity });
      }

      setPaths({
        wave1: d1,
        wave2: d2,
        wave3: d3,
        fill1,
      });
      setRibs(newRibs);

      animFrameRef.current = requestAnimationFrame(animate);
    };

    animFrameRef.current = requestAnimationFrame(animate);

    return () => {
      if (animFrameRef.current) {
        cancelAnimationFrame(animFrameRef.current);
      }
    };
  }, [isListening]);

  return (
    <div className={`relative flex items-center justify-center ${className}`}>
      <svg
        id="flow-dynamic-svg-wave"
        className="w-[130px] sm:w-[155px] h-[24px] overflow-visible select-none"
        viewBox="0 0 160 26"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <linearGradient id="flowAmberWaveGradient" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#8B5E3C" stopOpacity="0.35" />
            <stop offset="25%" stopColor="#B87333" stopOpacity="0.9" />
            <stop offset="50%" stopColor="#FFE0B8" stopOpacity="1" />
            <stop offset="75%" stopColor="#B87333" stopOpacity="0.9" />
            <stop offset="100%" stopColor="#8B5E3C" stopOpacity="0.35" />
          </linearGradient>

          <linearGradient id="flowHarmonicWaveGradient" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#3D2A1F" stopOpacity="0.2" />
            <stop offset="50%" stopColor="#E0A96D" stopOpacity="0.8" />
            <stop offset="100%" stopColor="#3D2A1F" stopOpacity="0.2" />
          </linearGradient>

          <linearGradient id="flowWaveFillGradient" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stopColor="#B87333" stopOpacity="0.28" />
            <stop offset="60%" stopColor="#8B5E3C" stopOpacity="0.08" />
            <stop offset="100%" stopColor="#1A0F08" stopOpacity="0" />
          </linearGradient>

          <linearGradient id="flowRibGradient" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stopColor="#FFE4C4" />
            <stop offset="50%" stopColor="#B87333" />
            <stop offset="100%" stopColor="#6E4122" />
          </linearGradient>

          <filter id="flowAmberWaveGlow" x="-10%" y="-30%" width="120%" height="160%">
            <feGaussianBlur stdDeviation="1.2" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        </defs>

        {/* Ambient Aurora Wave Gradient Fill */}
        {paths.fill1 && (
          <path d={paths.fill1} fill="url(#flowWaveFillGradient)" opacity={0.65} />
        )}

        {/* Wave 3: Deep harmonic foundation */}
        {paths.wave3 && (
          <path
            d={paths.wave3}
            stroke="#8B5E3C"
            strokeWidth="1.0"
            strokeOpacity="0.35"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        )}

        {/* Voice Frequency Ribs */}
        {isListening &&
          ribs.map((rib, idx) => (
            <rect
              key={idx}
              x={rib.x - 0.9}
              y={13 - rib.height / 2}
              width="1.8"
              height={rib.height}
              rx="0.9"
              fill="url(#flowRibGradient)"
              opacity={rib.opacity}
            />
          ))}

        {/* Wave 2: Harmonic secondary counter-wave */}
        {paths.wave2 && (
          <path
            d={paths.wave2}
            stroke="url(#flowHarmonicWaveGradient)"
            strokeWidth="1.3"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity={0.8}
          />
        )}

        {/* Wave 1: Dominant Glowing Amber Wave */}
        {paths.wave1 && (
          <path
            d={paths.wave1}
            stroke="url(#flowAmberWaveGradient)"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            filter="url(#flowAmberWaveGlow)"
          />
        )}

        {/* Epicenter pulse node when voice energy peaks */}
        {isListening && audioLevel > 15 && (
          <circle
            cx="80"
            cy="13"
            r={Math.min(2.8, 1.0 + (audioLevel / 100) * 2.2)}
            fill="#FFF5E6"
            opacity={Math.min(0.9, 0.45 + (audioLevel / 100) * 0.45)}
          />
        )}
      </svg>
    </div>
  );
};

interface FloatingHudProps {
  sessionState: SessionState;
  onStartDictation: () => void;
  onStopDictation: () => void;
  onBacktrack: () => void;
  onClose: () => void;
  audioLevel: number; // 0 - 100
  previewText?: string;
  isHandsFree?: boolean;
  setIsHandsFree?: (val: boolean) => void;
  activeStyleName?: string;
  lastTranscript?: DictationEntry | null;
  onCopyTranscript?: (text: string) => void;
  onInsertTranscript?: (text: string) => void;
  onOpenSettings?: () => void;
  activeMic?: string;
  bottomOffset?: number; // 24 - 48px
  multiMonitorMode?: 'primary' | 'secondary' | 'all';
  hasError?: boolean;
  errorMessage?: string;
  onRetry?: () => void;
}

export const FloatingHud: React.FC<FloatingHudProps> = ({
  sessionState,
  onStartDictation,
  onStopDictation,
  onBacktrack,
  onClose,
  audioLevel,
  previewText,
  lastTranscript,
  onCopyTranscript,
  onInsertTranscript,
  onOpenSettings,
  activeMic = 'Default Microphone (WASAPI)',
  bottomOffset = 28,
  multiMonitorMode = 'primary',
  hasError = false,
  errorMessage,
  onRetry,
}) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const [showFlyout, setShowFlyout] = useState(false);
  const [copied, setCopied] = useState(false);
  const [inserted, setInserted] = useState(false);

  const isListening = sessionState === 'listening';
  const isProcessing = sessionState === 'processing';
  const isDone = sessionState === 'inserted';

  // Haptic feedback trigger for tactile feel
  const triggerHaptic = () => {
    try {
      if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
        navigator.vibrate(10);
      }
    } catch {
      // Ignore if unsupported in environment
    }
  };

  const handleCopy = (text: string) => {
    if (!text) return;
    triggerHaptic();
    if (onCopyTranscript) onCopyTranscript(text);
    else navigator.clipboard?.writeText(text).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  const handleInsert = (text: string) => {
    if (!text) return;
    triggerHaptic();
    if (onInsertTranscript) onInsertTranscript(text);
    setInserted(true);
    setTimeout(() => setInserted(false), 1800);
  };

  return (
    <div
      id="flow-floating-bar-root"
      style={{ bottom: `${bottomOffset}px` }}
      className="fixed left-1/2 -translate-x-1/2 z-50 flex flex-col items-center select-none pointer-events-auto font-sans"
    >
      {/* 1. Contextual Flyout Overlay (Glassomorphic North-Anchored) */}
      {showFlyout && (
        <div
          id="flow-hud-flyout"
          className="mb-2.5 w-72 sm:w-80 rounded-2xl bg-gradient-to-b from-[#1E110A]/85 via-[#140B05]/80 to-[#100703]/85 backdrop-blur-2xl backdrop-saturate-150 border border-white/[0.12] border-t-white/[0.22] shadow-[0_16px_40px_rgba(0,0,0,0.8),inset_0_1px_1px_rgba(255,255,255,0.18)] p-3 text-xs text-[#F4E0C6] animate-in fade-in slide-in-from-bottom-2 space-y-2.5"
        >
          {/* Header */}
          <div className="flex items-center justify-between pb-2 border-b border-white/[0.08]">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-[#B87333] animate-pulse"></span>
              <span className="font-bold text-xs tracking-tight text-[#F4E0C6]">FLOW Companion</span>
            </div>
            <button
              onClick={() => {
                triggerHaptic();
                setShowFlyout(false);
              }}
              className="w-5 h-5 rounded-full hover:bg-white/[0.1] active:scale-90 flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
              title="Close Menu"
            >
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path d="M6 18L18 6M6 6l12 12" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </div>

          {/* Quick Hardware & Hotkey Info */}
          <div className="grid grid-cols-2 gap-2">
            <div className="p-2 rounded-xl bg-white/[0.04] border border-white/[0.08] flex flex-col gap-0.5">
              <span className="text-[10px] uppercase font-mono text-[#B89B7A]">Audio Endpoint</span>
              <span className="text-[11px] font-medium text-[#F4E0C6] truncate" title={activeMic}>
                {activeMic.replace('Default Windows Audio Endpoint', 'WASAPI Mic')}
              </span>
            </div>

            <div className="p-2 rounded-xl bg-white/[0.04] border border-white/[0.08] flex flex-col gap-0.5">
              <span className="text-[10px] uppercase font-mono text-[#B89B7A]">Display Target</span>
              <span className="text-[11px] font-medium text-[#F4E0C6]">
                {multiMonitorMode === 'all' ? 'All Connected Displays' : multiMonitorMode === 'secondary' ? 'Display 2' : 'Display 1 (Primary)'}
              </span>
            </div>
          </div>

          {/* Last Transcript Quick Action */}
          {lastTranscript && (
            <div className="p-2.5 rounded-xl bg-white/[0.04] border border-white/[0.08] space-y-1.5">
              <div className="flex items-center justify-between text-[11px]">
                <span className="text-[#B89B7A] font-medium">Last Transcript</span>
                <span className="text-[#B89B7A]/70 font-mono text-[10px]">{lastTranscript.wordCount} words</span>
              </div>
              <p className="text-[11px] text-[#F4E0C6] font-mono line-clamp-2 leading-relaxed bg-black/40 p-2 rounded-lg border border-white/[0.06]">
                "{lastTranscript.text}"
              </p>
              <div className="flex items-center gap-1.5 pt-0.5">
                <button
                  onClick={() => handleCopy(lastTranscript.text)}
                  className="flex-1 py-1 rounded-lg bg-white/[0.08] hover:bg-white/[0.14] active:scale-95 border border-white/[0.1] text-[#F4E0C6] text-[11px] font-medium transition flex items-center justify-center gap-1"
                >
                  <svg className="w-3.5 h-3.5 text-[#B89B7A]" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                    <path d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  <span>{copied ? 'Copied ✓' : 'Copy'}</span>
                </button>
                <button
                  onClick={() => handleInsert(lastTranscript.text)}
                  className="flex-1 py-1 rounded-lg bg-gradient-to-r from-[#B87333] to-[#9E6028] hover:from-[#C88343] hover:to-[#AE7038] active:scale-95 text-black font-semibold text-[11px] transition flex items-center justify-center gap-1 shadow-sm"
                >
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                    <path d="M11 16l-4-4m0 0l4-4m-4 4h14" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  <span>{inserted ? 'Inserted ✓' : 'Insert Text'}</span>
                </button>
              </div>
            </div>
          )}

          {/* Context Footer */}
          <div className="flex items-center justify-between pt-1 border-t border-white/[0.08] text-[11px]">
            <button
              onClick={() => {
                triggerHaptic();
                setShowFlyout(false);
                onBacktrack();
              }}
              className="text-[#B89B7A] hover:text-[#F4E0C6] active:scale-95 flex items-center gap-1 transition"
            >
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              <span>Backtrack Undo</span>
            </button>

            <div className="flex items-center gap-2">
              {onOpenSettings && (
                <button
                  onClick={() => {
                    triggerHaptic();
                    setShowFlyout(false);
                    onOpenSettings();
                  }}
                  className="text-[#B87333] hover:text-[#FFC896] font-medium transition"
                >
                  Settings
                </button>
              )}
              <button
                onClick={() => {
                  triggerHaptic();
                  onClose();
                }}
                className="text-rose-400 hover:text-rose-300 font-medium px-1.5 py-0.5 rounded hover:bg-rose-950/40 transition"
                title="Hide Floating Bar"
              >
                Hide
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 2. Primary Production Floating Bar (Refined Glassomorphic Capsule with Haptic Physics) */}
      <div
        id="flow-production-floating-bar"
        className={`relative flex items-center transition-all duration-300 ease-out backdrop-blur-2xl backdrop-saturate-200 border rounded-full px-2.5 py-1 gap-2 shadow-[0_12px_32px_-4px_rgba(0,0,0,0.7),0_0_20px_1px_rgba(184,115,51,0.16)] shadow-[inset_0_1px_1.5px_0_rgba(255,255,255,0.22),inset_0_-1px_1px_0_rgba(0,0,0,0.45)] ${
          isListening
            ? 'recording-amber-glow hud-recording-breathe bg-gradient-to-r from-[#2A160A]/60 via-[#1C0D05]/50 to-[#2A160A]/60 border-[#B87333]/90 border-t-amber-200/40 w-[275px] sm:w-[305px] h-[44px]'
            : isExpanded
            ? 'bg-gradient-to-r from-[#201208]/55 via-[#160B04]/45 to-[#201208]/55 border-white/[0.14] border-t-white/[0.26] w-[400px] sm:w-[440px] h-[44px] hover:border-[#B87333]/50'
            : 'bg-gradient-to-r from-[#201208]/55 via-[#160B04]/45 to-[#201208]/55 border-white/[0.14] border-t-white/[0.26] w-[230px] sm:w-[250px] h-[44px] hover:border-[#B87333]/50'
        }`}
      >
        {/* Specular glass reflection top highlight */}
        <div className="pointer-events-none absolute inset-x-4 top-0.5 h-[40%] rounded-t-full bg-gradient-to-b from-white/[0.18] to-transparent opacity-90" />

        {/* Left Action Trigger: Tactile Glass Microphone Icon Button */}
        <button
          id="flow-hud-mic-trigger"
          onClick={() => {
            triggerHaptic();
            if (isListening) {
              onStopDictation();
            } else {
              onStartDictation();
            }
          }}
          className={`w-7.5 h-7.5 rounded-full flex items-center justify-center transition-all duration-150 transform active:scale-90 active:translate-y-[0.5px] shrink-0 focus:outline-none backdrop-blur-md ${
            isListening
              ? 'bg-gradient-to-tr from-[#B87333] to-[#FFC896] text-black border border-amber-200/80 shadow-[0_0_12px_rgba(255,200,150,0.55),inset_0_1px_1px_rgba(255,255,255,0.7)] scale-105'
              : isProcessing
              ? 'bg-white/[0.06] border border-white/[0.12] text-[#B89B7A] opacity-75'
              : isDone
              ? 'bg-emerald-900/60 border border-emerald-400/60 text-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.3)]'
              : hasError
              ? 'bg-rose-950/70 border border-rose-500/60 text-rose-400 shadow-[0_0_8px_rgba(244,63,94,0.3)]'
              : 'bg-white/[0.08] hover:bg-white/[0.16] border border-white/[0.18] text-[#F4E0C6] shadow-[inset_0_1px_1px_rgba(255,255,255,0.22)]'
          }`}
          title={isListening ? 'Click to Stop Dictation' : 'Click to Speak (or Hold Alt + Space)'}
        >
          {isDone ? (
            /* Completed Checkmark */
            <svg className="w-3.5 h-3.5 text-emerald-400" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
              <path d="M5 13l4 4L19 7" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          ) : isProcessing ? (
            /* Spinning Progress Ring */
            <svg className="w-3.5 h-3.5 text-[#B87333] spin-progress" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" />
              <path className="opacity-80" d="M4 12a8 8 0 018-8v8H4z" fill="currentColor" />
            </svg>
          ) : hasError ? (
            /* Error Exclamation */
            <svg className="w-3.5 h-3.5 text-rose-400" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
              <path d="M12 9v2m0 4h.01" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          ) : (
            /* Microphone SVG Icon */
            <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m-4 0h8m-4-18a3 3 0 00-3 3v8a3 3 0 006 0V5a3 3 0 00-3-3z"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          )}
        </button>

        {/* Center Stage Dynamic Content */}
        <div className="flex items-center min-w-[110px] flex-1 pr-1 overflow-hidden">
          {/* STATE A: Listening / Realtime Audio Waveform (Timing removed as requested) */}
          {isListening && (
            <div className="flex items-center justify-center w-full animate-in fade-in duration-150 overflow-hidden px-1">
              <DynamicSvgWave audioLevel={audioLevel} isListening={isListening} />
            </div>
          )}

          {/* STATE B: Transcribing Spinner */}
          {!isListening && isProcessing && (
            <div className="flex items-center gap-2 animate-in fade-in duration-150">
              <svg className="w-3.5 h-3.5 text-[#B87333] spin-progress" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" />
                <path className="opacity-80" d="M4 12a8 8 0 018-8v8H4z" fill="currentColor" />
              </svg>
              <span className="text-[11px] font-medium text-[#F4E0C6]">Transcribing...</span>
            </div>
          )}

          {/* STATE C: Completed / Done */}
          {!isListening && !isProcessing && isDone && (
            <div className="flex items-center gap-1.5 animate-in fade-in duration-150">
              <span className="text-[11px] font-bold text-emerald-400">Done</span>
              <span className="text-[9.5px] text-[#B89B7A]">Text ready</span>
            </div>
          )}

          {/* STATE D: Error */}
          {!isListening && !isProcessing && !isDone && hasError && (
            <div className="flex items-center justify-between w-full animate-in fade-in duration-150">
              <span className="text-[11px] text-rose-300 truncate">
                {errorMessage || 'Unable to transcribe'}
              </span>
              {onRetry && (
                <button
                  onClick={() => {
                    triggerHaptic();
                    onRetry();
                  }}
                  className="px-2 py-0.5 rounded bg-white/[0.08] hover:bg-white/[0.14] active:scale-95 text-[#F4E0C6] text-[9.5px] font-semibold transition shrink-0 ml-1.5 border border-white/[0.1]"
                >
                  Retry
                </button>
              )}
            </div>
          )}

          {/* STATE E: Idle / Ready */}
          {!isListening && !isProcessing && !isDone && !hasError && (
            <div className="flex items-center gap-2 w-full">
              <div className="flex flex-col text-left">
                <span className="text-[11px] font-bold text-[#F4E0C6] tracking-tight leading-none">FLOW</span>
                <span className="text-[9.5px] text-[#B89B7A] font-medium leading-none mt-0.5">Ready</span>
              </div>

              {/* Expanded View Shortcuts Preview */}
              {isExpanded && (
                <div className="flex items-center gap-2 pl-2.5 border-l border-white/[0.12] text-[10px] text-[#B89B7A] ml-1.5">
                  <span className="text-[#F4E0C6] font-medium">
                    Hold{' '}
                    <kbd className="px-1.5 py-0.5 rounded bg-white/[0.08] text-[9.5px] text-[#F4E0C6] font-mono border border-white/[0.15]">
                      Alt + Space
                    </kbd>
                  </span>
                  <span className="text-[10px] text-[#B89B7A]/80">
                    Press{' '}
                    <kbd className="px-1.5 py-0.5 rounded bg-white/[0.08] text-[9.5px] text-[#F4E0C6] font-mono border border-white/[0.15]">
                      Alt + B
                    </kbd>
                  </span>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Trailing Controls (Options ••• / Expand / Settings) */}
        <div className="flex items-center gap-0.5 pl-0.5 shrink-0">
          {isExpanded ? (
            <div className="flex items-center gap-1 animate-in fade-in">
              {onOpenSettings && (
                <button
                  onClick={() => {
                    triggerHaptic();
                    onOpenSettings();
                  }}
                  className="w-6 h-6 rounded-full hover:bg-white/[0.1] active:scale-90 flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
                  title="Open Settings"
                >
                  <svg className="w-3 h-3" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                    <path d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                    <path d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                </button>
              )}

              <button
                onClick={() => {
                  triggerHaptic();
                  setIsExpanded(false);
                }}
                className="w-6 h-6 rounded-full hover:bg-white/[0.1] active:scale-90 flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
                title="Collapse Bar"
              >
                <svg className="w-3 h-3" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M6 18L18 6M6 6l12 12" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-0.5">
              <button
                id="flow-hud-overflow-trigger"
                onClick={() => {
                  triggerHaptic();
                  setShowFlyout(!showFlyout);
                }}
                className={`w-6 h-6 rounded-full flex items-center justify-center transition active:scale-90 ${
                  showFlyout
                    ? 'bg-white/[0.15] text-[#F4E0C6]'
                    : 'text-[#B89B7A] hover:text-[#F4E0C6] hover:bg-white/[0.1]'
                }`}
                title="Options Menu"
              >
                <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M6 10a2 2 0 11-4 0 2 2 0 014 0zM12 10a2 2 0 11-4 0 2 2 0 014 0zM18 10a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
              </button>

              <button
                id="flow-hud-expand-trigger"
                onClick={() => {
                  triggerHaptic();
                  setIsExpanded(true);
                }}
                className="w-5.5 h-5.5 rounded-full text-[#B89B7A]/70 hover:text-[#F4E0C6] hover:bg-white/[0.1] active:scale-90 flex items-center justify-center transition hidden sm:flex"
                title="Expand Floating Bar"
              >
                <svg className="w-2.5 h-2.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M4 8V4m0 0h4M4 4l5 5m11-5h-4m4 0v4m0-4l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
                </svg>
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
