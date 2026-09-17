import React, { useState, useEffect, useRef } from 'react';
import { DictionaryEntry, SnippetEntry } from '../types';

interface DictionaryTabProps {
  entries: DictionaryEntry[];
  onAddEntry: (term: string, replacement?: string, category?: string) => void;
  onUpdateEntry?: (id: string, term: string, replacement?: string, category?: string) => void;
  onToggleFavorite: (id: string) => void;
  onDeleteEntry: (id: string) => void;
  testWordSanitize?: (txt: string) => string;
  onNavigateToSnippets?: () => void;
}

export const DictionaryTab: React.FC<DictionaryTabProps> = ({
  entries,
  onAddEntry,
  onUpdateEntry,
  onToggleFavorite,
  onDeleteEntry,
  testWordSanitize,
  onNavigateToSnippets,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [activeCategory, setActiveCategory] = useState<string>('All');
  const [sortOption, setSortOption] = useState<string>('starred');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;
  const [importFeedback, setImportFeedback] = useState<string | null>(null);
  const [isListeningInput, setIsListeningInput] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Modal form states
  const [spokenInput, setSpokenInput] = useState('');
  const [outputInput, setOutputInput] = useState('');
  const [categoryInput, setCategoryInput] = useState('Tech');
  const [exactCase, setExactCase] = useState(true);
  const [autoSpacing, setAutoSpacing] = useState(true);

  // Pronunciation tester
  const [testerPhrase, setTesterPhrase] = useState('Deploy to cube netties production');
  const [testerOutput, setTesterOutput] = useState('Deploy to k8s production');

  const searchInputRef = useRef<HTMLInputElement>(null);

  // Reset page to 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, activeCategory, sortOption]);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const text = event.target?.result as string;
        let count = 0;
        if (file.name.endsWith('.json')) {
          const parsed = JSON.parse(text);
          if (Array.isArray(parsed)) {
            parsed.forEach((item: any) => {
              const term = item.term || item.spoken || item.trigger;
              const rep = item.replacement || item.output || item.expansion;
              const cat = item.category || 'Custom';
              if (term && rep) {
                onAddEntry(term, rep, cat);
                count++;
              }
            });
          }
        } else {
          const lines = text.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
          for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            if (i === 0 && line.toLowerCase().includes('term') && line.toLowerCase().includes('replacement')) {
              continue;
            }
            const parts = line.split(',').map(p => p.trim().replace(/^["']|["']$/g, ''));
            if (parts.length >= 2) {
              const term = parts[0];
              const rep = parts[1];
              const cat = parts[2] || 'Custom';
              if (term && rep) {
                onAddEntry(term, rep, cat);
                count++;
              }
            }
          }
        }
        setImportFeedback(`Imported ${count} vocabulary rules successfully`);
        setTimeout(() => setImportFeedback(null), 3000);
      } catch {
        setImportFeedback('Failed to parse file. Please provide valid CSV or JSON.');
        setTimeout(() => setImportFeedback(null), 3500);
      }
      if (fileInputRef.current) fileInputRef.current.value = '';
    };
    reader.readAsText(file);
  };

  const handleMicClick = () => {
    const SpeechRec = (window as any).webkitSpeechRecognition || (window as any).SpeechRecognition;
    if (SpeechRec) {
      try {
        const rec = new SpeechRec();
        rec.lang = 'en-US';
        rec.continuous = false;
        rec.interimResults = false;
        setIsListeningInput(true);
        rec.onresult = (e: any) => {
          const transcript = e.results[0]?.[0]?.transcript;
          if (transcript) setSpokenInput(transcript.trim());
          setIsListeningInput(false);
        };
        rec.onerror = () => setIsListeningInput(false);
        rec.onend = () => setIsListeningInput(false);
        rec.start();
      } catch {
        setIsListeningInput(false);
      }
    } else {
      setSpokenInput('DirectML WASAPI');
    }
  };

  // Keyboard shortcut Alt+N for Add Word, / for search
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.altKey && (e.key === 'n' || e.key === 'N')) {
        e.preventDefault();
        setEditingId(null);
        setSpokenInput('');
        setOutputInput('');
        setCategoryInput('Tech');
        setIsModalOpen(true);
      }
      if (e.key === '/' && document.activeElement !== searchInputRef.current && !isModalOpen) {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isModalOpen]);

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const handleSaveWord = (e: React.FormEvent) => {
    e.preventDefault();
    if (!spokenInput.trim() || !outputInput.trim()) return;

    if (editingId && onUpdateEntry) {
      onUpdateEntry(editingId, spokenInput.trim(), outputInput.trim(), categoryInput);
    } else {
      onAddEntry(spokenInput.trim(), outputInput.trim(), categoryInput);
    }
    setIsModalOpen(false);
    setEditingId(null);
    setSpokenInput('');
    setOutputInput('');
  };

  // Filter and sort entries
  const filteredEntries = entries.filter(entry => {
    if (activeCategory !== 'All') {
      const catLower = (entry.category || '').toLowerCase();
      if (activeCategory === 'Tech' && !catLower.includes('tech') && !catLower.includes('design')) return false;
      if (activeCategory === 'Medical' && !catLower.includes('medical')) return false;
      if (activeCategory === 'Names' && !catLower.includes('name')) return false;
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      return (
        entry.term.toLowerCase().includes(q) ||
        (entry.replacement && entry.replacement.toLowerCase().includes(q)) ||
        (entry.phonetic && entry.phonetic.toLowerCase().includes(q)) ||
        (entry.matchesHint && entry.matchesHint.toLowerCase().includes(q))
      );
    }
    return true;
  }).sort((a, b) => {
    if (sortOption === 'starred') {
      if (a.isFavorite === b.isFavorite) return 0;
      return a.isFavorite ? -1 : 1;
    }
    if (sortOption === 'az') {
      return a.term.localeCompare(b.term);
    }
    return b.id.localeCompare(a.id);
  });

  const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
  const pagedEntries = filteredEntries.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return (
    <div id="dictionary-view" className="flex flex-col w-full pb-16 max-w-6xl mx-auto select-none pt-1">
      {/* 1. Top Bar & View Switcher */}
      <div className="flex flex-col gap-3 mb-5">
        {/* Breadcrumb & Mode Segmented Rail */}
        <div className="flex items-center justify-between">
          <div className="inline-flex p-0.5 rounded-lg bg-white border border-[#e2e8f0] shadow-xs">
            <button 
              id="tab-dict"
              className="px-3 py-1.5 rounded bg-[#e0f2fe] text-[#0284c7] font-semibold text-xs transition-all"
            >
              <span className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[16px] text-[#0284c7]">menu_book</span>
                Personal Dictionary
              </span>
            </button>

            <button 
              id="tab-snippets"
              onClick={onNavigateToSnippets}
              className="px-3 py-1.5 rounded text-slate-500 hover:text-slate-900 text-xs transition-all"
            >
              <span className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[16px]">data_object</span>
                Snippets
                <span className="font-mono text-[10px] px-1.5 py-0.2 rounded bg-slate-100 text-slate-600">
                  14
                </span>
              </span>
            </button>
          </div>

          {/* Engine Status Cap */}
          <div className="flex items-center gap-2 bg-white px-2.5 py-1 rounded border border-[#e2e8f0] shadow-xs">
            <div className="w-1.5 h-1.5 rounded-full bg-[#0284c7] animate-pulse"></div>
            <span className="font-mono text-[11px] text-slate-500 tracking-wider uppercase">
              Whisper-Local v2.4 • 1,429 Lexemes
            </span>
          </div>
        </div>

        {/* Screen Title & Description */}
        <div className="flex items-start justify-between">
          <div className="space-y-0.5">
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Dictionary</h1>
              <span className="px-2 py-0.5 rounded bg-[#e0f2fe] text-[#0284c7] border border-[#bae6fd] text-[11px] font-medium">
                Active Layer
              </span>
            </div>
            <p className="text-xs text-slate-500 max-w-2xl">
              Teach FLOW custom phonetic variations, proprietary naming schemes, and instantaneous orthographic replacements for your local dictation pipeline.
            </p>
          </div>

          {/* Quick Metrics Strip */}
          <div className="hidden lg:flex items-center gap-3 bg-white px-3.5 py-2 rounded-lg border border-[#e2e8f0] shadow-xs">
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 uppercase font-semibold">Phonetic Accuracy</span>
              <span className="font-mono text-base text-[#0284c7] font-bold leading-tight">99.84%</span>
            </div>
            <div className="w-[1px] h-6 bg-[#e2e8f0]"></div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 uppercase font-semibold">Replacements Applied</span>
              <span className="font-mono text-base text-slate-900 font-bold leading-tight">12,840</span>
            </div>
          </div>
        </div>

        {/* Control Bar: Search, Sorters, and CTA */}
        <div className="flex flex-wrap items-center justify-between gap-2.5 pt-1">
          <div className="flex items-center gap-2 flex-1 max-w-xl">
            {/* Live Search Field */}
            <div className="relative flex-1 group">
              <span className="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400 text-[16px] pointer-events-none group-focus-within:text-[#0284c7] transition-colors">
                search
              </span>
              <input
                ref={searchInputRef}
                id="search-input"
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Search vocabulary, phonetic keys, or triggers..."
                className="w-full h-8 pl-8 pr-8 rounded bg-white border border-[#e2e8f0] text-slate-900 placeholder:text-slate-400 text-xs focus:outline-none focus:border-[#0284c7] focus:ring-1 focus:ring-[#0284c7] shadow-xs transition-all"
              />
              <kbd className="absolute right-2 top-1/2 -translate-y-1/2 font-mono text-[10px] text-slate-400 bg-slate-100 border border-slate-200 px-1 rounded pointer-events-none">
                /
              </kbd>
            </div>

            {/* Sort Select Pill */}
            <div className="relative">
              <select
                id="sort-select"
                value={sortOption}
                onChange={e => setSortOption(e.target.value)}
                className="appearance-none h-8 pl-2.5 pr-7 rounded bg-white border border-[#e2e8f0] hover:border-slate-300 text-slate-800 text-xs cursor-pointer transition-colors focus:outline-none focus:border-[#0284c7] shadow-xs"
              >
                <option value="starred">Starred first</option>
                <option value="newest">Newest added</option>
                <option value="az">Alphabetical (A-Z)</option>
                <option value="frequency">Most triggered</option>
              </select>
              <span className="material-symbols-outlined absolute right-1.5 top-1/2 -translate-y-1/2 text-slate-400 text-[14px] pointer-events-none">
                expand_more
              </span>
            </div>

            {/* Filter Category Tags */}
            <div className="hidden sm:flex items-center gap-1 bg-white border border-[#e2e8f0] p-0.5 rounded shadow-xs">
              {['All', 'Tech', 'Medical', 'Names'].map(cat => (
                <button
                  key={cat}
                  onClick={() => setActiveCategory(cat)}
                  className={`px-2 py-0.5 rounded text-xs transition-colors ${
                    activeCategory === cat
                      ? 'bg-[#e0f2fe] text-[#0284c7] font-semibold'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
                  }`}
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          {/* Add Word Primary Button */}
          <div className="flex items-center gap-1.5">
            <button
              id="open-modal-btn"
              onClick={() => {
                setEditingId(null);
                setSpokenInput('');
                setOutputInput('');
                setCategoryInput('Tech');
                setIsModalOpen(true);
              }}
              className="h-8 px-3.5 rounded bg-[#0284c7] hover:bg-[#0369a1] active:scale-[0.985] text-white text-xs font-medium flex items-center gap-1.5 shadow-xs transition-all"
            >
              <span className="material-symbols-outlined text-[16px]">add</span>
              <span>Add word</span>
              <kbd className="font-mono text-[10px] bg-white/20 px-1 py-0.2 rounded text-white ml-0.5">
                Alt+N
              </kbd>
            </button>
          </div>
        </div>
      </div>

      {/* 2. Bento Layout: Dictionary Data Grid + Quick Stats HUD */}
      <div className="grid grid-cols-1 xl:grid-cols-4 gap-4 items-start">
        {/* Table & List View (3 Columns in 4-col bento) */}
        <div className="xl:col-span-3 flex flex-col bg-white rounded-lg border border-[#e2e8f0] overflow-hidden shadow-xs">
          {/* Table Header */}
          <div className="grid grid-cols-12 px-4 py-2 bg-[#f8fafc] border-b border-[#e2e8f0] text-slate-500 text-[11px] uppercase tracking-wider items-center select-none font-semibold">
            <div className="col-span-1 text-center">★</div>
            <div className="col-span-4 pl-1">Spoken Input / Trigger</div>
            <div className="col-span-4">Formatted Output</div>
            <div className="col-span-2">Domain</div>
            <div className="col-span-1 text-right pr-1">Actions</div>
          </div>

          {/* Dictionary Entries Stream */}
          <div className="divide-y divide-[#e2e8f0]" id="dict-entries-container">
            {pagedEntries.map(entry => (
              <div 
                key={entry.id}
                className="grid grid-cols-12 px-4 py-2.5 items-center bg-white hover:bg-[#f8fafc] transition-colors group"
              >
                <div className="col-span-1 flex justify-center">
                  <button
                    onClick={() => onToggleFavorite(entry.id)}
                    aria-label="Favorite"
                    className="star-toggle text-slate-300 hover:text-amber-500 p-0.5 transition-transform active:scale-90"
                  >
                    <span 
                      className={`material-symbols-outlined text-[18px] ${
                        entry.isFavorite ? 'text-amber-500' : 'text-slate-300'
                      }`}
                      style={{ fontVariationSettings: entry.isFavorite ? "'FILL' 1" : "'FILL' 0" }}
                    >
                      star
                    </span>
                  </button>
                </div>

                <div className="col-span-4 pl-1 flex flex-col min-w-0 pr-2">
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs text-slate-900 font-medium truncate">{entry.term}</span>
                    {entry.phonetic && (
                      <span className="font-mono text-[10px] text-slate-400 truncate">{entry.phonetic}</span>
                    )}
                  </div>
                  <span className="text-[11px] text-slate-500 truncate">
                    {entry.matchesHint || `Rule created for ${entry.term}`}
                  </span>
                </div>

                <div className="col-span-4 flex items-center gap-2 min-w-0 pr-2">
                  <div className="flex items-center gap-1.5 px-2 py-0.5 bg-slate-100 border border-[#e2e8f0] rounded">
                    <span className="font-mono text-xs text-[#0284c7] font-semibold truncate">
                      {entry.replacement || entry.term}
                    </span>
                    <button 
                      onClick={() => handleCopy(entry.replacement || entry.term, entry.id)}
                      className="text-slate-400 hover:text-slate-700 p-0.5" 
                      title="Copy text"
                    >
                      <span className="material-symbols-outlined text-[12px]">
                        {copiedId === entry.id ? 'check' : 'content_copy'}
                      </span>
                    </button>
                  </div>
                  {entry.tag && (
                    <span className="text-[10px] text-slate-500 px-1.5 py-0.5 rounded bg-slate-100 border border-[#e2e8f0] font-medium">
                      {entry.tag}
                    </span>
                  )}
                </div>

                <div className="col-span-2 flex items-center">
                  <span className="px-2 py-0.5 rounded bg-sky-50 text-[#0284c7] border border-sky-200 text-[10px] font-medium truncate">
                    {entry.category || 'Tech / Cloud'}
                  </span>
                </div>

                <div className="col-span-1 flex items-center justify-end gap-1 opacity-40 group-hover:opacity-100 transition-opacity">
                  <button 
                    onClick={() => {
                      setEditingId(entry.id);
                      setSpokenInput(entry.term);
                      setOutputInput(entry.replacement || '');
                      setCategoryInput(entry.category || 'Tech');
                      setIsModalOpen(true);
                    }}
                    className="p-1 rounded hover:bg-slate-100 text-slate-400 hover:text-slate-700" 
                    title="Edit entry"
                  >
                    <span className="material-symbols-outlined text-[15px]">edit</span>
                  </button>
                  <button 
                    onClick={() => onDeleteEntry(entry.id)}
                    className="p-1 rounded hover:bg-red-50 hover:text-red-500 text-slate-400 transition-colors" 
                    title="Delete entry"
                  >
                    <span className="material-symbols-outlined text-[15px]">delete</span>
                  </button>
                </div>
              </div>
            ))}
          </div>

          {/* Pagination & Bottom Drawer Summary */}
          <div className="flex items-center justify-between px-4 py-2.5 bg-[#f8fafc] border-t border-[#e2e8f0] text-slate-500 text-xs">
            <div className="flex items-center gap-2">
              <span>
                Showing {pagedEntries.length > 0 ? (currentPage - 1) * pageSize + 1 : 0}-{Math.min(currentPage * pageSize, filteredEntries.length)} of {filteredEntries.length} vocabulary rules
              </span>
              <span className="text-slate-300">•</span>
              <span className="text-[#0284c7] font-medium">Synced with local model weights</span>
            </div>

            <div className="flex items-center gap-1">
              <button 
                id="btn-dict-prev-page"
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                className={`px-2.5 py-1 rounded border text-xs transition-colors ${
                  currentPage <= 1
                    ? 'bg-slate-50 border-[#e2e8f0] text-slate-300 cursor-not-allowed'
                    : 'bg-white border-[#e2e8f0] text-slate-700 hover:bg-slate-50 shadow-xs'
                }`}
              >
                Previous
              </button>
              <span className="px-2.5 py-1 font-mono text-[#0f172a] font-semibold bg-[#e0f2fe] border border-[#bae6fd] rounded text-xs">
                {currentPage} / {totalPages}
              </span>
              <button 
                id="btn-dict-next-page"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                className={`px-2.5 py-1 rounded border text-xs transition-colors ${
                  currentPage >= totalPages
                    ? 'bg-slate-50 border-[#e2e8f0] text-slate-300 cursor-not-allowed'
                    : 'bg-white border-[#e2e8f0] text-slate-700 hover:bg-slate-50 shadow-xs'
                }`}
              >
                Next
              </button>
            </div>
          </div>
        </div>

        {/* Side Bento: Live Engine Inspector & Sound Waveform Preview */}
        <div className="xl:col-span-1 flex flex-col gap-3">
          {/* Card: Phonetic Tuning */}
          <div className="p-4 bg-white rounded-lg border border-[#e2e8f0] shadow-xs flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <span className="text-[11px] text-slate-500 uppercase font-semibold tracking-wider">
                Acoustic Model Bias
              </span>
              <span className="material-symbols-outlined text-[16px] text-[#0284c7]">hearing</span>
            </div>

            <div className="space-y-1.5">
              <div className="flex justify-between text-xs">
                <span className="text-slate-900 font-medium">Vocabulary Priority Weight</span>
                <span className="text-[#0284c7] font-mono font-semibold">+2.4dB</span>
              </div>
              <div className="w-full h-1.5 bg-slate-200 rounded-full overflow-hidden">
                <div className="w-[78%] h-full bg-[#0284c7] rounded-full"></div>
              </div>
            </div>

            <p className="text-[11px] text-slate-500 leading-normal">
              Favors user dictionary terms over standard English dictionary tokens when speech confidence drops under 85%.
            </p>
          </div>

          {/* Card: Interactive Acoustic Test Area */}
          <div className="p-4 bg-white rounded-lg border border-[#e2e8f0] shadow-xs flex flex-col gap-2 relative overflow-hidden">
            <div className="flex items-center justify-between">
              <span className="text-[11px] text-slate-500 uppercase font-semibold tracking-wider">
                Pronunciation Tester
              </span>
              <div className="w-2 h-2 rounded-full bg-[#0284c7] animate-pulse"></div>
            </div>

            <p className="text-xs text-slate-600">
              Speak a phrase to test whether FLOW matches your replacement rules in real time:
            </p>

            {/* Simulated Audio Waveform HUD */}
            <div className="p-2.5 bg-[#f8fafc] border border-[#e2e8f0] rounded flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[#0284c7] text-[18px]">mic</span>
                <span className="font-mono text-[11px] text-slate-900 font-medium">Listening...</span>
              </div>

              {/* 8-bar Mini Visualizer */}
              <div className="flex items-center gap-0.5 h-5">
                <div className="w-0.5 h-2 bg-[#0284c7]/40 rounded-full animate-pulse"></div>
                <div className="w-0.5 h-3.5 bg-[#0284c7]/70 rounded-full animate-pulse" style={{ animationDelay: '75ms' }}></div>
                <div className="w-0.5 h-5 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '150ms' }}></div>
                <div className="w-0.5 h-4 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '220ms' }}></div>
                <div className="w-0.5 h-2 bg-[#0284c7]/50 rounded-full animate-pulse" style={{ animationDelay: '90ms' }}></div>
                <div className="w-0.5 h-4.5 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '180ms' }}></div>
                <div className="w-0.5 h-3 bg-[#0284c7] rounded-full animate-pulse" style={{ animationDelay: '300ms' }}></div>
                <div className="w-0.5 h-1.5 bg-[#0284c7]/40 rounded-full"></div>
              </div>
            </div>

            <div className="bg-slate-100 border border-[#e2e8f0] p-2 rounded">
              <div className="font-mono text-[10px] text-slate-500 uppercase font-semibold">Detected:</div>
              <div className="text-xs text-slate-900 mt-0.5 font-medium">
                "Deploy to <span className="text-[#0284c7] font-mono bg-[#e0f2fe] border border-[#bae6fd] px-1 rounded font-bold">k8s</span> production"
              </div>
            </div>
          </div>

          {/* Quick Import / Export Card */}
          <div className="p-4 bg-white rounded-lg border border-[#e2e8f0] shadow-xs flex flex-col gap-1.5">
            <span className="text-[11px] text-slate-500 uppercase font-semibold tracking-wider">
              Sync &amp; Lexicon Files
            </span>
            <div className="flex gap-2 mt-1">
              <input
                type="file"
                ref={fileInputRef}
                accept=".csv,.json"
                onChange={handleFileUpload}
                className="hidden"
              />
              <button 
                id="btn-dict-import-file"
                onClick={() => fileInputRef.current?.click()}
                className="flex-1 py-1.5 px-2 rounded bg-white hover:bg-slate-50 border border-[#e2e8f0] text-slate-800 text-xs font-medium flex items-center justify-center gap-1 transition-colors shadow-xs cursor-pointer"
              >
                <span className="material-symbols-outlined text-[14px] text-slate-500">upload_file</span>
                Import CSV / JSON
              </button>
              <button 
                id="btn-dict-export-file"
                onClick={() => {
                  const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(entries, null, 2));
                  const a = document.createElement('a');
                  a.href = dataStr;
                  a.download = 'flow_dictionary_lexicon.json';
                  a.click();
                }}
                className="flex-1 py-1.5 px-2 rounded bg-white hover:bg-slate-50 border border-[#e2e8f0] text-slate-800 text-xs font-medium flex items-center justify-center gap-1 transition-colors shadow-xs cursor-pointer"
              >
                <span className="material-symbols-outlined text-[14px] text-slate-500">download</span>
                Export
              </button>
            </div>
            {importFeedback && (
              <span className="text-[11px] text-emerald-600 font-medium animate-in fade-in pt-0.5">
                {importFeedback}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* 3. Integrated Windows 11 Native Flyout Dialog / Modal: 'Add Word' */}
      {isModalOpen && (
        <div 
          id="modal-backdrop"
          className="fixed inset-0 bg-slate-900/30 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
          onClick={(e) => {
            if (e.target === e.currentTarget) setIsModalOpen(false);
          }}
        >
          <div 
            id="modal-panel"
            className="w-full max-w-lg bg-white rounded-xl shadow-2xl border border-[#e2e8f0] overflow-hidden transform transition-transform duration-200"
            role="dialog"
            onClick={e => e.stopPropagation()}
          >
            {/* Dialog Header */}
            <div className="flex items-center justify-between px-6 py-4 bg-[#f8fafc] border-b border-[#e2e8f0]">
              <div className="flex items-center gap-2.5">
                <div className="w-6 h-6 rounded bg-[#e0f2fe] flex items-center justify-center text-[#0284c7]">
                  <span className="material-symbols-outlined text-[16px]">
                    {editingId ? 'edit' : 'add_circle'}
                  </span>
                </div>
                <div>
                  <h2 className="text-sm font-semibold text-slate-900 leading-none">
                    {editingId ? 'Edit Vocabulary Rule' : 'Add Vocabulary Rule'}
                  </h2>
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    Configures the local inference replacement mapper
                  </p>
                </div>
              </div>

              <button 
                id="close-modal-btn"
                onClick={() => setIsModalOpen(false)}
                className="w-7 h-7 rounded flex items-center justify-center text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>

            {/* Dialog Form */}
            <form onSubmit={handleSaveWord} className="p-6 flex flex-col gap-4">
              {/* Input 1: Spoken Trigger */}
              <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between">
                  <label className="text-xs text-slate-900 font-medium" htmlFor="input-spoken">
                    Word or phrase you speak
                  </label>
                  <span className="text-[11px] text-slate-400">Phonetic source</span>
                </div>
                <div className="relative">
                  <input
                    id="input-spoken"
                    type="text"
                    required
                    value={spokenInput}
                    onChange={e => setSpokenInput(e.target.value)}
                    placeholder="e.g. Wispr, Cloud native, Doctor Thorne"
                    className="w-full h-9 px-3 rounded bg-white border border-[#e2e8f0] text-slate-900 text-xs placeholder:text-slate-400 focus:outline-none focus:border-[#0284c7] focus:ring-1 focus:ring-[#0284c7] transition-all"
                  />
                  <button 
                    type="button"
                    onClick={handleMicClick}
                    className={`absolute right-2 top-1/2 -translate-y-1/2 p-1 rounded-full transition-colors ${
                      isListeningInput ? 'text-rose-500 animate-pulse bg-rose-50' : 'text-slate-400 hover:text-[#0284c7]'
                    }`}
                    title={isListeningInput ? "Listening to your voice..." : "Record voice sample"}
                  >
                    <span className="material-symbols-outlined text-[16px]">mic</span>
                  </button>
                </div>
                <span className="text-[11px] text-slate-500">
                  What the Whisper model hears during normal speaking velocity.
                </span>
              </div>

              {/* Input 2: Output Replacement */}
              <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between">
                  <label className="text-xs text-slate-900 font-medium" htmlFor="input-output">
                    How FLOW should write it
                  </label>
                  <span className="text-[11px] text-slate-400">Resulting string</span>
                </div>
                <input
                  id="input-output"
                  type="text"
                  required
                  value={outputInput}
                  onChange={e => setOutputInput(e.target.value)}
                  placeholder="e.g. Wispr Flow, cloud-native, Dr. Thorne, MD"
                  className="w-full h-9 px-3 rounded bg-white border border-[#e2e8f0] text-slate-900 text-xs placeholder:text-slate-400 focus:outline-none focus:border-[#0284c7] focus:ring-1 focus:ring-[#0284c7] transition-all"
                />
              </div>

              {/* Meta Selector: Category */}
              <div className="flex flex-col gap-1">
                <label className="text-xs text-slate-900 font-medium" htmlFor="input-category">
                  Category domain
                </label>
                <select
                  id="input-category"
                  value={categoryInput}
                  onChange={e => setCategoryInput(e.target.value)}
                  className="h-9 px-3 rounded bg-white border border-[#e2e8f0] text-slate-900 text-xs focus:outline-none focus:border-[#0284c7] focus:ring-1 focus:ring-[#0284c7] transition-all cursor-pointer"
                >
                  <option value="Tech">Tech / Engineering</option>
                  <option value="Medical">Medical / Anatomy</option>
                  <option value="Legal">Legal &amp; Contracts</option>
                  <option value="Name">Personal Names &amp; Titles</option>
                  <option value="General">General Jargon</option>
                </select>
              </div>

              {/* Fluent Toggles */}
              <div className="pt-1 space-y-2">
                <div className="flex items-center justify-between py-1.5 border-t border-slate-100">
                  <div className="flex flex-col">
                    <span className="text-xs text-slate-900 font-medium">Exact Case Preservation</span>
                    <span className="text-[11px] text-slate-500">Matches letter casing strictly as entered above</span>
                  </div>
                  <label className="relative inline-flex items-center cursor-pointer">
                    <input
                      id="toggle-case"
                      type="checkbox"
                      checked={exactCase}
                      onChange={e => setExactCase(e.target.checked)}
                      className="sr-only peer"
                    />
                    <div className="w-9 h-5 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
                    <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
                  </label>
                </div>

                <div className="flex items-center justify-between py-1.5 border-t border-slate-100">
                  <div className="flex flex-col">
                    <span className="text-xs text-slate-900 font-medium">Surrounding Auto-Spacing</span>
                    <span className="text-[11px] text-slate-500">Intelligently removes excess spaces preceding commas &amp; hyphens</span>
                  </div>
                  <label className="relative inline-flex items-center cursor-pointer">
                    <input
                      id="toggle-punctuation"
                      type="checkbox"
                      checked={autoSpacing}
                      onChange={e => setAutoSpacing(e.target.checked)}
                      className="sr-only peer"
                    />
                    <div className="w-9 h-5 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
                    <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
                  </label>
                </div>
              </div>

              {/* Live Validation Indicator */}
              <div className="flex items-center gap-2 px-3 py-2 rounded bg-slate-100 border border-[#e2e8f0] text-slate-600">
                <span className="material-symbols-outlined text-[16px] text-[#0284c7]">task_alt</span>
                <span className="text-xs">
                  {spokenInput.trim()
                    ? `Valid trigger: "${spokenInput.trim()}" ready for index`
                    : 'Validation: Ready to bind to model slot'}
                </span>
              </div>

              {/* Action Footer */}
              <div className="flex items-center justify-end gap-2 pt-2 border-t border-[#e2e8f0]">
                <button
                  type="button"
                  id="cancel-modal-btn"
                  onClick={() => setIsModalOpen(false)}
                  className="h-8 px-3 rounded bg-white hover:bg-slate-50 border border-[#e2e8f0] text-slate-700 text-xs font-medium transition-colors shadow-xs"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  id="save-word-btn"
                  className="h-8 px-4 rounded bg-[#0284c7] hover:bg-[#0369a1] text-white text-xs font-medium transition-all flex items-center gap-1.5 shadow-xs active:scale-95"
                >
                  <span className="material-symbols-outlined text-[16px]">save</span>
                  <span>{editingId ? 'Update word' : 'Save word'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
