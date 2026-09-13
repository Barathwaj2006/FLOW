import React, { useState } from 'react';
import { DictationEntry, SessionState } from '../types';

interface HomeTabProps {
  sessionState: SessionState;
  onStartDictation: () => void;
  onStopDictation: () => void;
  audioLevel: number;
  lastTranscript: DictationEntry | null;
  onCopyTranscript: (text: string) => void;
  onInsertTranscript: (text: string) => void;
  recentEntries: DictationEntry[];
  onNavigateToHistory: () => void;
  onToggleFavoriteHistory?: (id: string) => void;
  activeMic: string;
  selectedLanguage: string;
  onLanguageChange: (lang: string) => void;
  onOpenSettings?: () => void;
  showFloatingHud: boolean;
  onToggleFloatingHud: () => void;
}

export const HomeTab: React.FC<HomeTabProps> = ({
  sessionState,
  onStartDictation,
  onStopDictation,
  audioLevel,
  lastTranscript,
  onCopyTranscript,
  onInsertTranscript,
  recentEntries,
  onNavigateToHistory,
  onToggleFavoriteHistory,
  activeMic,
  selectedLanguage,
  onLanguageChange,
  onOpenSettings,
  showFloatingHud,
  onToggleFloatingHud,
}) => {
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [lastCopied, setLastCopied] = useState(false);

  const isRecording = sessionState === 'listening';
  const isProcessing = sessionState === 'processing';

  const handleCopyLast = () => {
    if (!lastTranscript?.text) return;
    onCopyTranscript(lastTranscript.text);
    setLastCopied(true);
    setTimeout(() => setLastCopied(false), 1800);
  };

  const handleCopyEntry = (text: string, id: string) => {
    onCopyTranscript(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const languages = [
    { code: 'auto', name: 'Auto-Detect' },
    { code: 'en-US', name: 'English (US)' },
    { code: 'en-GB', name: 'English (UK)' },
    { code: 'ta-IN', name: 'Tamil (தமிழ்)' },
    { code: 'hi-IN', name: 'Hindi (हिन्दी)' },
    { code: 'es-ES', name: 'Spanish (Español)' },
    { code: 'fr-FR', name: 'French (Français)' },
    { code: 'de-DE', name: 'German (Deutsch)' },
  ];

  return (
    <div id="home-view" className="flex flex-col w-full pb-16 space-y-6 pt-1 select-none max-w-5xl mx-auto">
      {/* 1. Primary Status & Main Hero */}
      <section className="relative w-full rounded-3xl bg-white p-6 md:p-8 border border-slate-200 shadow-xs overflow-hidden">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="flex flex-col space-y-3">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-2xl bg-[#0284c7] flex items-center justify-center text-white shadow-xs">
                <span className="material-symbols-outlined text-[24px]">graphic_eq</span>
              </div>
              <div className="flex items-center gap-2.5">
                <h1 className="text-2xl font-bold text-slate-900 tracking-tight">FLOW</h1>
                <div className={`flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ${
                  isRecording
                    ? 'bg-rose-50 text-rose-700 border border-rose-200'
                    : isProcessing
                    ? 'bg-amber-50 text-amber-700 border border-amber-200'
                    : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                }`}>
                  <span className={`w-2 h-2 rounded-full ${
                    isRecording ? 'bg-rose-500 animate-ping' :
                    isProcessing ? 'bg-amber-500 animate-spin' :
                    'bg-emerald-500'
                  }`}></span>
                  <span>{isRecording ? 'Listening...' : isProcessing ? 'Transcribing...' : 'Ready'}</span>
                </div>
              </div>
            </div>

            <p className="text-sm text-slate-600 leading-relaxed max-w-xl">
              Voice dictation everywhere you work in Windows. Press or hold your shortcut to speak into any active window.
            </p>

            {/* Global Shortcut Guidance */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
              <div className="flex items-center gap-3 p-3.5 rounded-2xl bg-slate-50 border border-slate-200">
                <kbd className="px-2.5 py-1.5 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs whitespace-nowrap">
                  Alt + Space
                </kbd>
                <div className="flex flex-col">
                  <span className="text-xs font-bold text-slate-900">Hold to speak</span>
                  <span className="text-[11px] text-slate-500">Record while held, release to insert</span>
                </div>
              </div>

              <div className="flex items-center gap-3 p-3.5 rounded-2xl bg-slate-50 border border-slate-200">
                <kbd className="px-2.5 py-1.5 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs whitespace-nowrap">
                  Alt + B
                </kbd>
                <div className="flex flex-col">
                  <span className="text-xs font-bold text-slate-900">Toggle recording</span>
                  <span className="text-[11px] text-slate-500">Press once to start, press again to stop</span>
                </div>
              </div>
            </div>
          </div>

          {/* Interactive Microphone Button */}
          <div className="flex flex-col items-center justify-center p-5 bg-slate-50/80 border border-slate-200 rounded-3xl shrink-0 self-start md:self-center gap-2.5">
            <button
              id="hero-mic-trigger"
              onClick={() => {
                if (isRecording) onStopDictation();
                else onStartDictation();
              }}
              className={`w-18 h-18 rounded-2xl flex items-center justify-center transition-all duration-200 shadow-md ${
                isRecording
                  ? 'bg-rose-600 hover:bg-rose-700 text-white shadow-rose-600/30 scale-105 animate-pulse'
                  : isProcessing
                  ? 'bg-amber-600 text-white shadow-amber-600/30'
                  : 'bg-[#0284c7] hover:bg-[#0369a1] text-white shadow-[#0284c7]/25 hover:scale-102 active:scale-95'
              }`}
              title={isRecording ? 'Click to Stop' : 'Click to Speak (or use shortcuts)'}
            >
              <span className="material-symbols-outlined text-[36px]">
                {isRecording ? 'mic_off' : isProcessing ? 'hourglass_top' : 'mic'}
              </span>
            </button>
            <span className="text-xs font-bold text-slate-700 text-center">
              {isRecording ? 'Click to Stop' : isProcessing ? 'Transcribing...' : 'Click to Speak'}
            </span>
            {isRecording && (
              <div className="w-24 bg-slate-200 h-2 rounded-full overflow-hidden">
                <div 
                  className="bg-[#0284c7] h-full transition-all duration-75"
                  style={{ width: `${Math.max(12, audioLevel)}%` }}
                />
              </div>
            )}
          </div>
        </div>
      </section>

      {/* 2. System Status Bar (Section 20: Microphone, Language, Floating Bar) */}
      <section className="flex flex-wrap items-center justify-between gap-4 p-4 rounded-2xl bg-white border border-slate-200 shadow-xs">
        <div className="flex flex-wrap items-center gap-4">
          {/* Active Mic */}
          <div className="flex items-center gap-2 text-xs text-slate-700">
            <span className="material-symbols-outlined text-[18px] text-[#0284c7]">mic</span>
            <span className="text-slate-500 font-medium">Microphone:</span>
            <span className="font-semibold text-slate-900 truncate max-w-[200px]" title={activeMic}>
              {activeMic}
            </span>
          </div>

          <span className="text-slate-200 hidden sm:inline">•</span>

          {/* Language Selector */}
          <div className="flex items-center gap-2 text-xs text-slate-700">
            <span className="material-symbols-outlined text-[18px] text-slate-400">language</span>
            <span className="text-slate-500 font-medium">Language:</span>
            <select
              value={selectedLanguage}
              onChange={e => onLanguageChange(e.target.value)}
              className="bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1 text-xs text-slate-800 font-semibold focus:outline-none focus:border-[#0284c7] cursor-pointer"
            >
              {languages.map(l => (
                <option key={l.code} value={l.name}>
                  {l.name}
                </option>
              ))}
            </select>
          </div>

          <span className="text-slate-200 hidden md:inline">•</span>

          {/* Floating Bar Toggle */}
          <div className="flex items-center gap-2 text-xs">
            <span className="material-symbols-outlined text-[18px] text-slate-400">picture_in_picture_alt</span>
            <span className="text-slate-500 font-medium">Floating Bar:</span>
            <button
              id="btn-home-toggle-floating-bar"
              onClick={onToggleFloatingHud}
              className={`px-2.5 py-1 rounded-lg font-semibold text-xs transition border flex items-center gap-1.5 ${
                showFloatingHud
                  ? 'bg-sky-50 border-sky-200 text-[#0284c7]'
                  : 'bg-slate-100 border-slate-200 text-slate-600 hover:bg-slate-200'
              }`}
            >
              <span className={`w-1.5 h-1.5 rounded-full ${showFloatingHud ? 'bg-[#0284c7]' : 'bg-slate-400'}`}></span>
              <span>{showFloatingHud ? 'Enabled' : 'Disabled'}</span>
            </button>
          </div>
        </div>

        <div className="flex items-center gap-2 text-xs">
          <div className="flex items-center gap-1 px-2.5 py-1 rounded-lg bg-emerald-50 text-emerald-800 border border-emerald-200 font-medium">
            <span className="material-symbols-outlined text-[15px] text-emerald-600">shield</span>
            <span>Zero-Enter Safety Enforced</span>
          </div>

          {onOpenSettings && (
            <button
              onClick={onOpenSettings}
              className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition"
              title="Open Settings"
            >
              <span className="material-symbols-outlined text-[18px]">settings</span>
            </button>
          )}
        </div>
      </section>

      {/* 3. Last Transcript (Section 18 & 20) */}
      <section className="bg-white border border-slate-200 rounded-3xl p-6 shadow-xs space-y-4">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[20px] text-[#0284c7]">text_snippet</span>
            <h2 className="text-base font-bold text-slate-900">Last Transcript</h2>
            {lastTranscript && (
              <span className="text-xs text-slate-400 font-mono">
                • {lastTranscript.timeShort || lastTranscript.createdAt}
              </span>
            )}
          </div>

          {lastTranscript && (
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-mono text-slate-500 px-2 py-0.5 rounded bg-slate-100">
                {lastTranscript.wordCount} words
              </span>
              <button
                id="btn-copy-last-transcript"
                onClick={handleCopyLast}
                className="h-8 px-3 rounded-xl text-xs font-medium bg-slate-100 hover:bg-slate-200 text-slate-800 transition flex items-center gap-1.5 shadow-2xs"
                title="Copy transcript to clipboard"
              >
                <span className="material-symbols-outlined text-[16px] text-slate-600">
                  {lastCopied ? 'check' : 'content_copy'}
                </span>
                <span>{lastCopied ? 'Copied' : 'Copy'}</span>
              </button>

              <button
                id="btn-insert-last-transcript"
                onClick={() => onInsertTranscript(lastTranscript.text)}
                className="h-8 px-3.5 rounded-xl text-xs font-medium bg-[#0284c7] hover:bg-[#0369a1] text-white transition flex items-center gap-1.5 shadow-xs"
                title="Insert transcript into focused field (safe, no Enter)"
              >
                <span className="material-symbols-outlined text-[16px]">input</span>
                <span>Insert</span>
              </button>
            </div>
          )}
        </div>

        {/* Transcript Body */}
        {lastTranscript ? (
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 text-slate-900 text-sm font-mono leading-relaxed select-text shadow-inner">
            "{lastTranscript.text}"
          </div>
        ) : (
          <div className="p-6 rounded-2xl bg-slate-50/60 border border-dashed border-slate-200 text-center space-y-1.5 text-slate-400">
            <span className="material-symbols-outlined text-[28px] text-slate-300">record_voice_over</span>
            <p className="text-xs font-semibold text-slate-700">No transcript yet</p>
            <p className="text-xs text-slate-400 max-w-md mx-auto">
              Hold <kbd className="px-1.5 py-0.5 rounded bg-white border border-slate-300 font-mono text-slate-700">Alt + Space</kbd> or press <kbd className="px-1.5 py-0.5 rounded bg-white border border-slate-300 font-mono text-slate-700">Alt + B</kbd> to speak. Transcripts are preserved even if no text field is focused.
            </p>
          </div>
        )}

        <div className="flex items-center justify-between text-xs text-slate-500 pt-1">
          <div className="flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[16px] text-slate-400">check_circle</span>
            <span>Recorded independently of cursor position. Saved to History and Clipboard.</span>
          </div>
          <span className="text-[11px] font-mono text-slate-400">Never simulates Enter or Return</span>
        </div>
      </section>

      {/* 4. Recent Transcripts List (Clean history only, no fake metrics) */}
      <section className="bg-white border border-slate-200 rounded-3xl p-6 shadow-xs space-y-3">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[20px] text-[#0284c7]">history</span>
            <h2 className="text-base font-bold text-slate-900">Recent Transcripts</h2>
            <span className="text-xs text-slate-400 font-mono">({recentEntries.length})</span>
          </div>

          <button
            onClick={onNavigateToHistory}
            className="text-xs font-semibold text-[#0284c7] hover:text-[#0369a1] transition flex items-center gap-1"
          >
            <span>View all in History</span>
            <span className="material-symbols-outlined text-[15px]">arrow_forward</span>
          </button>
        </div>

        {recentEntries.length > 0 ? (
          <div className="space-y-2">
            {recentEntries.slice(0, 5).map(entry => (
              <div
                key={entry.id}
                className="group flex flex-col sm:flex-row sm:items-center justify-between p-3.5 rounded-2xl bg-slate-50/70 border border-slate-200 hover:border-[#0284c7]/40 hover:bg-white transition-all gap-3"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 text-xs text-slate-500 font-mono mb-1">
                    <span className="font-semibold text-slate-700">{entry.application || 'Windows Desktop'}</span>
                    <span>•</span>
                    <span>{entry.timeShort || entry.createdAt}</span>
                    <span>•</span>
                    <span>{entry.wordCount} words</span>
                  </div>
                  <p className="text-xs text-slate-800 font-normal truncate select-text">
                    "{entry.text}"
                  </p>
                </div>

                <div className="flex items-center gap-1.5 shrink-0 self-end sm:self-center">
                  <button
                    onClick={() => handleCopyEntry(entry.text, entry.id)}
                    className="h-7 px-2.5 rounded-lg bg-white border border-slate-200 hover:bg-slate-100 text-slate-700 text-xs font-medium transition flex items-center gap-1 shadow-2xs"
                    title="Copy to clipboard"
                  >
                    <span className="material-symbols-outlined text-[15px] text-slate-500">
                      {copiedId === entry.id ? 'check' : 'content_copy'}
                    </span>
                    <span>{copiedId === entry.id ? 'Copied' : 'Copy'}</span>
                  </button>

                  {onToggleFavoriteHistory && (
                    <button
                      onClick={() => onToggleFavoriteHistory(entry.id)}
                      className={`w-7 h-7 rounded-lg flex items-center justify-center transition ${
                        entry.isFavorite
                          ? 'text-amber-500 hover:bg-amber-50'
                          : 'text-slate-300 hover:text-slate-500 hover:bg-slate-100'
                      }`}
                      title="Star transcript"
                    >
                      <span
                        className="material-symbols-outlined text-[16px]"
                        style={{ fontVariationSettings: entry.isFavorite ? "'FILL' 1" : "'FILL' 0" }}
                      >
                        star
                      </span>
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-xs text-slate-400 py-4 text-center">
            No recent transcripts. Start speaking to see your dictations recorded here.
          </p>
        )}
      </section>
    </div>
  );
};
