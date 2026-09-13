import React, { useState } from 'react';
import { SnippetEntry } from '../types';
import { Search, ArrowUpDown, RotateCw, Plus, Copy, Trash2, ArrowRight, Check } from 'lucide-react';

interface SnippetsTabProps {
  snippets: SnippetEntry[];
  onAddSnippet: (entry: Omit<SnippetEntry, 'id' | 'createdAt'>) => void;
  onDeleteSnippet: (id: string) => void;
  testSnippetSanitize?: (text: string) => string;
  onNavigateToDictionary?: () => void;
}

export const SnippetsTab: React.FC<SnippetsTabProps> = ({
  snippets,
  onAddSnippet,
  onDeleteSnippet,
}) => {
  const [activeFilter, setActiveFilter] = useState<'all' | 'personal' | 'team'>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddModal, setShowAddModal] = useState(false);
  const [newTrigger, setNewTrigger] = useState('');
  const [newExpansion, setNewExpansion] = useState('');
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTrigger.trim() || !newExpansion.trim()) return;
    onAddSnippet({
      trigger: newTrigger.trim(),
      expansion: newExpansion.trim(),
    });
    setNewTrigger('');
    setNewExpansion('');
    setShowAddModal(false);
  };

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const filteredSnippets = snippets.filter(s => {
    if (!searchQuery.trim()) return true;
    return (
      s.trigger.toLowerCase().includes(searchQuery.toLowerCase()) ||
      s.expansion.toLowerCase().includes(searchQuery.toLowerCase())
    );
  });

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Top Header & Add new Button */}
      <div className="flex items-center justify-between">
        <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
          Snippets
        </h1>

        <button
          onClick={() => setShowAddModal(true)}
          className="px-4 py-2 rounded-full bg-[#1c1917] text-white text-[13px] font-semibold hover:bg-black active:scale-95 transition-all shadow-xs"
        >
          Add new
        </button>
      </div>

      {/* Sub Tabs & Action Controls */}
      <div className="flex items-center justify-between border-b border-[#ede8e1] pb-1">
        <div className="flex items-center gap-6">
          <button
            onClick={() => setActiveFilter('all')}
            className={`pb-2.5 text-[14.5px] font-semibold transition-colors relative ${
              activeFilter === 'all'
                ? 'text-[#1c1917]'
                : 'text-[#78716c] hover:text-[#1c1917]'
            }`}
          >
            All
            {activeFilter === 'all' && (
              <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
            )}
          </button>

          <button
            onClick={() => setActiveFilter('personal')}
            className={`pb-2.5 text-[14.5px] font-semibold transition-colors relative ${
              activeFilter === 'personal'
                ? 'text-[#1c1917]'
                : 'text-[#78716c] hover:text-[#1c1917]'
            }`}
          >
            Personal
            {activeFilter === 'personal' && (
              <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
            )}
          </button>

          <button
            onClick={() => setActiveFilter('team')}
            className={`pb-2.5 text-[14.5px] font-semibold transition-colors relative ${
              activeFilter === 'team'
                ? 'text-[#1c1917]'
                : 'text-[#78716c] hover:text-[#1c1917]'
            }`}
          >
            Shared with team
            {activeFilter === 'team' && (
              <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
            )}
          </button>
        </div>

        {/* Right Search, Sort, Refresh Controls */}
        <div className="flex items-center gap-2">
          <div className="relative">
            <input
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="Search..."
              className="pl-8 pr-3 py-1 text-xs rounded-lg bg-[#f7f5f2] border border-[#ede8e1] focus:outline-none focus:ring-1 focus:ring-[#1c1917] w-36 transition-all focus:w-48"
            />
            <Search className="w-3.5 h-3.5 text-[#a8a29e] absolute left-2.5 top-2" />
          </div>

          <button 
            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#78716c] hover:text-[#1c1917] hover:bg-[#ede8e1] transition-colors"
            title="Sort snippets"
          >
            <ArrowUpDown className="w-3.5 h-3.5" />
          </button>

          <button 
            onClick={() => setSearchQuery('')}
            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#78716c] hover:text-[#1c1917] hover:bg-[#ede8e1] transition-colors"
            title="Refresh"
          >
            <RotateCw className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Hero Banner Card matching Screenshot 3 */}
      <div className="relative rounded-2xl overflow-hidden shadow-xs min-h-[290px] flex flex-col justify-between p-8 text-white">
        {/* Cinematic warm blurred background */}
        <div 
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: `radial-gradient(ellipse at 30% 20%, rgba(245, 158, 11, 0.4), transparent 60%),
                              radial-gradient(circle at 80% 80%, rgba(120, 53, 15, 0.5), transparent 70%),
                              linear-gradient(130deg, #18181b 0%, #292524 50%, #451a03 100%)`
          }}
        >
          <div className="absolute inset-0 bg-black/30 backdrop-blur-[2px]"></div>
        </div>

        {/* Banner Content */}
        <div className="relative z-10 max-w-xl space-y-2">
          <h2 className="text-[32px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
            The stuff <em className="italic font-serif-editorial">you</em> shouldn’t have to re-type.
          </h2>
          <p className="text-[14px] text-white/90 font-normal leading-relaxed max-w-lg">
            Save text you type often — an email, intro, or prompt — then say a word to drop it in instantly.
          </p>
        </div>

        {/* Interactive Example Pills matching Screenshot 3 */}
        <div className="relative z-10 space-y-2.5 my-3">
          {/* Chip 1 */}
          <div className="flex items-center gap-2.5 flex-wrap">
            <span className="px-3 py-1.5 rounded-lg bg-white/20 backdrop-blur-md text-[13px] font-mono text-white/95 border border-white/10">
              "my LinkedIn"
            </span>
            <ArrowRight className="w-3.5 h-3.5 text-white/60" />
            <span className="px-3.5 py-1.5 rounded-lg bg-white/15 backdrop-blur-md text-[13px] text-white/90 border border-white/10 truncate max-w-md">
              https://www.linkedin.com/in/john-doe/
            </span>
          </div>

          {/* Chip 2 */}
          <div className="flex items-center gap-2.5 flex-wrap">
            <span className="px-3 py-1.5 rounded-lg bg-white/20 backdrop-blur-md text-[13px] font-mono text-white/95 border border-white/10">
              "rewrite prompt"
            </span>
            <ArrowRight className="w-3.5 h-3.5 text-white/60" />
            <span className="px-3.5 py-1.5 rounded-lg bg-white/15 backdrop-blur-md text-[13px] text-white/90 border border-white/10">
              Rewrite this to be more concise...
            </span>
          </div>

          {/* Chip 3 */}
          <div className="flex items-center gap-2.5 flex-wrap">
            <span className="px-3 py-1.5 rounded-lg bg-white/20 backdrop-blur-md text-[13px] font-mono text-white/95 border border-white/10">
              "intro email"
            </span>
            <ArrowRight className="w-3.5 h-3.5 text-white/60" />
            <span className="px-3.5 py-1.5 rounded-lg bg-white/15 backdrop-blur-md text-[13px] text-white/90 border border-white/10">
              Hey, would love to find some time to chat later...
            </span>
          </div>
        </div>

        {/* White Pill Button */}
        <div className="relative z-10 pt-2">
          <button 
            onClick={() => setShowAddModal(true)}
            className="px-5 py-2.5 rounded-full bg-white text-[#1c1917] text-[13.5px] font-semibold hover:bg-[#f7f5f2] active:scale-98 transition-all shadow-xs"
          >
            Add new snippet
          </button>
        </div>
      </div>

      {/* Actual Saved Snippets Collection */}
      <div className="space-y-3 pt-3">
        <div className="flex items-center justify-between">
          <h3 className="text-xs font-bold uppercase tracking-wider text-[#a8a29e]">
            Your Shortcuts ({filteredSnippets.length})
          </h3>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
          {filteredSnippets.map(snippet => (
            <div 
              key={snippet.id} 
              className="bg-[#f9f8f6] border border-[#ede8e1] rounded-xl p-4 flex flex-col justify-between hover:border-[#d6cfc4] transition-all group"
            >
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="px-2.5 py-1 rounded-md bg-[#ede8e1] text-[#1c1917] text-xs font-mono font-bold">
                    "{snippet.trigger}"
                  </span>
                  <div className="flex items-center gap-1 opacity-60 group-hover:opacity-100 transition-opacity">
                    <button
                      onClick={() => handleCopy(snippet.expansion, snippet.id)}
                      className="w-7 h-7 rounded-lg flex items-center justify-center text-[#78716c] hover:text-[#1c1917] hover:bg-[#e7e2d9]"
                      title="Copy text"
                    >
                      {copiedId === snippet.id ? (
                        <Check className="w-3.5 h-3.5 text-emerald-600" />
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                    <button
                      onClick={() => onDeleteSnippet(snippet.id)}
                      className="w-7 h-7 rounded-lg flex items-center justify-center text-[#78716c] hover:text-rose-600 hover:bg-rose-50"
                      title="Delete"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>

                <p className="text-[13.5px] text-[#44403c] leading-relaxed line-clamp-2">
                  {snippet.expansion}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Add Snippet Modal */}
      {showAddModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-xl border border-[#ede8e1] space-y-4">
            <h3 className="text-lg font-bold text-[#1c1917]">Create New Snippet</h3>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="text-xs font-bold uppercase text-[#78716c] block mb-1">
                  Spoken Trigger Word
                </label>
                <input
                  type="text"
                  value={newTrigger}
                  onChange={e => setNewTrigger(e.target.value)}
                  placeholder="e.g. my link, sign off, zoom link"
                  required
                  className="w-full px-3 py-2 text-sm rounded-xl bg-[#f7f5f2] border border-[#d6cfc4] focus:outline-none focus:ring-1 focus:ring-[#1c1917]"
                />
              </div>

              <div>
                <label className="text-xs font-bold uppercase text-[#78716c] block mb-1">
                  Expanded Text
                </label>
                <textarea
                  value={newExpansion}
                  onChange={e => setNewExpansion(e.target.value)}
                  placeholder="The exact text to type when the trigger is spoken..."
                  rows={4}
                  required
                  className="w-full px-3 py-2 text-sm rounded-xl bg-[#f7f5f2] border border-[#d6cfc4] focus:outline-none focus:ring-1 focus:ring-[#1c1917]"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 rounded-xl text-xs font-medium text-[#78716c] hover:bg-[#ede8e1]"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-5 py-2 rounded-xl text-xs font-semibold bg-[#1c1917] text-white hover:bg-black"
                >
                  Save Snippet
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
