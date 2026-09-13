import React, { useState } from 'react';
import { SnippetEntry } from '../types';

interface SnippetsTabProps {
  snippets: SnippetEntry[];
  onAddSnippet: (entry: Omit<SnippetEntry, 'id' | 'createdAt'>) => void;
  onDeleteSnippet: (id: string) => void;
  testSnippetSanitize: (text: string) => string;
  onNavigateToDictionary?: () => void;
}

export const SnippetsTab: React.FC<SnippetsTabProps> = ({
  snippets,
  onAddSnippet,
  onDeleteSnippet,
  testSnippetSanitize,
  onNavigateToDictionary,
}) => {
  const [showAddForm, setShowAddForm] = useState(false);
  const [trigger, setTrigger] = useState('');
  const [expansion, setExpansion] = useState('');
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [testInput, setTestInput] = useState('please join my meeting link thanks');
  const [searchQuery, setSearchQuery] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!trigger.trim() || !expansion.trim()) return;
    onAddSnippet({
      trigger: trigger.trim().toLowerCase(),
      expansion: expansion.trim(),
    });
    setTrigger('');
    setExpansion('');
    setShowAddForm(false);
  };

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const filteredSnippets = snippets.filter(s => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return s.trigger.toLowerCase().includes(q) || s.expansion.toLowerCase().includes(q);
  });

  const testResult = testSnippetSanitize(testInput);

  return (
    <div id="snippets-tab-content" className="flex flex-col w-full pb-16 max-w-6xl mx-auto space-y-5 select-none pt-1">
      {/* 1. Top Rail & Mode Switcher */}
      <div className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <div className="inline-flex p-0.5 rounded-lg bg-white border border-[#e2e8f0] shadow-xs">
            <button 
              id="tab-dict-nav"
              onClick={onNavigateToDictionary}
              className="px-3 py-1.5 rounded text-slate-500 hover:text-slate-900 text-xs transition-all"
            >
              <span className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[16px]">menu_book</span>
                Personal Dictionary
              </span>
            </button>

            <button 
              id="tab-snippets-active"
              className="px-3 py-1.5 rounded bg-[#e0f2fe] text-[#0284c7] font-semibold text-xs transition-all"
            >
              <span className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[16px] text-[#0284c7]">data_object</span>
                Snippets
                <span className="font-mono text-[10px] px-1.5 py-0.2 rounded bg-sky-100 text-sky-700">
                  {snippets.length}
                </span>
              </span>
            </button>
          </div>

          <div className="flex items-center gap-2 bg-white px-2.5 py-1 rounded border border-[#e2e8f0] shadow-xs">
            <div className="w-1.5 h-1.5 rounded-full bg-[#0284c7]"></div>
            <span className="font-mono text-[11px] text-slate-500 tracking-wider uppercase">
              Macro Dispatcher Active
            </span>
          </div>
        </div>

        {/* Header Title & Summary */}
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Snippets &amp; Voice Templates</h1>
              <span className="px-2 py-0.5 rounded bg-[#e0f2fe] text-[#0284c7] border border-[#bae6fd] text-[11px] font-medium">
                Instant Expansion
              </span>
            </div>
            <p className="text-xs text-slate-500 max-w-2xl mt-0.5">
              Speak a short trigger phrase to insert boilerplate templates, meeting links, greetings, or code snippets with zero typing.
            </p>
          </div>

          <button
            id="btn-show-add-snippet"
            onClick={() => setShowAddForm(!showAddForm)}
            className="h-8 px-3.5 rounded bg-[#0284c7] hover:bg-[#0369a1] text-white text-xs font-medium flex items-center gap-1.5 shadow-xs transition-all active:scale-95"
          >
            <span className="material-symbols-outlined text-[16px]">
              {showAddForm ? 'close' : 'add'}
            </span>
            <span>{showAddForm ? 'Close Form' : '+ Add Snippet'}</span>
          </button>
        </div>

        {/* Search Bar */}
        <div className="relative max-w-md">
          <span className="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400 text-[16px] pointer-events-none">
            search
          </span>
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Filter snippets by trigger phrase or text..."
            className="w-full h-8 pl-8 pr-4 rounded bg-white border border-[#e2e8f0] text-slate-900 placeholder:text-slate-400 text-xs focus:outline-none focus:border-[#0284c7] shadow-xs"
          />
        </div>
      </div>

      {/* 2. Add Snippet Modal / Embedded Form */}
      {showAddForm && (
        <form onSubmit={handleSubmit} className="bg-white border border-[#0284c7]/30 rounded-xl p-5 shadow-md space-y-3 animate-in fade-in">
          <div className="flex items-center justify-between pb-2 border-b border-slate-100">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-[#0284c7] text-[18px]">bolt</span>
              <h2 className="text-sm font-bold text-slate-900">Create Voice Snippet</h2>
            </div>
            <span className="text-[11px] text-slate-500">Expands instantaneously upon speech pause</span>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-700 mb-1 font-medium">
                Spoken Trigger Phrase (e.g. "meeting link", "my intro", "sign off"):
              </label>
              <input
                type="text"
                required
                value={trigger}
                onChange={e => setTrigger(e.target.value)}
                placeholder="e.g. meeting link"
                className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none font-mono"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs text-slate-700 mb-1 font-medium">
              Expansion Text (multi-line supported):
            </label>
            <textarea
              required
              rows={3}
              value={expansion}
              onChange={e => setExpansion(e.target.value)}
              placeholder="Text to automatically insert when trigger is spoken..."
              className="w-full bg-slate-50 border border-slate-200 rounded-lg p-3 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none font-mono"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={() => setShowAddForm(false)}
              className="px-3 py-1.5 rounded-lg text-xs font-medium bg-white text-slate-700 hover:bg-slate-50 border border-slate-200 shadow-2xs"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-1.5 rounded-lg text-xs font-semibold bg-[#0284c7] hover:bg-[#0369a1] text-white shadow-xs"
            >
              Save Snippet
            </button>
          </div>
        </form>
      )}

      {/* 3. Live Snippet Expansion Verification Box */}
      <div className="bg-white border border-[#e2e8f0] rounded-xl p-4 shadow-xs space-y-2.5">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">auto_fix_high</span>
            <h2 className="text-xs font-bold text-slate-900">Live Snippet Real-Time Match Test</h2>
          </div>
          <span className="text-[11px] text-slate-500">Deterministic tokenizer test</span>
        </div>

        <input
          type="text"
          value={testInput}
          onChange={e => setTestInput(e.target.value)}
          placeholder="Speak or type a trigger phrase (e.g. 'meeting link', 'my intro')..."
          className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none font-mono"
        />

        <div className="p-3 rounded-lg bg-slate-50 border border-slate-200 flex items-start gap-2 text-xs font-mono">
          <span className="text-slate-500 shrink-0 font-medium">Result:</span>
          <span className="text-[#0284c7] font-semibold break-all leading-relaxed">
            {testResult || '<empty>'}
          </span>
        </div>
      </div>

      {/* 4. Snippets Grid Stream */}
      <div className="bg-white border border-[#e2e8f0] rounded-xl p-5 shadow-xs">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-bold text-slate-900">
            Registered Snippets ({filteredSnippets.length})
          </h2>
          <span className="text-[11px] text-slate-500 font-mono">
            Zero-allocation string lookup
          </span>
        </div>

        {filteredSnippets.length === 0 ? (
          <div className="text-center py-12 text-xs text-slate-500">
            No snippets found matching your query.
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {filteredSnippets.map(snip => (
              <div
                key={snip.id}
                className="bg-slate-50/70 border border-slate-200 rounded-xl p-3.5 flex flex-col justify-between gap-3 hover:border-sky-300 hover:bg-slate-50 transition-colors"
              >
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[15px] text-[#0284c7]">mic</span>
                    <span className="px-2 py-0.5 rounded bg-sky-50 text-[#0284c7] border border-sky-200 font-mono text-xs font-bold">
                      "{snip.trigger}"
                    </span>
                  </div>

                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleCopy(snip.expansion, snip.id)}
                      className="w-7 h-7 rounded flex items-center justify-center bg-white border border-slate-200 text-slate-600 hover:text-slate-900 hover:bg-slate-100 transition-colors shadow-2xs"
                      title="Copy expansion text"
                    >
                      <span className="material-symbols-outlined text-[14px]">
                        {copiedId === snip.id ? 'check' : 'content_copy'}
                      </span>
                    </button>

                    <button
                      onClick={() => onDeleteSnippet(snip.id)}
                      className="w-7 h-7 rounded flex items-center justify-center bg-white border border-slate-200 text-slate-400 hover:text-red-600 hover:bg-red-50 hover:border-red-200 transition-colors shadow-2xs"
                      title="Delete snippet"
                    >
                      <span className="material-symbols-outlined text-[14px]">delete</span>
                    </button>
                  </div>
                </div>

                <div className="bg-white border border-slate-200 rounded-lg p-2.5 text-xs text-slate-800 font-mono leading-relaxed break-words shadow-2xs">
                  {snip.expansion}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
