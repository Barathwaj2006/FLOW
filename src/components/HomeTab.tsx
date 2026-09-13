import React, { useState } from 'react';
import { DictationEntry, SessionState } from '../types';
import { Play, Copy, Bookmark, MoreVertical, Search, Check, Volume2, X, Mic, MicOff, Sparkles, CheckCircle2, RotateCcw } from 'lucide-react';

interface DictationTabProps {
  entries: DictationEntry[];
  onCopyTranscript: (text: string) => void;
  onInsertTranscript?: (text: string) => void;
  onToggleFavorite?: (id: string) => void;
  onDeleteEntry?: (id: string) => void;
  sessionState?: SessionState;
  onStartDictation?: () => void;
  onStopDictation?: () => void;
  audioLevel?: number;
  showFloatingHud?: boolean;
  onToggleFloatingHud?: () => void;
}

export const HomeTab: React.FC<DictationTabProps> = ({
  entries,
  onCopyTranscript,
  onInsertTranscript,
  onToggleFavorite,
  onDeleteEntry,
  sessionState = 'idle',
  onStartDictation,
  onStopDictation,
  audioLevel = 0,
  showFloatingHud = true,
  onToggleFloatingHud,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [showSearchInput, setShowSearchInput] = useState(false);
  const [bannerVisible, setBannerVisible] = useState(true);
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [insertedId, setInsertedId] = useState<string | null>(null);
  const [playingId, setPlayingId] = useState<string | null>(null);
  const [dismissedEntries, setDismissedEntries] = useState<string[]>([]);
  const [showActionMenuId, setShowActionMenuId] = useState<string | null>(null);

  const isListening = sessionState === 'listening';

  const handleCopy = (text: string, id: string) => {
    onCopyTranscript(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const handleInsert = (text: string, id: string) => {
    if (onInsertTranscript) {
      onInsertTranscript(text);
      setInsertedId(id);
      setTimeout(() => setInsertedId(null), 1500);
    }
  };

  const handlePlayAudio = (text: string, id: string) => {
    if ('speechSynthesis' in window) {
      window.speechSynthesis.cancel();
      if (playingId === id) {
        setPlayingId(null);
        return;
      }
      setPlayingId(id);
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.rate = 1.05;
      utterance.onend = () => setPlayingId(null);
      utterance.onerror = () => setPlayingId(null);
      window.speechSynthesis.speak(utterance);
    }
  };

  const handleRecover = (id: string) => {
    setDismissedEntries(prev => prev.filter(item => item !== id));
  };

  const handleDismiss = (id: string) => {
    setDismissedEntries(prev => [...prev, id]);
    setShowActionMenuId(null);
  };

  // Filtered transcript entries
  const filteredEntries = entries.filter(e => {
    if (!searchQuery.trim()) return true;
    return e.text.toLowerCase().includes(searchQuery.toLowerCase());
  });

  // Calculate live stats
  const totalWords = entries.reduce((acc, curr) => acc + (curr.wordCount || curr.text.split(/\s+/).length), 0);
  const totalWordsFormatted = totalWords > 1000 ? `${(totalWords / 1000).toFixed(1)}K` : totalWords.toString();

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-7 select-none pb-24 font-sans">
      {/* 1. Header Greeting & Quick Dictation Action */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-[26px] font-bold text-[#0f172a] tracking-tight">
            Welcome back, Barathwaj
          </h1>
          <p className="text-xs text-[#64748b] mt-0.5">
            Voice productivity engine online • Local-first Whisper INT8 • Zero-Enter invariant
          </p>
        </div>

        {/* Quick Dictate Button in view */}
        <div className="flex items-center gap-2">
          {onToggleFloatingHud && (
            <button
              onClick={onToggleFloatingHud}
              className={`px-3 py-1.5 rounded-xl text-xs font-medium border transition-colors ${
                showFloatingHud 
                  ? 'bg-slate-100 border-slate-300 text-slate-700' 
                  : 'bg-white border-slate-200 text-slate-500 hover:bg-slate-50'
              }`}
              title="Toggle floating glass capsule bar"
            >
              {showFloatingHud ? 'Hide Glass Bar' : 'Show Glass Bar'}
            </button>
          )}

          {onStartDictation && onStopDictation && (
            <button
              id="btn-main-dictate-trigger"
              onClick={isListening ? onStopDictation : onStartDictation}
              className={`px-4 py-2 rounded-xl text-xs font-semibold flex items-center gap-2 transition-all shadow-sm ${
                isListening
                  ? 'bg-rose-600 text-white animate-pulse hover:bg-rose-700 ring-2 ring-rose-300'
                  : 'bg-[#4f46e5] hover:bg-[#4338ca] text-white'
              }`}
            >
              {isListening ? (
                <>
                  <MicOff className="w-4 h-4" />
                  <span>Stop Dictating ({audioLevel}%)</span>
                </>
              ) : (
                <>
                  <Mic className="w-4 h-4" />
                  <span>Start Dictation</span>
                </>
              )}
            </button>
          )}
        </div>
      </div>

      {/* 2. Top Split Row: Banner Card + Stats Profile Card */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-stretch">
        {/* Banner: Original Sapphire & Indigo Gradient */}
        {bannerVisible ? (
          <div className="lg:col-span-8 relative rounded-2xl overflow-hidden shadow-xs min-h-[190px] flex flex-col justify-between p-7 text-white group">
            {/* Original modern gradient backdrop */}
            <div 
              className="absolute inset-0 bg-cover bg-center"
              style={{
                backgroundImage: `radial-gradient(ellipse at 80% 30%, rgba(6, 182, 212, 0.45), transparent 60%),
                                  radial-gradient(circle at 15% 85%, rgba(99, 102, 241, 0.5), transparent 65%),
                                  linear-gradient(125deg, #0f172a 0%, #1e1b4b 45%, #1e293b 100%)`
              }}
            >
              <div className="absolute inset-0 bg-black/20 backdrop-blur-[1px]" />
            </div>

            {/* Close banner button */}
            <button
              onClick={() => setBannerVisible(false)}
              className="absolute top-4 right-4 w-7 h-7 rounded-full bg-black/35 hover:bg-black/60 flex items-center justify-center text-white/80 hover:text-white transition-colors z-10"
              title="Dismiss banner"
            >
              <X className="w-4 h-4 stroke-[2.5]" />
            </button>

            {/* Banner Text Content */}
            <div className="relative z-10 max-w-lg space-y-1.5">
              <h2 className="text-[25px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
                Working around other people?
              </h2>
              <p className="text-[14px] text-white/90 font-normal leading-relaxed">
                With the right setup, you can dictate fluently without disrupting your neighbors
              </p>
            </div>

            {/* Banner Action Pill Button */}
            <div className="relative z-10 mt-4 flex items-center gap-3">
              <button 
                onClick={() => {
                  alert('Tip: Use directional headsets or whisper mode with FLOW offline inference to dictate cleanly in open offices without audio leakage.');
                }}
                className="px-5 py-2 rounded-full bg-white text-[#0f172a] text-[13.5px] font-semibold hover:bg-slate-100 active:scale-98 transition-all shadow-xs"
              >
                Show me how
              </button>
              <span className="text-xs text-white/70 font-mono">Whisper INT8 • Offline</span>
            </div>
          </div>
        ) : (
          <div className="lg:col-span-8 p-6 rounded-2xl border border-dashed border-[#cbd5e1] flex items-center justify-between text-sm text-[#64748b] bg-slate-50/50">
            <span>Tip banner hidden.</span>
            <button 
              onClick={() => setBannerVisible(true)} 
              className="text-[#0f172a] font-semibold underline text-xs hover:text-[#4f46e5]"
            >
              Show again
            </button>
          </div>
        )}

        {/* Right Stats & Voice Profile Card */}
        <div className="lg:col-span-4 bg-[#f8fafc] border border-[#e2e8f0] rounded-2xl p-5 flex flex-col justify-between shadow-2xs">
          {/* Numbers grid */}
          <div className="space-y-3">
            <div className="flex items-baseline gap-2">
              <span className="text-[32px] font-serif-editorial font-bold text-[#0f172a] leading-none">
                {totalWordsFormatted}
              </span>
              <span className="text-[13px] text-[#64748b] font-normal">
                total words
              </span>
            </div>

            <div className="flex items-baseline gap-2">
              <span className="text-[26px] font-serif-editorial font-bold text-[#0f172a] leading-none">
                134
              </span>
              <span className="text-[13px] text-[#64748b] font-normal">
                wpm
              </span>
            </div>

            <div className="flex items-baseline gap-2">
              <span className="text-[26px] font-serif-editorial font-bold text-[#0f172a] leading-none">
                1
              </span>
              <span className="text-[13px] text-[#64748b] font-normal">
                day streak
              </span>
            </div>
          </div>

          {/* Voice Profile Row */}
          <div className="pt-4 mt-4 border-t border-[#e2e8f0] flex items-center justify-between">
            <div className="flex flex-col">
              <span className="text-[10.5px] font-bold uppercase tracking-wider text-[#94a3b8]">
                Voice Profile
              </span>
              <span className="text-[14px] font-bold text-[#0f172a] mt-0.5">
                Connectivity Architect
              </span>
            </div>

            {/* Original Modern Avatar Graphic */}
            <div className="relative w-12 h-12 shrink-0">
              <div className="w-12 h-12 rounded-2xl bg-gradient-to-tr from-[#6366f1] via-[#06b6d4] to-[#10b981] p-0.5 shadow-sm flex items-center justify-center">
                <div className="w-full h-full rounded-[14px] bg-[#0f172a] flex items-center justify-center relative overflow-hidden">
                  {/* Modern voice nodes */}
                  <div className="absolute inset-0 bg-gradient-to-b from-indigo-500/20 to-transparent" />
                  <Sparkles className="w-5 h-5 text-cyan-400 z-10" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 3. Dictation History Section */}
      <div className="space-y-3 pt-2">
        {/* Section Header with TODAY and Search icon */}
        <div className="flex items-center justify-between py-1">
          <span className="text-[12px] font-bold uppercase tracking-wider text-[#94a3b8]">
            Today
          </span>

          <div className="flex items-center gap-2">
            {showSearchInput && (
              <input
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Search transcripts..."
                autoFocus
                className="px-3 py-1 text-xs rounded-lg bg-[#f1f5f9] border border-[#cbd5e1] text-[#0f172a] focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
              />
            )}
            <button
              onClick={() => setShowSearchInput(!showSearchInput)}
              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0] transition-colors"
              title="Search transcripts"
            >
              <Search className="w-4 h-4 stroke-[2]" />
            </button>
          </div>
        </div>

        {/* Rows of Dictation Transcripts */}
        <div className="divide-y divide-[#f1f5f9] border-t border-[#f1f5f9]">
          {/* Dismissed row placeholder */}
          {dismissedEntries.map(dId => (
            <div key={dId} className="py-4.5 flex items-start gap-8 group bg-slate-50/50 -mx-2 px-2 rounded-xl">
              <span className="text-[13px] text-[#94a3b8] w-16 shrink-0 pt-0.5 font-normal">
                Dismissed
              </span>
              <div className="flex-1 flex items-center justify-between text-xs text-[#64748b]">
                <span>This transcription was dismissed.</span>
                <button 
                  onClick={() => handleRecover(dId)}
                  className="flex items-center gap-1 font-semibold text-[#4f46e5] hover:text-[#4338ca]"
                >
                  <RotateCcw className="w-3 h-3" />
                  <span>Recover</span>
                </button>
              </div>
            </div>
          ))}

          {/* Real Dictation entries */}
          {filteredEntries.map(entry => {
            if (dismissedEntries.includes(entry.id)) return null;

            return (
              <div 
                key={entry.id} 
                className="py-5 flex items-start gap-6 group hover:bg-[#f8fafc] -mx-3 px-3 rounded-xl transition-colors relative"
              >
                {/* Timestamp */}
                <span className="text-[13px] text-[#94a3b8] w-16 shrink-0 pt-0.5 font-normal">
                  {entry.timeShort || '4:31 pm'}
                </span>

                {/* Main Transcript Body */}
                <div className="flex-1 text-[14.5px] leading-relaxed text-[#1e293b] font-normal pr-4">
                  <p>{entry.text}</p>
                  <div className="flex items-center gap-3 mt-1.5 text-[11px] text-[#94a3b8]">
                    <span>{entry.appName || entry.application}</span>
                    <span>•</span>
                    <span>{entry.wordCount || entry.text.split(/\s+/).length} words</span>
                    {entry.zeroEnterGuaranteed && (
                      <>
                        <span>•</span>
                        <span className="text-emerald-600 font-mono">Zero-Enter ✓</span>
                      </>
                    )}
                  </div>
                </div>

                {/* Right Action Icons */}
                <div className="flex items-center gap-1.5 opacity-80 group-hover:opacity-100 shrink-0 pt-0.5">
                  <button
                    onClick={() => handlePlayAudio(entry.text, entry.id)}
                    className={`w-7 h-7 rounded-lg flex items-center justify-center transition-colors ${
                      playingId === entry.id
                        ? 'bg-[#0f172a] text-white'
                        : 'text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0]'
                    }`}
                    title={playingId === entry.id ? 'Stop audio' : 'Play audio preview'}
                  >
                    {playingId === entry.id ? (
                      <Volume2 className="w-3.5 h-3.5 stroke-[2.2] animate-pulse" />
                    ) : (
                      <Play className="w-3.5 h-3.5 stroke-[2.2] fill-current" />
                    )}
                  </button>

                  <button
                    onClick={() => handleCopy(entry.text, entry.id)}
                    className="w-7 h-7 rounded-lg flex items-center justify-center text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0] transition-colors"
                    title="Copy transcript"
                  >
                    {copiedId === entry.id ? (
                      <Check className="w-3.5 h-3.5 text-emerald-600 stroke-[2.5]" />
                    ) : (
                      <Copy className="w-3.5 h-3.5 stroke-[2]" />
                    )}
                  </button>

                  {onInsertTranscript && (
                    <button
                      onClick={() => handleInsert(entry.text, entry.id)}
                      className="w-7 h-7 rounded-lg flex items-center justify-center text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0] transition-colors"
                      title="Insert into active cursor (Zero Enter)"
                    >
                      {insertedId === entry.id ? (
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 stroke-[2.5]" />
                      ) : (
                        <span className="text-[11px] font-bold">↵</span>
                      )}
                    </button>
                  )}

                  <button
                    onClick={() => onToggleFavorite && onToggleFavorite(entry.id)}
                    className={`w-7 h-7 rounded-lg flex items-center justify-center transition-colors ${
                      entry.isFavorite
                        ? 'text-amber-500'
                        : 'text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0]'
                    }`}
                    title="Bookmark / Star"
                  >
                    <Bookmark className={`w-3.5 h-3.5 stroke-[2] ${entry.isFavorite ? 'fill-current' : ''}`} />
                  </button>

                  <div className="relative">
                    <button
                      onClick={() => setShowActionMenuId(showActionMenuId === entry.id ? null : entry.id)}
                      className="w-7 h-7 rounded-lg flex items-center justify-center text-[#64748b] hover:text-[#0f172a] hover:bg-[#e2e8f0] transition-colors"
                      title="More options"
                    >
                      <MoreVertical className="w-3.5 h-3.5 stroke-[2]" />
                    </button>

                    {showActionMenuId === entry.id && (
                      <div className="absolute right-0 top-8 w-36 bg-white rounded-xl shadow-lg border border-slate-200 py-1.5 z-20 animate-in fade-in">
                        <button
                          onClick={() => handleDismiss(entry.id)}
                          className="w-full text-left px-3 py-1.5 text-xs text-slate-700 hover:bg-slate-100 flex items-center gap-2"
                        >
                          <span>Dismiss</span>
                        </button>
                        {onDeleteEntry && (
                          <button
                            onClick={() => {
                              onDeleteEntry(entry.id);
                              setShowActionMenuId(null);
                            }}
                            className="w-full text-left px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 flex items-center gap-2"
                          >
                            <span>Delete permanently</span>
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
