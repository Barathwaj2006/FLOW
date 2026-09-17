import React, { useState, useEffect, useRef } from 'react';
import { DictationEntry, SessionState } from '../types';

interface DynamicSvgWaveProps {
  audioLevel: number; // 0 - 100
  isListening: boolean;
  className?: string;
}

/**
 * High-performance, organic SVG wave visualization for the FLOW Obsidian Amber HUD.
 * Reacts directly to the audioLevel prop with smooth lerp interpolation and harmonic sine waves.
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

  const phaseRef = useRef(0);
  const audioLevelRef = useRef(audioLevel);
  const smoothedLevelRef = useRef(audioLevel);
  const animFrameRef = useRef<number | null>(null);

  useEffect(() => {
    audioLevelRef.current = Math.max(0, Math.min(100, audioLevel));
  }, [audioLevel]);

  useEffect(() => {
    if (!isListening) {
      const width = 140;
      const height = 28;
      const midY = height / 2;
      const resting = `M 0 ${midY} Q ${width / 2} ${midY} ${width} ${midY}`;
      setPaths({
        wave1: resting,
        wave2: resting,
        wave3: resting,
        fill1: `M 0 ${midY} L ${width} ${midY} L ${width} ${height} L 0 ${height} Z`,
      });
      return;
    }

    const width = 140;
    const height = 28;
    const midY = height / 2;
    const pointCount = 44;
    const dx = width / pointCount;

    const animate = () => {
      smoothedLevelRef.current += (audioLevelRef.current - smoothedLevelRef.current) * 0.2;
      const currentLevel = smoothedLevelRef.current;

      // When level is near 0 (silence/quiet background), waveform settles to flat line
      const isQuiet = currentLevel < 1.5;
      const speed = isQuiet ? 0 : 0.04 + (currentLevel / 100) * 0.16;
      phaseRef.current += speed;
      const phase = phaseRef.current;

      const baseAmp = isQuiet ? 0 : (currentLevel / 100) * 12.0;

      let d1 = '';
      let d2 = '';
      let d3 = '';

      for (let i = 0; i <= pointCount; i++) {
        const x = i * dx;
        const envelope = Math.sin((i / pointCount) * Math.PI);

        const y1 = midY + Math.sin(x * 0.075 + phase) * (baseAmp * envelope);
        const y2 = midY + Math.sin(x * 0.115 - phase * 1.35 + 1.2) * (baseAmp * 0.72 * envelope);
        const y3 = midY + Math.sin(x * 0.05 + phase * 0.65 + 2.4) * (baseAmp * 0.48 * envelope);

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

      setPaths({
        wave1: d1,
        wave2: d2,
        wave3: d3,
        fill1,
      });

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
        className="w-[125px] sm:w-[145px] h-[28px] overflow-visible select-none"
        viewBox="0 0 140 28"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <linearGradient id="flowAmberWaveGradient" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#8B5E3C" stopOpacity="0.3" />
            <stop offset="25%" stopColor="#B87333" stopOpacity="0.9" />
            <stop offset="50%" stopColor="#FFC896" stopOpacity="1" />
            <stop offset="75%" stopColor="#B87333" stopOpacity="0.9" />
            <stop offset="100%" stopColor="#8B5E3C" stopOpacity="0.3" />
          </linearGradient>

          <linearGradient id="flowHarmonicWaveGradient" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#3D2A1F" stopOpacity="0.2" />
            <stop offset="50%" stopColor="#E0A96D" stopOpacity="0.75" />
            <stop offset="100%" stopColor="#3D2A1F" stopOpacity="0.2" />
          </linearGradient>

          <linearGradient id="flowWaveFillGradient" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stopColor="#B87333" stopOpacity="0.25" />
            <stop offset="60%" stopColor="#8B5E3C" stopOpacity="0.08" />
            <stop offset="100%" stopColor="#1A0F08" stopOpacity="0" />
          </linearGradient>

          <filter id="flowAmberWaveGlow" x="-10%" y="-30%" width="120%" height="160%">
            <feGaussianBlur stdDeviation="1.5" result="blur" />
            <feComposite in="SourceGraphic" in2="blur" operator="over" />
          </filter>
        </defs>

        {paths.fill1 && (
          <path d={paths.fill1} fill="url(#flowWaveFillGradient)" opacity={0.7} />
        )}

        {paths.wave3 && (
          <path
            d={paths.wave3}
            stroke="#8B5E3C"
            strokeWidth="1.2"
            strokeOpacity="0.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        )}

        {paths.wave2 && (
          <path
            d={paths.wave2}
            stroke="url(#flowHarmonicWaveGradient)"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity={0.85}
          />
        )}

        {paths.wave1 && (
          <path
            d={paths.wave1}
            stroke="url(#flowAmberWaveGradient)"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
            filter="url(#flowAmberWaveGlow)"
          />
        )}

        {isListening && audioLevel > 12 && (
          <circle
            cx="70"
            cy="14"
            r={Math.min(3.2, 1.2 + (audioLevel / 100) * 2.5)}
            fill="#FFF2E0"
            opacity={Math.min(0.9, 0.4 + (audioLevel / 100) * 0.5)}
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
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  const isListening = sessionState === 'listening';
  const isProcessing = sessionState === 'processing';
  const isDone = sessionState === 'inserted';

  // Elapsed timer tracking
  useEffect(() => {
    let timer: any = null;
    if (isListening) {
      setElapsedSeconds(0);
      timer = setInterval(() => {
        setElapsedSeconds(prev => prev + 1);
      }, 1000);
    } else {
      setElapsedSeconds(0);
    }
    return () => {
      if (timer) clearInterval(timer);
    };
  }, [isListening]);

  // Format time as MM:SS (e.g., 00:00, 00:09, 01:24)
  const formatTimer = (totalSec: number) => {
    const mins = Math.floor(totalSec / 60);
    const secs = totalSec % 60;
    const mm = mins.toString().padStart(2, '0');
    const ss = secs.toString().padStart(2, '0');
    return `${mm}:${ss}`;
  };

  const handleCopy = (text: string) => {
    if (!text) return;
    if (onCopyTranscript) onCopyTranscript(text);
    else navigator.clipboard?.writeText(text).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  const handleInsert = (text: string) => {
    if (!text) return;
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
      {/* 1. Contextual Flyout Overlay (North-Anchored) */}
      {showFlyout && (
        <div
          id="flow-hud-flyout"
          className="mb-3 w-80 sm:w-96 rounded-2xl bg-[#1A0F08]/98 backdrop-blur-2xl border border-[#3D2A1F] shadow-2xl p-3.5 text-xs text-[#F4E0C6] animate-in fade-in slide-in-from-bottom-2 space-y-3"
        >
          {/* Header */}
          <div className="flex items-center justify-between pb-2 border-b border-[#3D2A1F]/70">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-[#B87333] animate-pulse"></span>
              <span className="font-bold text-xs tracking-tight text-[#F4E0C6]">FLOW Companion</span>
            </div>
            <button
              onClick={() => setShowFlyout(false)}
              className="w-5 h-5 rounded-full hover:bg-[#2B1A10] flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
              title="Close Menu"
            >
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path d="M6 18L18 6M6 6l12 12" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </div>

          {/* Quick Hardware & Hotkey Info */}
          <div className="grid grid-cols-2 gap-2">
            <div className="p-2 rounded-xl bg-[#24150C] border border-[#3D2A1F]/80 flex flex-col gap-0.5">
              <span className="text-[10px] uppercase font-mono text-[#B89B7A]">Audio Endpoint</span>
              <span className="text-[11px] font-medium text-[#F4E0C6] truncate" title={activeMic}>
                {activeMic.replace('Default Windows Audio Endpoint', 'WASAPI Mic')}
              </span>
            </div>

            <div className="p-2 rounded-xl bg-[#24150C] border border-[#3D2A1F]/80 flex flex-col gap-0.5">
              <span className="text-[10px] uppercase font-mono text-[#B89B7A]">Display Target</span>
              <span className="text-[11px] font-medium text-[#F4E0C6]">
                {multiMonitorMode === 'all' ? 'All Connected Displays' : multiMonitorMode === 'secondary' ? 'Display 2' : 'Display 1 (Primary)'}
              </span>
            </div>
          </div>

          {/* Last Transcript Quick Action */}
          {lastTranscript && (
            <div className="p-2.5 rounded-xl bg-[#24150C] border border-[#3D2A1F]/80 space-y-1.5">
              <div className="flex items-center justify-between text-[11px]">
                <span className="text-[#B89B7A] font-medium">Last Transcript</span>
                <span className="text-[#B89B7A]/70 font-mono text-[10px]">{lastTranscript.wordCount} words</span>
              </div>
              <p className="text-[11px] text-[#F4E0C6] font-mono line-clamp-2 leading-relaxed bg-[#1A0F08]/80 p-2 rounded-lg border border-[#3D2A1F]/50">
                "{lastTranscript.text}"
              </p>
              <div className="flex items-center gap-1.5 pt-0.5">
                <button
                  onClick={() => handleCopy(lastTranscript.text)}
                  className="flex-1 py-1 rounded-lg bg-[#2B1A10] hover:bg-[#382317] border border-[#3D2A1F] text-[#F4E0C6] text-[11px] font-medium transition flex items-center justify-center gap-1"
                >
                  <svg className="w-3.5 h-3.5 text-[#B89B7A]" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                    <path d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  <span>{copied ? 'Copied ✓' : 'Copy'}</span>
                </button>
                <button
                  onClick={() => handleInsert(lastTranscript.text)}
                  className="flex-1 py-1 rounded-lg bg-[#B87333] hover:bg-[#9E6028] text-black font-semibold text-[11px] transition flex items-center justify-center gap-1"
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
          <div className="flex items-center justify-between pt-1 border-t border-[#3D2A1F]/70 text-[11px]">
            <button
              onClick={() => {
                setShowFlyout(false);
                onBacktrack();
              }}
              className="text-[#B89B7A] hover:text-[#F4E0C6] flex items-center gap-1 transition"
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
                    setShowFlyout(false);
                    onOpenSettings();
                  }}
                  className="text-[#B87333] hover:text-[#FFC896] font-medium transition"
                >
                  Settings
                </button>
              )}
              <button
                onClick={onClose}
                className="text-rose-400 hover:text-rose-300 font-medium px-1.5 py-0.5 rounded hover:bg-rose-950/40 transition"
                title="Hide Floating Bar"
              >
                Hide
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 2. Primary Production Floating Bar (Obsidian-Amber Capsule) */}
      <div
        id="flow-production-floating-bar"
        className={`relative flex items-center transition-all duration-300 ease-out bg-[#1A0F08]/96 backdrop-blur-2xl border border-[#3D2A1F] rounded-full px-3 py-1.5 gap-3 shadow-[0_14px_40px_-4px_rgba(0,0,0,0.85),0_0_24px_2px_rgba(184,115,51,0.18)] shadow-[inset_0_1px_1px_0_rgba(244,224,198,0.12)] ${
          isListening
            ? 'recording-amber-glow hud-recording-breathe border-[#B87333]/90 w-[370px] sm:w-[410px] h-[54px]'
            : isExpanded
            ? 'w-[480px] sm:w-[520px] h-[54px] hover:border-[#B87333]/40'
            : 'w-[290px] sm:w-[310px] h-[54px] hover:border-[#B87333]/40'
        }`}
      >
        {/* Left Action Trigger: Physical Microphone Icon Button */}
        <button
          id="flow-hud-mic-trigger"
          onClick={() => {
            if (isListening) {
              onStopDictation();
            } else {
              onStartDictation();
            }
          }}
          className={`w-9 h-9 rounded-full flex items-center justify-center transition-all duration-150 transform active:scale-95 shrink-0 focus:outline-none ${
            isListening
              ? 'bg-[#B87333] text-black border border-[#B87333] shadow-lg scale-105'
              : isProcessing
              ? 'bg-[#27170E] border border-[#3D2A1F] text-[#B89B7A] opacity-75'
              : isDone
              ? 'bg-[#064e3b] border border-emerald-600/60 text-emerald-400'
              : hasError
              ? 'bg-rose-950/80 border border-rose-600/60 text-rose-400'
              : 'bg-[#27170E] hover:bg-[#341F13] border border-[#3D2A1F]/80 text-[#F4E0C6]'
          }`}
          title={isListening ? 'Click to Stop Dictation' : 'Click to Speak (or Hold Alt + Space)'}
        >
          {isDone ? (
            /* Completed Checkmark */
            <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
              <path d="M5 13l4 4L19 7" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          ) : isProcessing ? (
            /* Spinning Progress Ring */
            <svg className="w-4 h-4 text-[#B87333] spin-progress" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" />
              <path className="opacity-80" d="M4 12a8 8 0 018-8v8H4z" fill="currentColor" />
            </svg>
          ) : hasError ? (
            /* Error Exclamation */
            <svg className="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
              <path d="M12 9v2m0 4h.01" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          ) : (
            /* Microphone SVG Icon */
            <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m-4 0h8m-4-18a3 3 0 00-3 3v8a3 3 0 006 0V5a3 3 0 00-3-3z"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          )}
        </button>

        {/* Center Stage Dynamic Content */}
        <div className="flex items-center min-w-[130px] flex-1 pr-1 overflow-hidden">
          {/* STATE A: Listening / Realtime Audio Waveform & MM:SS Elapsed Timer */}
          {isListening && (
            <div className="flex items-center justify-between gap-2.5 w-full animate-in fade-in duration-150 overflow-hidden">
              <DynamicSvgWave audioLevel={audioLevel} isListening={isListening} />
              <div
                id="flow-recording-timer-pill"
                className="flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-[#27170E]/85 border border-[#3D2A1F] shrink-0 select-none shadow-xs"
                title="Recording Duration (MM:SS)"
              >
                <span className="w-1.5 h-1.5 rounded-full bg-[#B87333] animate-pulse" />
                <span 
                  id="flow-recording-timer"
                  className="font-mono text-xs font-bold text-[#F4E0C6] tracking-wider tabular-nums"
                >
                  {formatTimer(elapsedSeconds)}
                </span>
              </div>
            </div>
          )}

          {/* STATE B: Transcribing Spinner */}
          {!isListening && isProcessing && (
            <div className="flex items-center gap-2.5 animate-in fade-in duration-150">
              <svg className="w-4 h-4 text-[#B87333] spin-progress" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" />
                <path className="opacity-80" d="M4 12a8 8 0 018-8v8H4z" fill="currentColor" />
              </svg>
              <span className="text-xs font-medium text-[#F4E0C6]">Transcribing...</span>
            </div>
          )}

          {/* STATE C: Completed / Done */}
          {!isListening && !isProcessing && isDone && (
            <div className="flex items-center gap-2 animate-in fade-in duration-150">
              <div className="flex flex-col text-left">
                <span className="text-xs font-bold text-emerald-400">Done</span>
                <span className="text-[10px] text-[#B89B7A]">Text ready</span>
              </div>
            </div>
          )}

          {/* STATE D: Error */}
          {!isListening && !isProcessing && !isDone && hasError && (
            <div className="flex items-center justify-between w-full animate-in fade-in duration-150">
              <span className="text-xs text-rose-300 truncate">
                {errorMessage || 'Unable to transcribe'}
              </span>
              {onRetry && (
                <button
                  onClick={onRetry}
                  className="px-2 py-0.5 rounded bg-[#3D2A1F] hover:bg-[#523829] text-[#F4E0C6] text-[10px] font-semibold transition shrink-0 ml-2"
                >
                  Retry
                </button>
              )}
            </div>
          )}

          {/* STATE E: Idle / Ready */}
          {!isListening && !isProcessing && !isDone && !hasError && (
            <div className="flex items-center gap-2.5 w-full">
              <div className="flex flex-col text-left">
                <span className="text-xs font-bold text-[#F4E0C6] tracking-tight">FLOW</span>
                <span className="text-[11px] text-[#B89B7A] font-medium">Ready</span>
              </div>

              {/* Expanded View Shortcuts Preview */}
              {isExpanded && (
                <div className="flex items-center gap-2.5 pl-3 border-l border-[#3D2A1F]/70 text-xs text-[#B89B7A] ml-2">
                  <span className="text-[#F4E0C6] font-medium">
                    Hold{' '}
                    <kbd className="px-1.5 py-0.5 rounded bg-[#2A1A11] text-[10px] text-[#F4E0C6] font-mono border border-[#3D2A1F]">
                      Alt + Space
                    </kbd>
                  </span>
                  <span className="text-[11px] text-[#B89B7A]/80">
                    Press{' '}
                    <kbd className="px-1.5 py-0.5 rounded bg-[#2A1A11] text-[10px] text-[#F4E0C6] font-mono border border-[#3D2A1F]">
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
                  onClick={onOpenSettings}
                  className="w-7 h-7 rounded-full hover:bg-[#2B1A10] flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
                  title="Open Settings"
                >
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                    <path d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                    <path d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                </button>
              )}

              <button
                onClick={() => setIsExpanded(false)}
                className="w-7 h-7 rounded-full hover:bg-[#2B1A10] flex items-center justify-center text-[#B89B7A] hover:text-[#F4E0C6] transition"
                title="Collapse Bar"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M6 18L18 6M6 6l12 12" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-0.5">
              <button
                id="flow-hud-overflow-trigger"
                onClick={() => setShowFlyout(!showFlyout)}
                className={`w-7 h-7 rounded-full flex items-center justify-center transition ${
                  showFlyout
                    ? 'bg-[#2B1A10] text-[#F4E0C6]'
                    : 'text-[#B89B7A] hover:text-[#F4E0C6] hover:bg-[#2B1A10]'
                }`}
                title="Options Menu"
              >
                <svg className="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M6 10a2 2 0 11-4 0 2 2 0 014 0zM12 10a2 2 0 11-4 0 2 2 0 014 0zM18 10a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
              </button>

              <button
                id="flow-hud-expand-trigger"
                onClick={() => setIsExpanded(true)}
                className="w-6 h-6 rounded-full text-[#B89B7A]/70 hover:text-[#F4E0C6] hover:bg-[#2B1A10] flex items-center justify-center transition hidden sm:flex"
                title="Expand Floating Bar"
              >
                <svg className="w-3 h-3" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
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
