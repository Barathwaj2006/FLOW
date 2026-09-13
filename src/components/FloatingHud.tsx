import React, { useEffect, useState } from 'react';
import { SessionState } from '../types';

interface FloatingHudProps {
  sessionState: SessionState;
  onStartDictation: () => void;
  onStopDictation: () => void;
  onBacktrack: () => void;
  onClose: () => void;
  audioLevel: number; // 0 - 100
  previewText: string;
  isHandsFree: boolean;
  setIsHandsFree: (val: boolean) => void;
  activeStyleName: string;
}

export const FloatingHud: React.FC<FloatingHudProps> = ({
  sessionState,
  onStartDictation,
  onStopDictation,
  onBacktrack,
  onClose,
  audioLevel,
  previewText,
  isHandsFree,
  setIsHandsFree,
  activeStyleName,
}) => {
  const isListening = sessionState === 'listening';
  const isProcessing = sessionState === 'processing';

  // Dynamic waveform bars
  const [bars, setBars] = useState<number[]>([15, 25, 45, 30, 20, 35, 50, 25]);

  useEffect(() => {
    if (isListening) {
      const interval = setInterval(() => {
        const factor = Math.max(0.2, audioLevel / 100);
        setBars([
          Math.min(90, Math.floor(15 + Math.random() * 50 * factor)),
          Math.min(100, Math.floor(25 + Math.random() * 75 * factor)),
          Math.min(100, Math.floor(40 + Math.random() * 60 * factor)),
          Math.min(100, Math.floor(30 + Math.random() * 70 * factor)),
          Math.min(95, Math.floor(20 + Math.random() * 65 * factor)),
          Math.min(100, Math.floor(35 + Math.random() * 70 * factor)),
          Math.min(90, Math.floor(15 + Math.random() * 45 * factor)),
          Math.min(80, Math.floor(10 + Math.random() * 35 * factor)),
        ]);
      }, 75);
      return () => clearInterval(interval);
    } else {
      setBars([15, 20, 30, 20, 15, 25, 20, 15]);
    }
  }, [isListening, audioLevel]);

  return (
    <div 
      id="floating-hud-container"
      className="fixed bottom-6 right-6 z-50 flex flex-col items-end pointer-events-auto select-none font-sans"
    >
      {/* Windows 11 Acrylic Light Pill Container */}
      <div className="bg-white/95 backdrop-blur-2xl border border-slate-200/90 rounded-2xl shadow-[0_12px_32px_rgba(19,27,46,0.14),0_2px_8px_rgba(0,97,148,0.08)] p-3.5 w-84 text-slate-800 transition-all duration-300 flex flex-col gap-2.5">
        {/* Top Handle / Status */}
        <div className="flex items-center justify-between pb-2 border-b border-slate-100 text-xs text-slate-500">
          <div className="flex items-center gap-1.5 cursor-grab">
            <span className="material-symbols-outlined text-[15px] text-slate-400">drag_indicator</span>
            <span className="font-bold text-[11px] text-[#0284c7] tracking-wider uppercase">FLOW Bar</span>
            <span className="text-slate-300">•</span>
            <span className="text-[11px] text-slate-600 font-medium truncate max-w-[90px]">{activeStyleName}</span>
          </div>

          <div className="flex items-center gap-1.5">
            <div className="flex items-center gap-1 text-[10px] text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200 font-medium">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
              <span>Zero-Enter</span>
            </div>
            <button 
              onClick={onClose}
              className="w-5 h-5 rounded flex items-center justify-center text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
              title="Minimize HUD"
            >
              <span className="material-symbols-outlined text-[15px]">close</span>
            </button>
          </div>
        </div>

        {/* Center: Live Waveform Visualizer & Microphone Trigger */}
        <div className="flex items-center justify-between py-1">
          <div className="flex items-center gap-2.5">
            {/* Action Mic Button */}
            <button
              id="hud-mic-button"
              onClick={() => {
                if (isListening) {
                  onStopDictation();
                } else {
                  onStartDictation();
                }
              }}
              className={`w-10 h-10 rounded-xl flex items-center justify-center transition-all duration-150 shadow-xs ${
                isListening
                  ? 'bg-rose-600 hover:bg-rose-700 text-white shadow-rose-600/30 scale-105 animate-pulse'
                  : isProcessing
                  ? 'bg-amber-600 text-white animate-spin'
                  : 'bg-[#0284c7] hover:bg-[#0369a1] text-white shadow-[#0284c7]/20 active:scale-95'
              }`}
              title={isListening ? 'Click to Stop Dictating' : 'Click to Start Dictating'}
            >
              <span className="material-symbols-outlined text-[20px]">
                {isListening ? 'mic_off' : isProcessing ? 'sync' : 'mic'}
              </span>
            </button>

            {/* Dynamic Waveform Bars */}
            <div className="flex items-end gap-[3px] h-7 px-1">
              {bars.map((height, i) => (
                <div
                  key={i}
                  style={{ height: `${isListening ? height : 20}%` }}
                  className={`w-1 rounded-full transition-all duration-75 ${
                    isListening
                      ? 'bg-[#0284c7]'
                      : 'bg-slate-200'
                  }`}
                />
              ))}
            </div>
          </div>

          {/* Right Action Tools */}
          <div className="flex items-center gap-1.5">
            <button
              id="hud-btn-hands-free"
              onClick={() => setIsHandsFree(!isHandsFree)}
              className={`px-2 py-1 rounded text-[11px] font-medium border transition-colors ${
                isHandsFree
                  ? 'bg-sky-50 border-sky-300 text-[#0284c7] font-semibold'
                  : 'bg-slate-50 border-slate-200 text-slate-600 hover:text-slate-900'
              }`}
              title="Hands-Free Continuous Dictation Toggle"
            >
              Hands-Free
            </button>

            <button
              id="hud-btn-backtrack"
              onClick={onBacktrack}
              className="w-7 h-7 rounded flex items-center justify-center bg-slate-50 border border-slate-200 text-slate-600 hover:text-slate-900 hover:bg-slate-100 transition-colors"
              title="Backtrack Undo"
            >
              <span className="material-symbols-outlined text-[15px]">undo</span>
            </button>
          </div>
        </div>

        {/* Live Audio / Transcribed Preview Text */}
        <div className="pt-1.5 border-t border-slate-100 text-xs">
          <div className="flex items-center justify-between text-[11px] text-slate-400 mb-1">
            <span>{isListening ? 'Streaming Speech...' : isProcessing ? 'Cleaning transcript...' : 'Active Hotkey:'}</span>
            <kbd className="font-mono text-[10px] text-slate-700 bg-slate-100 border border-slate-200 px-1.5 py-0.2 rounded font-semibold">
              Right Alt
            </kbd>
          </div>
          <div 
            id="hud-preview-text"
            className="min-h-[30px] max-h-16 overflow-y-auto bg-slate-50 border border-slate-200 rounded p-2 text-xs text-slate-800 font-mono leading-relaxed"
          >
            {previewText || (
              <span className="text-slate-400 italic">
                {isListening ? 'Listening for speech...' : 'Press [Right Alt] or click mic to dictate.'}
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
