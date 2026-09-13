import React, { useState } from 'react';
import { ScratchpadEntry } from '../types';
import { Search, Plus, RotateCw, Info, FileText, Trash2, Edit3, Check, X, Maximize2 } from 'lucide-react';

interface ScratchpadTabProps {
  notes: ScratchpadEntry[];
  activeNoteId: string;
  onSelectNote: (id: string) => void;
  onSaveNoteContent: (id: string, title: string, content: string) => void;
  onCreateNote: () => void;
  onDeleteNote: (id: string) => void;
}

export const ScratchpadTab: React.FC<ScratchpadTabProps> = ({
  notes,
  activeNoteId,
  onSelectNote,
  onSaveNoteContent,
  onCreateNote,
  onDeleteNote,
}) => {
  const [addToFlowBar, setAddToFlowBar] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [isEditingModalOpen, setIsEditingModalOpen] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editContent, setEditContent] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);

  const activeNote = notes.find(n => n.id === activeNoteId) || notes[0];

  const handleStartNewNote = () => {
    onCreateNote();
    setEditTitle('Untitled Note');
    setEditContent('');
    setEditingId(`note-${Date.now()}`);
    setIsEditingModalOpen(true);
  };

  const handleOpenEdit = (note: ScratchpadEntry) => {
    onSelectNote(note.id);
    setEditingId(note.id);
    setEditTitle(note.title);
    setEditContent(note.content);
    setIsEditingModalOpen(true);
  };

  const handleSaveModal = () => {
    if (editingId) {
      onSaveNoteContent(editingId, editTitle || 'Untitled Note', editContent);
    }
    setIsEditingModalOpen(false);
  };

  const filteredNotes = notes.filter(n => {
    if (!searchQuery.trim()) return true;
    return (
      n.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      n.content.toLowerCase().includes(searchQuery.toLowerCase())
    );
  });

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Top Header & Right Controls matching Screenshot 5 */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-2.5">
          <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
            Scratchpad
          </h1>
          <span className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-[#1c1917] text-white">
            Beta
          </span>
        </div>

        {/* Right Toggle & Shortcut Pill */}
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 text-xs text-[#57534e]">
            <span>Add to Flow Bar</span>
            <Info className="w-3.5 h-3.5 text-[#a8a29e]" />
            <button
              onClick={() => setAddToFlowBar(!addToFlowBar)}
              className={`w-10 h-5.5 rounded-full p-0.5 transition-colors relative ${
                addToFlowBar ? 'bg-[#134e4a]' : 'bg-[#d6cfc4]'
              }`}
            >
              <div
                className={`w-4.5 h-4.5 rounded-full bg-white transition-transform ${
                  addToFlowBar ? 'translate-x-4.5' : 'translate-x-0'
                }`}
              />
            </button>
          </div>

          <button
            onClick={() => alert('Shortcut Alt + N registered for Scratchpad quick capture!')}
            className="px-4 py-2 rounded-full bg-[#ede8e1] hover:bg-[#e4ded5] text-[#1c1917] text-xs font-semibold transition-colors"
          >
            Click to enable shortcut
          </button>
        </div>
      </div>

      {/* Hero Banner Card matching Screenshot 5 */}
      <div className="relative rounded-2xl overflow-hidden shadow-xs min-h-[220px] flex items-center justify-between p-8 text-white">
        {/* Warm Golden/Amber Sticky Notes blurred backdrop */}
        <div 
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: `radial-gradient(circle at 75% 50%, rgba(245, 158, 11, 0.45), transparent 70%),
                              radial-gradient(circle at 20% 80%, rgba(180, 83, 9, 0.4), transparent 60%),
                              linear-gradient(120deg, #1c1917 0%, #292524 50%, #78350f 100%)`
          }}
        >
          <div className="absolute inset-0 bg-black/35 backdrop-blur-[2px]"></div>
        </div>

        {/* Banner Left Content */}
        <div className="relative z-10 max-w-md space-y-2">
          <h2 className="text-[28px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
            For quick thoughts you want to come back to
          </h2>
          <p className="text-[13.5px] text-white/90 font-normal leading-relaxed">
            Drop a to-do list, polish a message before you send it, brain dump an idea. Scratchpad is your safe space to save, create, and explore.
          </p>

          <div className="pt-2">
            <button
              onClick={handleStartNewNote}
              className="px-5 py-2.5 rounded-full bg-white text-[#1c1917] text-[13.5px] font-semibold hover:bg-[#f7f5f2] active:scale-98 transition-all shadow-xs"
            >
              Start new note
            </button>
          </div>
        </div>

        {/* Mini Graphic Window Mockup matching Screenshot 5 */}
        <div className="relative z-10 hidden md:block w-72 bg-white/95 text-[#1c1917] rounded-xl p-3.5 shadow-xl border border-white/20 text-xs backdrop-blur-md">
          <div className="flex items-center justify-between border-b border-[#ede8e1] pb-2 mb-2 text-[#78716c]">
            <div className="flex items-center gap-2">
              <span>✕</span>
              <span>Untitled</span>
              <span>+</span>
            </div>
            <Maximize2 className="w-3 h-3" />
          </div>
          <p className="text-[11.5px] text-[#44403c] leading-relaxed line-clamp-4 font-normal">
            The core principle is about agency and stakes. A progress bar says "you have a shrinking window to do something" — it puts the user in flow.
          </p>
        </div>
      </div>

      {/* Recents Section Header & Controls */}
      <div className="space-y-4 pt-2">
        <div className="flex items-center justify-between border-b border-[#ede8e1] pb-2">
          <h3 className="text-base font-bold text-[#1c1917]">
            Recents
          </h3>

          <div className="flex items-center gap-2">
            <div className="relative">
              <input
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Search notes..."
                className="pl-8 pr-3 py-1 text-xs rounded-lg bg-[#f7f5f2] border border-[#ede8e1] focus:outline-none focus:ring-1 focus:ring-[#1c1917] w-36 transition-all focus:w-48"
              />
              <Search className="w-3.5 h-3.5 text-[#a8a29e] absolute left-2.5 top-2" />
            </div>

            <button
              onClick={handleStartNewNote}
              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#78716c] hover:text-[#1c1917] hover:bg-[#ede8e1] transition-colors"
              title="Add note"
            >
              <Plus className="w-4 h-4 stroke-[2.2]" />
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

        {/* Notes Grid or Empty State */}
        {filteredNotes.length === 0 ? (
          <div className="py-20 text-center text-[#a8a29e] text-sm">
            No notes found
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {filteredNotes.map(note => (
              <div
                key={note.id}
                onClick={() => handleOpenEdit(note)}
                className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-5 hover:border-[#d6cfc4] hover:shadow-xs transition-all cursor-pointer flex flex-col justify-between min-h-[170px] group"
              >
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <h4 className="font-bold text-[#1c1917] text-sm truncate">
                      {note.title || 'Untitled Note'}
                    </h4>
                    <button
                      onClick={e => {
                        e.stopPropagation();
                        onDeleteNote(note.id);
                      }}
                      className="w-6 h-6 rounded-md opacity-0 group-hover:opacity-100 flex items-center justify-center text-[#78716c] hover:text-rose-600 hover:bg-rose-50 transition-all"
                      title="Delete note"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>

                  <p className="text-xs text-[#57534e] line-clamp-3 leading-relaxed">
                    {note.content || 'Empty note. Click to start typing or dictating.'}
                  </p>
                </div>

                <div className="flex items-center justify-between text-[11px] text-[#a8a29e] pt-3 border-t border-[#ede8e1]">
                  <span>{new Date(note.updatedAt || note.createdAt).toLocaleDateString()}</span>
                  <span>{note.wordCount || 0} words</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Note Editor Drawer / Modal */}
      {isEditingModalOpen && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-2xl shadow-2xl border border-[#ede8e1] space-y-4">
            <div className="flex items-center justify-between border-b border-[#ede8e1] pb-3">
              <input
                type="text"
                value={editTitle}
                onChange={e => setEditTitle(e.target.value)}
                placeholder="Note Title"
                className="text-lg font-bold text-[#1c1917] focus:outline-none w-full"
              />
              <button
                onClick={() => setIsEditingModalOpen(false)}
                className="w-8 h-8 rounded-lg flex items-center justify-center text-[#78716c] hover:bg-[#ede8e1]"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <textarea
              value={editContent}
              onChange={e => setEditContent(e.target.value)}
              placeholder="Drop thoughts, paste links, or dictate with Flow..."
              rows={12}
              className="w-full text-sm leading-relaxed text-[#1c1917] focus:outline-none resize-none"
            />

            <div className="flex items-center justify-between pt-3 border-t border-[#ede8e1]">
              <span className="text-xs text-[#78716c]">
                {editContent.trim() ? editContent.trim().split(/\s+/).length : 0} words • {editContent.length} characters
              </span>
              <button
                onClick={handleSaveModal}
                className="px-5 py-2 rounded-xl bg-[#1c1917] text-white text-xs font-semibold hover:bg-black transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
