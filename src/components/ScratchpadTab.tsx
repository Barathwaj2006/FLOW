import React, { useState, useEffect } from 'react';
import { ScratchpadEntry } from '../types';

interface ScratchpadTabProps {
  notes: ScratchpadEntry[];
  activeNoteId: string;
  onSelectNote: (id: string) => void;
  onSaveNoteContent: (id: string, title: string, content: string) => void;
  onCreateNote: () => void;
  onDeleteNote: (id: string) => void;
  onTogglePin: (id: string) => void;
  onInsertDictationIntoScratchpad: (text: string) => void;
}

export const ScratchpadTab: React.FC<ScratchpadTabProps> = ({
  notes,
  activeNoteId,
  onSelectNote,
  onSaveNoteContent,
  onCreateNote,
  onDeleteNote,
  onTogglePin,
}) => {
  const activeNote = notes.find(n => n.id === activeNoteId) || notes[0];
  const [content, setContent] = useState(activeNote?.content || '');
  const [title, setTitle] = useState(activeNote?.title || 'Untitled Note');
  const [saveStatus, setSaveStatus] = useState<'saved' | 'saving'>('saved');
  const [copied, setCopied] = useState(false);

  // Sync state when active note changes
  useEffect(() => {
    if (activeNote) {
      setContent(activeNote.content);
      setTitle(activeNote.title);
      setSaveStatus('saved');
    }
  }, [activeNote?.id]);

  // Debounced autosave
  useEffect(() => {
    if (!activeNote) return;
    if (content === activeNote.content && title === activeNote.title) return;

    setSaveStatus('saving');
    const timer = setTimeout(() => {
      onSaveNoteContent(activeNote.id, title, content);
      setSaveStatus('saved');
    }, 600);

    return () => clearTimeout(timer);
  }, [content, title, activeNote?.id]);

  const wordCount = content.trim() ? content.trim().split(/\s+/).length : 0;
  const charCount = content.length;

  const handleCopy = () => {
    navigator.clipboard?.writeText(content);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  const handleExportMarkdown = () => {
    if (!activeNote) return;
    const blob = new Blob([content], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}.md`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div id="scratchpad-tab-content" className="flex flex-col lg:flex-row gap-4 h-[calc(100vh-140px)] max-w-6xl mx-auto select-none pt-1">
      {/* Notes Sidebar List */}
      <div className="w-full lg:w-72 bg-white border border-slate-200 rounded-xl p-3 flex flex-col justify-between shrink-0 shadow-xs">
        <div>
          <div className="flex items-center justify-between pb-2.5 mb-2 border-b border-slate-100">
            <div className="flex items-center gap-1.5 text-xs font-bold text-slate-900">
              <span className="material-symbols-outlined text-[18px] text-[#0284c7]">edit_note</span>
              <span>Scratchpad Notes</span>
            </div>
            <button
              id="btn-new-note"
              onClick={onCreateNote}
              className="h-7 px-2.5 rounded text-xs font-medium bg-[#0284c7] hover:bg-[#0369a1] text-white flex items-center gap-1 transition-all shadow-xs"
            >
              <span className="material-symbols-outlined text-[15px]">add</span>
              <span>New</span>
            </button>
          </div>

          <div className="space-y-1.5 overflow-y-auto max-h-[calc(100vh-230px)] pr-0.5">
            {notes.map(note => {
              const isSelected = note.id === activeNoteId;
              return (
                <div
                  key={note.id}
                  onClick={() => onSelectNote(note.id)}
                  className={`cursor-pointer p-3 rounded-lg border transition-all flex items-start justify-between gap-2 ${
                    isSelected
                      ? 'bg-sky-50/70 border-[#0284c7] text-slate-900 shadow-2xs'
                      : 'bg-white border-slate-200/80 text-slate-700 hover:border-slate-300 hover:bg-slate-50/70'
                  }`}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-1.5">
                      {note.isPinned && (
                        <span 
                          className="material-symbols-outlined text-[15px] text-amber-500 shrink-0"
                          style={{ fontVariationSettings: "'FILL' 1" }}
                        >
                          push_pin
                        </span>
                      )}
                      <span className="text-xs font-semibold truncate">{note.title || 'Untitled Note'}</span>
                    </div>
                    <div className="text-[10px] text-slate-400 mt-1 flex items-center gap-1.5 font-mono">
                      <span>{new Date(note.updatedAt).toLocaleDateString([], { month: 'short', day: 'numeric' })}</span>
                      <span>•</span>
                      <span>{note.wordCount} words</span>
                    </div>
                  </div>

                  <button
                    onClick={e => {
                      e.stopPropagation();
                      onTogglePin(note.id);
                    }}
                    className="p-1 rounded text-slate-400 hover:text-amber-500 transition-colors"
                    title={note.isPinned ? 'Unpin Note' : 'Pin Note'}
                  >
                    <span 
                      className={`material-symbols-outlined text-[16px] ${note.isPinned ? 'text-amber-500' : ''}`}
                      style={{ fontVariationSettings: note.isPinned ? "'FILL' 1" : "'FILL' 0" }}
                    >
                      push_pin
                    </span>
                  </button>
                </div>
              );
            })}
          </div>
        </div>

        <div className="pt-2.5 border-t border-slate-100 text-[11px] text-slate-400 flex items-center justify-between font-mono">
          <span>{notes.length} notes</span>
          <span className="text-emerald-600 font-medium">100% Offline SQLite</span>
        </div>
      </div>

      {/* Note Editor Area */}
      {activeNote ? (
        <div className="flex-1 bg-white border border-slate-200 rounded-xl p-5 flex flex-col justify-between shadow-xs min-w-0">
          {/* Editor Header */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-100">
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Note title..."
              className="bg-transparent text-lg font-bold text-slate-900 focus:outline-none placeholder:text-slate-400 flex-1 font-sans"
            />

            <div className="flex items-center gap-2 shrink-0">
              <span className={`text-[11px] font-mono px-2 py-0.5 rounded border ${
                saveStatus === 'saving' 
                  ? 'text-amber-700 bg-amber-50 border-amber-200' 
                  : 'text-emerald-700 bg-emerald-50 border-emerald-200'
              }`}>
                {saveStatus === 'saving' ? 'Saving...' : 'Saved ✓'}
              </span>

              <button
                onClick={handleCopy}
                className="h-7 px-2 rounded text-xs bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1 shadow-2xs"
                title="Copy note content"
              >
                <span className="material-symbols-outlined text-[15px]">
                  {copied ? 'check' : 'content_copy'}
                </span>
                <span>{copied ? 'Copied' : 'Copy'}</span>
              </button>

              <button
                onClick={handleExportMarkdown}
                className="h-7 px-2 rounded text-xs bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1 shadow-2xs"
                title="Export as Markdown (.md)"
              >
                <span className="material-symbols-outlined text-[15px] text-[#0284c7]">download</span>
                <span>.md</span>
              </button>

              {notes.length > 1 && (
                <button
                  onClick={() => onDeleteNote(activeNote.id)}
                  className="w-7 h-7 rounded flex items-center justify-center bg-white border border-slate-200 text-slate-400 hover:text-red-600 hover:bg-red-50 hover:border-red-200 transition-colors shadow-2xs"
                  title="Delete this note"
                >
                  <span className="material-symbols-outlined text-[15px]">delete</span>
                </button>
              )}
            </div>
          </div>

          {/* Textarea Workspace */}
          <textarea
            value={content}
            onChange={e => setContent(e.target.value)}
            placeholder="Start typing or hold [Right Alt] to dictate seamlessly into this distraction-free workspace..."
            className="flex-1 w-full bg-slate-50/60 border border-slate-200 rounded-lg p-4 text-xs text-slate-900 placeholder:text-slate-400 focus:border-[#0284c7] focus:bg-white focus:outline-none font-mono my-3 resize-none leading-relaxed select-text shadow-inner"
          />

          {/* Editor Footer Metrics */}
          <div className="flex items-center justify-between text-xs text-slate-500 pt-1 font-mono">
            <div className="flex items-center gap-3">
              <span>{wordCount} words</span>
              <span>•</span>
              <span>{charCount} characters</span>
            </div>
            <div className="flex items-center gap-1 text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
              <span className="material-symbols-outlined text-[14px]">shield</span>
              <span className="text-[11px] font-medium">Zero-Enter Protected Auto-Save</span>
            </div>
          </div>
        </div>
      ) : (
        <div className="flex-1 bg-white border border-slate-200 rounded-xl flex items-center justify-center text-xs text-slate-400">
          No note selected. Click "New" to create one.
        </div>
      )}
    </div>
  );
};
