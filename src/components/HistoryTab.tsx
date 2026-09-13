import React, { useState, useEffect, useRef } from 'react';
import { DictationEntry } from '../types';

interface HistoryTabProps {
  entries: DictationEntry[];
  onToggleFavorite?: (id: string) => void;
  onDeleteEntry?: (id: string) => void;
}

export const HistoryTab: React.FC<HistoryTabProps> = ({
  entries,
  onToggleFavorite,
  onDeleteEntry,
}) => {
  const [selectedId, setSelectedId] = useState<string>(entries[0]?.id || 'hist-1');
  const [searchQuery, setSearchQuery] = useState('');
  const [dateFilter, setDateFilter] = useState('all');
  const [appFilter, setAppFilter] = useState('all');
  const [langFilter, setLangFilter] = useState('all');
  const [isStarredOnly, setIsStarredOnly] = useState(false);
  const [isExportMenuOpen, setIsExportMenuOpen] = useState(false);
  const [isCopied, setIsCopied] = useState(false);
  const [pasteToast, setPasteToast] = useState<string | null>(null);

  // Audio Playback simulation
  const [isPlayingAudio, setIsPlayingAudio] = useState(false);
  const [playbackSec, setPlaybackSec] = useState(0);
  const playbackIntervalRef = useRef<any>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  const selectedItem = entries.find(e => e.id === selectedId) || entries[0];

  // Hotkey Ctrl + F
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f') {
        e.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  // Filter logic
  const filteredEntries = entries.filter(item => {
    if (isStarredOnly && !item.isFavorite) return false;
    
    if (appFilter !== 'all') {
      const appLower = item.application.toLowerCase();
      if (appFilter === 'teams' && !appLower.includes('teams')) return false;
      if (appFilter === 'vscode' && !appLower.includes('code')) return false;
      if (appFilter === 'slack' && !appLower.includes('slack')) return false;
      if (appFilter === 'word' && !appLower.includes('word')) return false;
      if (appFilter === 'chrome' && !appLower.includes('chrome')) return false;
    }

    if (queryMatch(item, searchQuery)) return true;
    return false;
  });

  function queryMatch(item: DictationEntry, query: string) {
    if (!query.trim()) return true;
    const q = query.toLowerCase();
    return (
      item.text.toLowerCase().includes(q) ||
      item.application.toLowerCase().includes(q) ||
      (item.target && item.target.toLowerCase().includes(q))
    );
  }

  // Audio playback controls
  const togglePlayAudio = () => {
    if (isPlayingAudio) {
      clearInterval(playbackIntervalRef.current);
      setIsPlayingAudio(false);
      setPlaybackSec(0);
    } else {
      setIsPlayingAudio(true);
      setPlaybackSec(0);
      const totalSec = Math.max(3, Math.round((selectedItem?.durationMs || 8400) / 1000));
      playbackIntervalRef.current = setInterval(() => {
        setPlaybackSec(prev => {
          if (prev >= totalSec) {
            clearInterval(playbackIntervalRef.current);
            setIsPlayingAudio(false);
            return 0;
          }
          return prev + 1;
        });
      }, 1000);
    }
  };

  useEffect(() => {
    setIsPlayingAudio(false);
    clearInterval(playbackIntervalRef.current);
    setPlaybackSec(0);
  }, [selectedId]);

  const handleCopyFormatted = () => {
    if (!selectedItem) return;
    navigator.clipboard?.writeText(selectedItem.text);
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 1500);
  };

  const handlePasteToActiveApp = () => {
    if (!selectedItem) return;
    setPasteToast(`Dispatched to ${selectedItem.application}`);
    setTimeout(() => setPasteToast(null), 2400);
  };

  const handleExport = (type: 'json' | 'csv' | 'txt') => {
    setIsExportMenuOpen(false);
    let content = '';
    let mimeType = 'text/plain';

    if (type === 'json') {
      content = JSON.stringify(entries, null, 2);
      mimeType = 'application/json';
    } else if (type === 'csv') {
      const headers = ['id', 'application', 'createdAt', 'durationMs', 'words', 'text'];
      const rows = entries.map(e => [
        e.id,
        `"${e.application}"`,
        `"${e.createdAt}"`,
        e.durationMs,
        e.wordCount,
        `"${e.text.replace(/"/g, '""')}"`
      ]);
      content = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
      mimeType = 'text/csv';
    } else {
      content = entries.map(e => `[${e.createdAt}] ${e.application}:\n${e.text}\n`).join('\n---\n\n');
    }

    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `flow_transcripts_export.${type}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const durationSec = Math.max(1, Math.round((selectedItem?.durationMs || 8400) / 1000));

  return (
    <div id="history-view" className="flex flex-col w-full pb-16 space-y-5 select-none pt-1">
      {/* 1. Top Bar / Action Controls */}
      <div className="flex flex-col gap-3">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-2">
          <div className="flex flex-col">
            <div className="flex items-center gap-2">
              <span className="text-2xl font-bold text-slate-900 tracking-tight">History</span>
              <div className="flex items-center gap-1.5 ml-1 px-2.5 py-0.5 rounded-full bg-emerald-50 border border-emerald-200/80 text-emerald-700 font-mono text-[11px] font-medium">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                <span>Local Database Synced</span>
              </div>
            </div>
            <p className="text-[13px] text-slate-500 mt-0.5">
              All voice transcripts recorded and formatted locally on this device.
            </p>
          </div>

          {/* Export Action Dropdown */}
          <div className="relative inline-block text-left self-start md:self-auto">
            <button
              id="exportBtn"
              onClick={() => setIsExportMenuOpen(!isExportMenuOpen)}
              className="h-8 px-3.5 rounded bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 shadow-xs flex items-center gap-1.5 text-xs font-medium transition-all active:scale-[0.985]"
            >
              <span className="material-symbols-outlined text-[16px] text-sky-600">ios_share</span>
              <span>Export CSV/JSON</span>
              <span className="material-symbols-outlined text-[14px] text-slate-400">expand_more</span>
            </button>

            {isExportMenuOpen && (
              <div className="absolute right-0 mt-1 w-44 rounded-lg bg-white border border-slate-200 shadow-lg z-30 py-1 text-xs text-slate-800">
                <button
                  onClick={() => handleExport('json')}
                  className="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center justify-between font-medium"
                >
                  <span>JSON Raw Dump</span>
                  <span className="font-mono text-[11px] text-slate-400">.json</span>
                </button>
                <button
                  onClick={() => handleExport('csv')}
                  className="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center justify-between border-t border-slate-100 font-medium"
                >
                  <span>Structured CSV</span>
                  <span className="font-mono text-[11px] text-slate-400">.csv</span>
                </button>
                <button
                  onClick={() => handleExport('txt')}
                  className="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center justify-between border-t border-slate-100 font-medium"
                >
                  <span>Plain Transcript</span>
                  <span className="font-mono text-[11px] text-slate-400">.txt</span>
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Search + Filter Strip */}
        <div className="grid grid-cols-1 xl:grid-cols-12 gap-2.5 items-center">
          {/* Search Input with Ctrl+F */}
          <div className="xl:col-span-6 relative">
            <span className="material-symbols-outlined absolute left-3 top-2 text-[18px] text-slate-400">
              search
            </span>
            <input
              ref={searchInputRef}
              id="searchInput"
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="Search transcripts, applications, keywords..."
              className="w-full h-8 pl-9 pr-20 rounded bg-white border border-slate-300 shadow-xs text-slate-900 placeholder:text-slate-400 text-xs focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition-colors"
            />
            <div className="absolute right-2.5 top-1.5 flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-slate-100 border border-slate-200 text-slate-500 font-mono text-[10px] pointer-events-none">
              <span>Ctrl</span><span>+</span><span>F</span>
            </div>
          </div>

          {/* Filter Pills */}
          <div className="xl:col-span-6 flex flex-wrap items-center gap-1.5">
            {/* Date Filter Dropdown */}
            <div className="relative">
              <select
                id="dateFilter"
                value={dateFilter}
                onChange={e => setDateFilter(e.target.value)}
                className="h-8 pl-2.5 pr-7 rounded bg-white border border-slate-300 hover:border-slate-400 text-slate-700 text-xs shadow-xs appearance-none focus:outline-none focus:border-sky-500 cursor-pointer"
              >
                <option value="all">All Time</option>
                <option value="today">Today</option>
                <option value="week">This Week</option>
              </select>
              <span className="material-symbols-outlined absolute right-2 top-2 pointer-events-none text-[14px] text-slate-400">
                expand_more
              </span>
            </div>

            {/* Application Filter Dropdown */}
            <div className="relative">
              <select
                id="appFilter"
                value={appFilter}
                onChange={e => setAppFilter(e.target.value)}
                className="h-8 pl-2.5 pr-7 rounded bg-white border border-slate-300 hover:border-slate-400 text-slate-700 text-xs shadow-xs appearance-none focus:outline-none focus:border-sky-500 cursor-pointer"
              >
                <option value="all">All Applications</option>
                <option value="teams">Microsoft Teams</option>
                <option value="vscode">Visual Studio Code</option>
                <option value="slack">Slack</option>
                <option value="word">Microsoft Word</option>
                <option value="chrome">Google Chrome</option>
              </select>
              <span className="material-symbols-outlined absolute right-2 top-2 pointer-events-none text-[14px] text-slate-400">
                expand_more
              </span>
            </div>

            {/* Language Filter Dropdown */}
            <div className="relative">
              <select
                id="langFilter"
                value={langFilter}
                onChange={e => setLangFilter(e.target.value)}
                className="h-8 pl-2.5 pr-7 rounded bg-white border border-slate-300 hover:border-slate-400 text-slate-700 text-xs shadow-xs appearance-none focus:outline-none focus:border-sky-500 cursor-pointer"
              >
                <option value="all">All Languages</option>
                <option value="en">English (US/UK)</option>
                <option value="ta">Tamil (தமிழ்)</option>
                <option value="hi">Hindi (हिन्दी)</option>
              </select>
              <span className="material-symbols-outlined absolute right-2 top-2 pointer-events-none text-[14px] text-slate-400">
                expand_more
              </span>
            </div>

            {/* Starred Only Toggle */}
            <button
              id="starredToggle"
              onClick={() => setIsStarredOnly(!isStarredOnly)}
              className={`h-8 px-3 rounded border shadow-xs flex items-center gap-1 text-xs transition-colors ${
                isStarredOnly
                  ? 'bg-sky-50 text-sky-700 border-sky-300 font-semibold'
                  : 'bg-white hover:bg-slate-50 border-slate-300 text-slate-700'
              }`}
            >
              <span 
                className="material-symbols-outlined text-[15px] text-amber-500"
                style={{ fontVariationSettings: isStarredOnly ? "'FILL' 1" : "'FILL' 0" }}
              >
                grade
              </span>
              <span>Starred Only</span>
            </button>
          </div>
        </div>
      </div>

      {/* 2. Main Split Layout Area */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">
        {/* LEFT: Chronological Transcript Cards Stream (7 cols) */}
        <div className="lg:col-span-7 flex flex-col gap-3">
          {/* Section Header with Metrics */}
          <div className="flex items-center justify-between px-1 text-slate-500 text-[11px] tracking-wider uppercase font-semibold">
            <span id="transcriptCountLabel">
              Showing {filteredEntries.length} Transcripts Recorded Today
            </span>
            <span className="flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-sky-600"></span>
              Zero cloud latency
            </span>
          </div>

          {filteredEntries.map(entry => {
            const isSelected = selectedItem?.id === entry.id;
            return (
              <div
                key={entry.id}
                id={`card-${entry.id}`}
                onClick={() => setSelectedId(entry.id)}
                className={`transcript-card cursor-pointer p-4 rounded-xl bg-white shadow-xs transition-all duration-150 flex flex-col gap-1.5 relative overflow-hidden ${
                  isSelected
                    ? 'border-2 border-sky-500 shadow-sm'
                    : 'border border-slate-200 hover:border-slate-300 hover:bg-slate-50/70'
                }`}
              >
                {/* Active Accent Line Indicator */}
                {isSelected && (
                  <div className="active-indicator absolute left-0 top-0 bottom-0 w-1 bg-sky-600"></div>
                )}

                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="px-2 py-0.5 rounded bg-sky-50 text-sky-700 border border-sky-200 text-[11px] flex items-center gap-1 font-medium">
                      <span className="material-symbols-outlined text-[12px] text-sky-600">
                        {entry.application.includes('Teams') ? 'forum' :
                         entry.application.includes('Code') ? 'code' :
                         entry.application.includes('Slack') ? 'chat' :
                         entry.application.includes('Chrome') ? 'language' : 'description'}
                      </span>
                      {entry.application}
                    </span>
                    <span className="font-mono text-[11px] text-slate-500 font-medium">
                      {entry.timeShort || '2:18 PM'}
                    </span>
                    <span className="text-slate-300">•</span>
                    <span className="font-mono text-[11px] text-slate-500 font-medium">
                      {entry.durationText || `${(entry.durationMs / 1000).toFixed(1)}s`}
                    </span>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <span className="text-[11px] text-slate-500">{entry.wordCount} words</span>
                    <button
                      className="w-6 h-6 rounded flex items-center justify-center hover:bg-slate-100 text-amber-500"
                      onClick={(e) => {
                        e.stopPropagation();
                        onToggleFavorite && onToggleFavorite(entry.id);
                      }}
                      title="Star transcript"
                    >
                      <span 
                        className="material-symbols-outlined text-[16px]"
                        style={{ fontVariationSettings: entry.isFavorite ? "'FILL' 1" : "'FILL' 0" }}
                      >
                        grade
                      </span>
                    </button>
                  </div>
                </div>

                <p className="text-xs text-slate-800 line-clamp-2 mt-1">
                  {entry.text}
                </p>

                <div className="flex items-center justify-between text-slate-500 text-[11px] mt-1 pt-1 border-t border-slate-100">
                  <span className="text-slate-500 font-mono text-[10px]">
                    {entry.latency || 'INT8 • EN-US • 120ms'}
                  </span>
                  <span className="text-sky-600 font-medium flex items-center gap-0.5">
                    {entry.target || 'Direct Message'}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* RIGHT: Inspector / Detail Pane (5 cols) */}
        <div className="lg:col-span-5 flex flex-col gap-3 sticky top-16">
          {selectedItem ? (
            <div className="p-5 rounded-xl bg-white border border-slate-200 shadow-sm flex flex-col gap-4">
              {/* Header & Target App Status */}
              <div className="flex items-start justify-between gap-2">
                <div className="flex flex-col">
                  <span className="text-[11px] text-slate-500 uppercase tracking-wider font-semibold">
                    Active Transcript
                  </span>
                  <h2 className="text-base text-slate-900 flex items-center gap-1.5 mt-0.5" id="inspectorAppTitle">
                    <span className="font-bold">{selectedItem.application}</span>
                    <span className="text-slate-500 text-xs font-normal">• {selectedItem.target || 'Direct Message'}</span>
                  </h2>
                </div>

                <div className="flex items-center gap-1">
                  <button
                    id="detailStarBtn"
                    onClick={() => onToggleFavorite && onToggleFavorite(selectedItem.id)}
                    className="w-8 h-8 rounded bg-slate-100 hover:bg-slate-200 text-amber-500 flex items-center justify-center transition-colors border border-slate-200"
                    title="Toggle Star"
                  >
                    <span 
                      className="material-symbols-outlined text-[18px]"
                      style={{ fontVariationSettings: selectedItem.isFavorite ? "'FILL' 1" : "'FILL' 0" }}
                    >
                      grade
                    </span>
                  </button>

                  <button
                    onClick={() => onDeleteEntry && onDeleteEntry(selectedItem.id)}
                    className="w-8 h-8 rounded bg-slate-100 hover:bg-red-50 hover:border-red-200 hover:text-red-600 text-slate-500 flex items-center justify-center transition-colors border border-slate-200"
                    title="Delete Transcript"
                  >
                    <span className="material-symbols-outlined text-[18px]">delete</span>
                  </button>
                </div>
              </div>

              {/* Full Transcript Container with Backtrack Highlight Mode */}
              <div className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] text-slate-500 font-semibold tracking-wide uppercase">
                    Formatted Transcript
                  </span>
                  <span className="text-[11px] text-sky-700 bg-sky-50 px-2 py-0.5 rounded border border-sky-200 flex items-center gap-1 font-medium">
                    <span className="material-symbols-outlined text-[14px] text-sky-600">auto_fix_high</span>
                    Smart Punctuation Active
                  </span>
                </div>

                <div className="p-3.5 rounded-lg bg-slate-50/80 border border-slate-200 text-slate-900 text-sm leading-relaxed shadow-inner min-h-[120px] select-text">
                  <p id="inspectorBody">
                    {/* Render with custom highlight if matching 'Int8 inference' or 'audioStreamHandler' */}
                    {selectedItem.text.includes('Int8 inference') ? (
                      <>
                        Hey Jordan, could you make sure the pull request for the zero-latency audio engine gets merged into staging before our 3:30 sync? We already ran the{' '}
                        <span className="bg-sky-100 border border-sky-300 text-sky-900 font-semibold px-1.5 py-0.5 rounded font-mono text-[11px] inline-block" title="Auto-corrected terminology">
                          Int8 inference
                        </span>{' '}
                        benchmark tests locally.
                      </>
                    ) : (
                      selectedItem.text
                    )}
                  </p>
                </div>
              </div>

              {/* Quick Primary Actions */}
              <div className="grid grid-cols-2 gap-2">
                <button
                  onClick={handleCopyFormatted}
                  className="h-9 rounded-lg bg-sky-600 hover:bg-sky-700 text-white text-xs font-medium flex items-center justify-center gap-1.5 shadow-xs transition-all active:scale-[0.985]"
                >
                  <span className="material-symbols-outlined text-[16px]">
                    {isCopied ? 'check' : 'content_copy'}
                  </span>
                  <span>{isCopied ? 'Copied!' : 'Copy Formatted'}</span>
                </button>

                <button
                  onClick={handlePasteToActiveApp}
                  className="h-9 rounded-lg bg-white hover:bg-slate-50 text-slate-800 border border-slate-300 text-xs font-medium flex items-center justify-center gap-1.5 shadow-xs transition-all active:scale-[0.985]"
                >
                  <span className="material-symbols-outlined text-[16px] text-sky-600">keyboard_return</span>
                  <span>Paste to Active App</span>
                </button>
              </div>

              {/* Toast confirmation */}
              {pasteToast && (
                <div className="px-3 py-2 rounded bg-sky-50 border border-sky-200 text-sky-800 text-xs flex items-center gap-1.5 animate-in fade-in">
                  <span className="material-symbols-outlined text-[16px] text-sky-600">check_circle</span>
                  <span>{pasteToast}</span>
                </div>
              )}

              {/* Audio Playback Bar with Mini Scrubber */}
              <div className="p-3.5 rounded-lg bg-slate-50/80 border border-slate-200 flex flex-col gap-2">
                <div className="flex items-center justify-between text-slate-500 text-[11px]">
                  <div className="flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[15px] text-sky-600">mic</span>
                    <span className="font-mono font-medium text-slate-700">Local Session Replay</span>
                  </div>
                  <span className="font-mono text-slate-700 font-semibold" id="audioTimer">
                    0:0{playbackSec} / 0:{durationSec < 10 ? `0${durationSec}` : durationSec}
                  </span>
                </div>

                {/* Interactive Waveform Scrubber */}
                <div 
                  onClick={togglePlayAudio}
                  className="flex items-center gap-1 h-9 px-2.5 bg-white border border-slate-200 rounded cursor-pointer group shadow-xs hover:border-sky-300 transition-colors"
                >
                  <button
                    className="w-6 h-6 rounded-full bg-sky-600 hover:bg-sky-700 flex items-center justify-center text-white mr-1 shrink-0 shadow-xs"
                    id="playBtn"
                  >
                    <span className="material-symbols-outlined text-[16px]">
                      {isPlayingAudio ? 'pause' : 'play_arrow'}
                    </span>
                  </button>

                  {/* 15 Mini Waveform Bars */}
                  <div className="flex items-center justify-between flex-1 h-full gap-[3px] py-1" id="waveformContainer">
                    {[12, 20, 28, 16, 24, 32, 20, 12, 24, 28, 16, 20, 28, 12, 8].map((height, idx) => {
                      const isActive = isPlayingAudio ? idx <= playbackSec * 2 : idx < 6;
                      return (
                        <div
                          key={idx}
                          style={{ height: `${height}px` }}
                          className={`w-[3px] rounded-full transition-all duration-150 ${
                            isActive ? 'bg-sky-600' : 'bg-slate-200'
                          }`}
                        ></div>
                      );
                    })}
                  </div>
                </div>
              </div>

              {/* Metadata Grid */}
              <div className="flex flex-col gap-1.5 pt-1">
                <span className="text-[11px] text-slate-500 uppercase tracking-wider font-semibold">
                  Engine Diagnostic Specs
                </span>
                <div className="grid grid-cols-2 gap-2">
                  <div className="p-2.5 rounded bg-slate-50 border border-slate-200 flex flex-col">
                    <span className="text-[10px] text-slate-500 uppercase">Timestamp</span>
                    <span className="font-mono text-xs text-slate-800 font-semibold mt-0.5">
                      {selectedItem.createdAt}
                    </span>
                  </div>
                  <div className="p-2.5 rounded bg-slate-50 border border-slate-200 flex flex-col">
                    <span className="text-[10px] text-slate-500 uppercase">Engine Inference</span>
                    <span className="font-mono text-xs text-sky-700 font-semibold mt-0.5">
                      {selectedItem.latency || '120ms (Int8)'}
                    </span>
                  </div>
                  <div className="p-2.5 rounded bg-slate-50 border border-slate-200 flex flex-col">
                    <span className="text-[10px] text-slate-500 uppercase">Audio Duration</span>
                    <span className="font-mono text-xs text-slate-800 font-semibold mt-0.5">
                      {selectedItem.durationText || `${(selectedItem.durationMs / 1000).toFixed(1)}s recorded`}
                    </span>
                  </div>
                  <div className="p-2.5 rounded bg-slate-50 border border-slate-200 flex flex-col">
                    <span className="text-[10px] text-slate-500 uppercase">Model Pipeline</span>
                    <span className="font-mono text-xs text-slate-800 font-semibold mt-0.5">
                      {selectedItem.engine || 'FLOW Local Int8 v3.2'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Local Privacy Badge */}
              <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-emerald-50/80 border border-emerald-200 text-emerald-800 text-xs">
                <span className="material-symbols-outlined text-[18px] text-emerald-600">security</span>
                <span className="font-medium">Zero cloud egress. Audio stays isolated in RAM.</span>
              </div>
            </div>
          ) : (
            <div className="p-8 rounded-xl bg-white border border-slate-200 text-center text-slate-400 text-xs">
              Select a transcript from the left to inspect full diagnostics.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
