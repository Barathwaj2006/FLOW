import React, { useState, useEffect } from 'react';
import { DictationEntry, ProductivityMetrics, SessionState } from '../types';

interface HomeTabProps {
  sessionState: SessionState;
  onStartDictation: () => void;
  onStopDictation: () => void;
  audioLevel: number;
  testText: string;
  setTestText: (text: string) => void;
  onSimulateDictation: (samplePhrase: string) => void;
  metrics: ProductivityMetrics;
  recentEntries: DictationEntry[];
  onNavigateToHistory: () => void;
  onToggleFavoriteHistory?: (id: string) => void;
  onOpenSettings?: () => void;
}

export const HomeTab: React.FC<HomeTabProps> = ({
  sessionState,
  onStartDictation,
  onStopDictation,
  audioLevel,
  testText,
  setTestText,
  onSimulateDictation,
  metrics,
  recentEntries,
  onNavigateToHistory,
  onToggleFavoriteHistory,
  onOpenSettings,
}) => {
  const [isTestModalOpen, setIsTestModalOpen] = useState(false);
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [isStreamPaused, setIsStreamPaused] = useState(false);
  const [selectedAppFilter, setSelectedAppFilter] = useState<string>('all');
  const [isAppFilterMenuOpen, setIsAppFilterMenuOpen] = useState(false);
  const [activeShortcutModal, setActiveShortcutModal] = useState(false);
  const [micSettingsModal, setMicSettingsModal] = useState(false);

  const isRecording = sessionState === 'listening';

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  // Filter entries
  const filteredEntries = recentEntries.filter(entry => {
    if (selectedAppFilter === 'all') return true;
    return entry.application.toLowerCase().includes(selectedAppFilter.toLowerCase());
  });

  const todayEntries = filteredEntries.filter(e => e.createdAt.toLowerCase().includes('today') || e.createdAt.includes('2:18') || e.createdAt.includes('1:42') || e.createdAt.includes('11:05'));
  const yesterdayEntries = filteredEntries.filter(e => e.createdAt.toLowerCase().includes('yesterday') || e.createdAt.includes('4:48') || e.createdAt.includes('2:10'));

  return (
    <div id="home-view" className="flex flex-col w-full pb-16 space-y-6 pt-1 select-none">
      {/* 1. Top Command & Ambient Hub Header */}
      <section className="relative w-full rounded-xl bg-white p-6 md:p-8 border border-[#e2e8f0] shadow-xs overflow-hidden">
        <div className="absolute -right-20 -top-20 w-96 h-96 rounded-full bg-sky-100/40 blur-3xl pointer-events-none"></div>
        <div className="absolute -left-12 -bottom-12 w-64 h-64 rounded-full bg-sky-50/50 blur-2xl pointer-events-none"></div>

        <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
          <div className="flex flex-col space-y-1 max-w-2xl">
            <div className="flex items-center gap-2">
              <span className="text-[11px] uppercase tracking-widest text-[#0284c7] font-bold">
                Local Neural Acoustic Layer
              </span>
              <span className="text-[#94a3b8] text-[8px]">•</span>
              <span className="font-mono text-xs text-[#64748b]">Whisper-Engine.v3.1.bin</span>
            </div>

            <h1 className="text-3xl font-bold text-[#0f172a] tracking-tight">FLOW</h1>
            <p className="text-[15px] text-[#475569] leading-relaxed">
              Voice productivity, anywhere you type. Zero cloud reliance, uncompromising speed.
            </p>

            {/* Status Row Pills */}
            <div className="flex flex-wrap items-center gap-2 pt-2">
              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-emerald-50 border border-emerald-200 text-emerald-800">
                <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                <span className="text-xs font-semibold">Ready</span>
              </div>

              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#f8fafc] border border-[#e2e8f0] text-slate-700">
                <span className="material-symbols-outlined text-[15px] text-[#0284c7]">mic</span>
                <span className="text-xs truncate max-w-xs font-medium">Default Microphone (Realtek Audio)</span>
              </div>

              <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#f8fafc] border border-[#e2e8f0] text-slate-700">
                <span className="text-[11px] uppercase text-slate-500 font-semibold">Trigger:</span>
                <kbd className="px-1.5 py-0.5 rounded bg-white border border-[#e2e8f0] text-[#0284c7] font-mono text-xs shadow-xs font-semibold">
                  Right Alt
                </kbd>
              </div>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex flex-wrap lg:flex-col xl:flex-row items-center gap-2.5 self-start lg:self-center">
            <button 
              id="open-test-modal"
              onClick={() => setIsTestModalOpen(true)}
              className="flex items-center gap-1.5 px-4 py-2 rounded bg-[#0284c7] hover:bg-[#0369a1] text-white text-xs font-medium shadow-sm transition-all active:scale-[0.98]"
            >
              <span className="material-symbols-outlined text-[18px]">record_voice_over</span>
              <span>Test dictation</span>
            </button>

            <button 
              id="btn-change-shortcut"
              onClick={() => setActiveShortcutModal(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded bg-white border border-[#e2e8f0] hover:bg-slate-50 text-slate-700 text-xs font-medium shadow-xs transition-colors"
            >
              <span className="material-symbols-outlined text-[18px] text-slate-500">keyboard</span>
              <span>Change shortcut</span>
            </button>

            <button 
              id="btn-mic-settings"
              onClick={() => setMicSettingsModal(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded bg-white border border-[#e2e8f0] hover:bg-slate-50 text-slate-700 text-xs font-medium shadow-xs transition-colors"
            >
              <span className="material-symbols-outlined text-[18px] text-slate-500">tune</span>
              <span>Microphone settings</span>
            </button>
          </div>
        </div>

        {/* Interactive Desktop Floating Flow Bar Preview Widget */}
        <div className="mt-6 pt-4 border-t border-slate-100">
          <div className="flex items-center justify-between pb-2">
            <span className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold">
              Desktop HUD Preview (Current Live Overlay)
            </span>
            <span className="font-mono text-slate-400 text-[11px]">
              Hover or hold Right Alt to engage
            </span>
          </div>

          <div 
            id="flow-bar-preview"
            onClick={() => {
              if (isRecording) onStopDictation();
              else onStartDictation();
            }}
            className="relative w-full p-3 rounded-xl bg-slate-50/90 border border-slate-200 flex flex-wrap items-center justify-between gap-3 shadow-xs cursor-pointer transition-all duration-300 hover:border-sky-300 hover:scale-[1.004]"
          >
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-md bg-[#0284c7] flex items-center justify-center text-white shadow-xs">
                <span className="material-symbols-outlined text-[18px]">mic</span>
              </div>
              <div className="flex flex-col">
                <div className="flex items-center gap-1.5">
                  <span className="text-xs text-slate-900 font-bold">FLOW BAR</span>
                  <span className="px-1.5 py-0.5 rounded bg-sky-100 border border-sky-200 text-[#0284c7] font-mono text-[10px] font-semibold">
                    ACTIVE HUD
                  </span>
                </div>
                <span className="text-xs text-slate-600">
                  Listening for keypress: <kbd className="text-[#0284c7] font-mono font-semibold">Right Alt</kbd>
                </span>
              </div>
            </div>

            {/* Waveform Simulation */}
            <div className="flex items-center gap-[3px] px-3 py-1.5 rounded-md bg-white border border-slate-200 h-9">
              <span className="w-[3px] h-2 bg-[#0284c7] rounded-full animate-pulse"></span>
              <span className="w-[3px] h-4 bg-sky-500 rounded-full animate-pulse" style={{ animationDelay: '150ms' }}></span>
              <span className="w-[3px] h-6 bg-sky-400 rounded-full animate-pulse" style={{ animationDelay: '300ms' }}></span>
              <span className="w-[3px] h-3 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '75ms' }}></span>
              <span className="w-[3px] h-7 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '200ms' }}></span>
              <span className="w-[3px] h-5 bg-sky-600 rounded-full animate-pulse" style={{ animationDelay: '350ms' }}></span>
              <span className="w-[3px] h-2 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '100ms' }}></span>
              <span className="w-[3px] h-4 bg-sky-400 rounded-full animate-pulse" style={{ animationDelay: '250ms' }}></span>
              <span className="w-[3px] h-6 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '180ms' }}></span>
              <span className="w-[3px] h-3 bg-[#0284c7] rounded-full"></span>
              <span className="w-[3px] h-1.5 bg-slate-300 rounded-full"></span>
              <span className="w-[3px] h-1.5 bg-slate-300 rounded-full"></span>
            </div>

            {/* Mode Indicator & Shortcut Pill */}
            <div className="flex items-center gap-2">
              <div className="px-2.5 py-1 rounded bg-white border border-slate-200 text-slate-700 text-xs">
                Mode: <span className="text-slate-900 font-semibold">Smart Polish</span>
              </div>
              <div className="flex items-center gap-1 px-2 py-1 rounded bg-white border border-slate-200 text-slate-500 text-[11px] font-mono">
                <span className="material-symbols-outlined text-[13px]">lock</span>
                <span>Local Buffer</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 2. Metrics Grid & Privacy Signal */}
      <section className="flex flex-col space-y-3">
        {/* Local Privacy Badge Pill */}
        <div className="flex flex-wrap items-center justify-between gap-2 p-3 rounded-lg bg-white border border-[#e2e8f0] shadow-xs">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">verified_user</span>
            <span className="text-[13px] text-slate-900 font-semibold">On-Device Speech Model</span>
            <span className="text-slate-400 text-xs">•</span>
            <span className="text-xs text-slate-600">0 ms cloud latency • 100% Private local telemetry</span>
          </div>
          <div className="flex items-center gap-2 text-slate-500 font-mono text-xs">
            <span>RAM Footprint: 214 MB</span>
            <span>•</span>
            <span>AVX-512 Native</span>
          </div>
        </div>

        {/* 4 Bento Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {/* Card 1: Dictated Today */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-[#e2e8f0] hover:border-sky-300 transition-colors shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold">Dictated Today</span>
              <div className="w-7 h-7 rounded bg-sky-50 border border-sky-100 flex items-center justify-center text-[#0284c7]">
                <span className="material-symbols-outlined text-[16px]">notes</span>
              </div>
            </div>
            <div className="my-2">
              <span className="text-3xl font-bold text-slate-900 tracking-tight">1,428</span>
              <span className="text-xs text-slate-500 ml-1">words</span>
            </div>
            <div className="flex items-center gap-1 text-emerald-600 text-xs font-medium">
              <span className="material-symbols-outlined text-[16px]">trending_up</span>
              <span>+18% vs 7-day avg</span>
            </div>
          </div>

          {/* Card 2: Average Pace */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-[#e2e8f0] hover:border-sky-300 transition-colors shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold">Average Pace</span>
              <div className="w-7 h-7 rounded bg-sky-50 border border-sky-100 flex items-center justify-center text-[#0284c7]">
                <span className="material-symbols-outlined text-[16px]">speed</span>
              </div>
            </div>
            <div className="my-2">
              <span className="text-3xl font-bold text-slate-900 tracking-tight">142</span>
              <span className="text-xs text-slate-500 ml-1">WPM</span>
            </div>
            <div className="flex items-center gap-1 text-[#0284c7] text-xs font-medium">
              <span className="material-symbols-outlined text-[16px]">bolt</span>
              <span>3.2x faster than typing</span>
            </div>
          </div>

          {/* Card 3: Active Sessions */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-[#e2e8f0] hover:border-sky-300 transition-colors shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold">Active Sessions</span>
              <div className="w-7 h-7 rounded bg-slate-100 border border-slate-200 flex items-center justify-center text-slate-600">
                <span className="material-symbols-outlined text-[16px]">spatial_audio</span>
              </div>
            </div>
            <div className="my-2">
              <span className="text-3xl font-bold text-slate-900 tracking-tight">24</span>
              <span className="text-xs text-slate-500 ml-1">invocations</span>
            </div>
            <div className="flex items-center gap-1 text-slate-500 text-xs">
              <span className="material-symbols-outlined text-[16px]">schedule</span>
              <span>Avg session: 3.4s</span>
            </div>
          </div>

          {/* Card 4: Productivity Streak */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-[#e2e8f0] hover:border-sky-300 transition-colors shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold">Productivity Streak</span>
              <div className="w-7 h-7 rounded bg-amber-50 border border-amber-200 flex items-center justify-center text-amber-600">
                <span className="material-symbols-outlined text-[16px]">local_fire_department</span>
              </div>
            </div>
            <div className="my-2">
              <span className="text-3xl font-bold text-slate-900 tracking-tight">12</span>
              <span className="text-xs text-slate-500 ml-1">consecutive days</span>
            </div>
            <div className="flex items-center gap-1 text-amber-600 text-xs font-medium">
              <span className="material-symbols-outlined text-[16px]">military_tech</span>
              <span>Personal best record</span>
            </div>
          </div>
        </div>
      </section>

      {/* 3. Live Sparkline & Real-Time Throughput Ratio Graph */}
      <section className="p-5 rounded-xl bg-white border border-[#e2e8f0] shadow-xs flex flex-col md:flex-row items-center justify-between gap-6">
        <div className="flex flex-col max-w-md">
          <span className="text-[11px] uppercase tracking-wider text-slate-400 font-semibold">Throughput Efficiency</span>
          <h3 className="text-base font-bold text-slate-900 mt-0.5">Dictation vs Traditional Keypress Input</h3>
          <p className="text-xs text-slate-600 mt-1">
            Based on telemetry across Slack, VS Code, and browser documents over today's 24 active runs.
          </p>
        </div>

        {/* SVG Vector Visualization */}
        <div className="w-full md:w-80 h-16 flex items-center justify-center">
          <svg className="w-full h-full text-[#0284c7]" fill="none" viewBox="0 0 320 60" xmlns="http://www.w3.org/2000/svg">
            <path d="M0 50 L40 45 L80 48 L120 35 L160 38 L200 20 L240 28 L280 12 L320 8" stroke="#0284c7" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5"></path>
            <path d="M0 50 L40 45 L80 48 L120 35 L160 38 L200 20 L240 28 L280 12 L320 8 V60 H0 Z" fill="#0284c7" fillOpacity="0.08"></path>
            <line stroke="#cbd5e1" strokeDasharray="4 4" strokeWidth="1.5" x1="0" x2="320" y1="46" y2="46"></line>
            <circle cx="320" cy="8" fill="#0284c7" r="4"></circle>
          </svg>
        </div>

        <div className="flex items-center gap-6">
          <div className="flex flex-col items-end">
            <span className="text-[11px] text-slate-400 uppercase font-semibold">Speed Delta</span>
            <span className="text-lg text-[#0284c7] font-bold">+98 WPM</span>
          </div>
          <div className="flex flex-col items-end">
            <span className="text-[11px] text-slate-400 uppercase font-semibold">Est. Time Saved</span>
            <span className="text-lg text-slate-800 font-bold">42 mins</span>
          </div>
        </div>
      </section>

      {/* 4. Recent Dictation History Section */}
      <section className="flex flex-col space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[#0284c7] text-[20px]">history</span>
            <h2 className="text-lg text-slate-900 font-bold">Recent Transcripts</h2>
            <span className="px-2 py-0.5 rounded bg-slate-100 border border-slate-200 text-slate-600 font-mono text-xs font-medium">
              {filteredEntries.length} entries
            </span>
          </div>

          <div className="flex items-center gap-2">
            <button 
              onClick={onNavigateToHistory}
              className="px-2.5 py-1 rounded bg-white border border-[#e2e8f0] text-slate-600 hover:text-slate-900 hover:bg-slate-50 text-xs font-medium transition-colors shadow-xs"
            >
              View all
            </button>
            
            <div className="relative">
              <button 
                onClick={() => setIsAppFilterMenuOpen(!isAppFilterMenuOpen)}
                className="px-2.5 py-1 rounded bg-white border border-[#e2e8f0] text-slate-600 hover:text-slate-900 hover:bg-slate-50 text-xs font-medium transition-colors flex items-center gap-1 shadow-xs"
              >
                <span className="material-symbols-outlined text-[14px]">filter_list</span>
                <span className="capitalize">{selectedAppFilter === 'all' ? 'All Apps' : selectedAppFilter}</span>
              </button>

              {isAppFilterMenuOpen && (
                <div className="absolute right-0 mt-1 w-40 bg-white border border-[#e2e8f0] rounded-lg shadow-lg z-30 py-1 text-xs text-slate-700">
                  {['all', 'Teams', 'VS Code', 'Slack', 'Word', 'Chrome', 'Outlook'].map(app => (
                    <button
                      key={app}
                      onClick={() => {
                        setSelectedAppFilter(app);
                        setIsAppFilterMenuOpen(false);
                      }}
                      className="w-full text-left px-3 py-1.5 hover:bg-slate-50 capitalize"
                    >
                      {app === 'all' ? 'All Applications' : app}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Group: TODAY */}
        {todayEntries.length > 0 && (
          <div className="flex flex-col space-y-2">
            <div className="flex items-center gap-2 px-1 py-1">
              <span className="text-[11px] uppercase tracking-widest text-[#0284c7] font-bold">Today</span>
              <div className="h-[1px] flex-1 bg-[#e2e8f0]"></div>
            </div>

            {todayEntries.map(entry => (
              <div 
                key={entry.id}
                className="group flex flex-col md:flex-row md:items-center justify-between p-3.5 rounded-lg bg-white border border-[#e2e8f0] hover:border-sky-300 transition-all gap-3 shadow-xs"
              >
                <div className="flex items-start gap-3 min-w-0 flex-1">
                  <div className="w-8 h-8 rounded bg-sky-50 border border-sky-100 flex items-center justify-center shrink-0 mt-0.5 text-[#0284c7]">
                    <span className="material-symbols-outlined text-[18px]">
                      {entry.application.includes('Slack') ? 'chat' :
                       entry.application.includes('Code') ? 'terminal' :
                       entry.application.includes('Teams') ? 'forum' : 'description'}
                    </span>
                  </div>
                  <div className="flex flex-col min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-xs text-slate-900 font-bold">{entry.application}</span>
                      <span className="text-slate-300 text-[10px]">•</span>
                      <span className="font-mono text-xs text-slate-500">{entry.timeShort || '11:42 AM'}</span>
                      <span className="px-1.5 py-0.2 rounded bg-slate-100 text-slate-500 font-mono text-[11px]">
                        {entry.wordCount} words
                      </span>
                      <span className="px-1.5 py-0.2 rounded bg-sky-50 border border-sky-200 text-[#0284c7] text-[10px] font-semibold">
                        {entry.mode}
                      </span>
                    </div>
                    <p className="text-xs text-slate-700 mt-1 truncate">
                      "{entry.text}"
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-1 self-end md:self-center shrink-0">
                  <button 
                    onClick={() => handleCopy(entry.text, entry.id)}
                    className="w-8 h-8 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
                    title="Copy to clipboard"
                  >
                    <span className="material-symbols-outlined text-[16px]">
                      {copiedId === entry.id ? 'check' : 'content_copy'}
                    </span>
                  </button>
                  <button 
                    onClick={() => onToggleFavoriteHistory && onToggleFavoriteHistory(entry.id)}
                    className={`w-8 h-8 rounded flex items-center justify-center transition-colors ${
                      entry.isFavorite ? 'text-amber-400 hover:bg-slate-100' : 'text-slate-300 hover:text-slate-500 hover:bg-slate-100'
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
                  <button 
                    onClick={onNavigateToHistory}
                    className="w-8 h-8 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors" 
                    title="Inspect transcript"
                  >
                    <span className="material-symbols-outlined text-[16px]">more_horiz</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Group: YESTERDAY */}
        {yesterdayEntries.length > 0 && (
          <div className="flex flex-col space-y-2 pt-2">
            <div className="flex items-center gap-2 px-1 py-1">
              <span className="text-[11px] uppercase tracking-widest text-slate-400 font-bold">Yesterday</span>
              <div className="h-[1px] flex-1 bg-[#e2e8f0]"></div>
            </div>

            {yesterdayEntries.map(entry => (
              <div 
                key={entry.id}
                className="group flex flex-col md:flex-row md:items-center justify-between p-3.5 rounded-lg bg-white border border-[#e2e8f0] hover:border-sky-300 transition-all gap-3 shadow-xs"
              >
                <div className="flex items-start gap-3 min-w-0 flex-1">
                  <div className="w-8 h-8 rounded bg-sky-50 border border-sky-100 flex items-center justify-center shrink-0 mt-0.5 text-[#0284c7]">
                    <span className="material-symbols-outlined text-[18px]">
                      {entry.application.includes('Word') ? 'description' : 'mail'}
                    </span>
                  </div>
                  <div className="flex flex-col min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-xs text-slate-900 font-bold">{entry.application}</span>
                      <span className="text-slate-300 text-[10px]">•</span>
                      <span className="font-mono text-xs text-slate-500">{entry.timeShort || 'Yesterday'}</span>
                      <span className="px-1.5 py-0.2 rounded bg-slate-100 text-slate-500 font-mono text-[11px]">
                        {entry.wordCount} words
                      </span>
                      <span className="px-1.5 py-0.2 rounded bg-sky-50 border border-sky-200 text-[#0284c7] text-[10px] font-semibold">
                        {entry.mode}
                      </span>
                    </div>
                    <p className="text-xs text-slate-700 mt-1 truncate">
                      "{entry.text}"
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-1 self-end md:self-center shrink-0">
                  <button 
                    onClick={() => handleCopy(entry.text, entry.id)}
                    className="w-8 h-8 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
                    title="Copy to clipboard"
                  >
                    <span className="material-symbols-outlined text-[16px]">
                      {copiedId === entry.id ? 'check' : 'content_copy'}
                    </span>
                  </button>
                  <button 
                    onClick={() => onToggleFavoriteHistory && onToggleFavoriteHistory(entry.id)}
                    className={`w-8 h-8 rounded flex items-center justify-center transition-colors ${
                      entry.isFavorite ? 'text-amber-400 hover:bg-slate-100' : 'text-slate-300 hover:text-slate-500 hover:bg-slate-100'
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
                  <button 
                    onClick={onNavigateToHistory}
                    className="w-8 h-8 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors" 
                    title="Inspect transcript"
                  >
                    <span className="material-symbols-outlined text-[16px]">more_horiz</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* 5. Interactive Test Dictation Floating Modal */}
      {isTestModalOpen && (
        <div 
          id="test-dictation-modal"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/30 backdrop-blur-sm animate-in fade-in duration-200"
          onClick={(e) => {
            if (e.target === e.currentTarget) setIsTestModalOpen(false);
          }}
        >
          <div className="relative w-full max-w-xl rounded-xl bg-white shadow-2xl border border-slate-200 p-6 flex flex-col space-y-4">
            {/* Modal Header */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className="w-2.5 h-2.5 rounded-full bg-[#0284c7] animate-ping"></div>
                <h3 className="text-base text-slate-900 font-bold">Test Dictation Buffer</h3>
              </div>
              <button 
                id="close-test-modal"
                onClick={() => setIsTestModalOpen(false)}
                className="w-7 h-7 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>

            {/* Real-time Live Sandbox Card */}
            <div className="flex flex-col space-y-2 p-4 rounded-lg bg-slate-50 border border-slate-200">
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-slate-500 uppercase font-semibold">
                  Real-Time Transcription Stream
                </span>
                <span className={`font-mono text-[11px] font-semibold ${
                  isStreamPaused ? 'text-slate-400' : 'text-[#0284c7]'
                }`}>
                  {isStreamPaused ? 'Stream Paused' : 'Microphone Active'}
                </span>
              </div>

              {/* Live Text Area */}
              <p className="text-[15px] text-slate-800 min-h-[100px] leading-relaxed select-text font-normal">
                {testText || '"Speak now to verify acoustic response and local transcription speed. The text will stream here word-by-word with instant formatting."'}
              </p>

              {/* Simulated Active Waveform in Modal */}
              <div className="flex items-center gap-[3px] h-6 py-1">
                <span className="w-1 h-2 bg-[#0284c7] rounded-full animate-pulse"></span>
                <span className="w-1 h-5 bg-sky-400 rounded-full animate-pulse" style={{ animationDelay: '100ms' }}></span>
                <span className="w-1 h-3 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '200ms' }}></span>
                <span className="w-1 h-6 bg-sky-600 rounded-full animate-pulse" style={{ animationDelay: '150ms' }}></span>
                <span className="w-1 h-2 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '300ms' }}></span>
                <span className="w-1 h-4 bg-sky-400 rounded-full animate-pulse" style={{ animationDelay: '50ms' }}></span>
                <span className="w-1 h-6 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '250ms' }}></span>
                <span className="w-1 h-1 bg-slate-300 rounded-full"></span>
              </div>
            </div>

            {/* Modal Footer Controls */}
            <div className="flex items-center justify-between pt-1">
              <div className="flex items-center gap-2 font-mono text-xs text-slate-500">
                <span>Latency: <span className="text-[#0284c7] font-bold">14ms</span></span>
                <span>•</span>
                <span>Engine: <span className="text-slate-800 font-semibold">Whisper INT8</span></span>
              </div>

              <div className="flex items-center gap-2">
                <button 
                  id="toggle-record-btn"
                  onClick={() => setIsStreamPaused(!isStreamPaused)}
                  className="px-3.5 py-1.5 rounded bg-[#0284c7] text-white text-xs font-medium hover:bg-[#0369a1] transition-colors flex items-center gap-1 shadow-xs"
                >
                  <span className="material-symbols-outlined text-[16px]">
                    {isStreamPaused ? 'play_arrow' : 'pause'}
                  </span>
                  <span>{isStreamPaused ? 'Resume Stream' : 'Pause Stream'}</span>
                </button>

                <button 
                  id="clear-buffer-btn"
                  onClick={() => setTestText('')}
                  className="px-3.5 py-1.5 rounded bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-medium transition-colors shadow-xs"
                >
                  Clear
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Shortcut Settings Quick Dialog */}
      {activeShortcutModal && (
        <div 
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/30 backdrop-blur-sm"
          onClick={() => setActiveShortcutModal(false)}
        >
          <div className="w-full max-w-md bg-white rounded-xl shadow-xl border border-slate-200 p-5 space-y-4" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">Change Activation Hotkey</h3>
              <button onClick={() => setActiveShortcutModal(false)} className="text-slate-400 hover:text-slate-600">
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>
            <p className="text-xs text-slate-600">
              Select the global Windows key for Push-to-Talk dictation.
            </p>
            <div className="space-y-2">
              {[
                { key: 'Right Alt', desc: 'Recommended: Non-interfering single finger push-to-talk' },
                { key: 'Caps Lock', desc: 'Hold Caps Lock to speak, tap to toggle standard caps' },
                { key: 'Ctrl + Space', desc: 'Classic command palette toggle trigger' },
              ].map(item => (
                <div key={item.key} className="flex items-center justify-between p-2.5 rounded-lg border border-slate-200 bg-slate-50/70 hover:border-sky-300 cursor-pointer">
                  <span className="font-mono text-xs font-bold text-slate-900">{item.key}</span>
                  <span className="text-[11px] text-slate-500">{item.desc}</span>
                </div>
              ))}
            </div>
            <div className="flex justify-end">
              <button 
                onClick={() => setActiveShortcutModal(false)}
                className="px-3 py-1.5 bg-[#0284c7] text-white rounded text-xs font-medium"
              >
                Save Shortcut
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Microphone Settings Quick Dialog */}
      {micSettingsModal && (
        <div 
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/30 backdrop-blur-sm"
          onClick={() => setMicSettingsModal(false)}
        >
          <div className="w-full max-w-md bg-white rounded-xl shadow-xl border border-slate-200 p-5 space-y-4" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">Audio Endpoint Configuration</h3>
              <button onClick={() => setMicSettingsModal(false)} className="text-slate-400 hover:text-slate-600">
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>
            <p className="text-xs text-slate-600">
              Direct WASAPI capture stream running at 16kHz mono without system resamplers.
            </p>
            <div className="space-y-2">
              <label className="text-xs font-semibold text-slate-700">Input Device</label>
              <select className="w-full p-2 border border-slate-200 rounded text-xs text-slate-800 bg-white">
                <option>Default Microphone (Realtek High Definition Audio)</option>
                <option>Headset Microphone (Plantronics BT600)</option>
                <option>USB Audio Array (WASAPI Loopback Direct)</option>
              </select>
            </div>
            <div className="flex items-center justify-between pt-2">
              <span className="text-xs font-mono text-[#0284c7]">VAD Threshold: 42%</span>
              <button 
                onClick={() => setMicSettingsModal(false)}
                className="px-3 py-1.5 bg-[#0284c7] text-white rounded text-xs font-medium"
              >
                Apply
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
