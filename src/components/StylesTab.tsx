import React, { useState } from 'react';
import { StyleProfile } from '../types';
import { Check, Plus } from 'lucide-react';

interface StylesTabProps {
  styles: StyleProfile[];
  activeStyleId: string;
  onSelectActiveStyle: (id: string) => void;
}

export const StylesTab: React.FC<StylesTabProps> = ({
  styles,
  activeStyleId,
  onSelectActiveStyle,
}) => {
  const [activeCategory, setActiveCategory] = useState<'personal' | 'work' | 'email' | 'other' | 'auto'>('personal');
  const [selectedPreset, setSelectedPreset] = useState<'formal' | 'casual' | 'very-casual'>('formal');

  const categories = [
    { id: 'personal', label: 'Personal messages' },
    { id: 'work', label: 'Work messages' },
    { id: 'email', label: 'Email' },
    { id: 'other', label: 'Other' },
    { id: 'auto', label: 'Auto cleanup', badge: 'Beta' },
  ];

  const presets = [
    {
      id: 'formal',
      title: 'Formal.',
      subtitle: 'Caps + Punctuation',
      sampleText: "Hey, are you free for lunch tomorrow? Let's do 12 if that works for you.",
      avatarBg: 'bg-[#f3e8ff]',
      avatarText: 'text-[#6b21a8]',
      linkedStyleId: 'style-formal',
    },
    {
      id: 'casual',
      title: 'Casual',
      subtitle: 'Caps + Less punctuation',
      sampleText: "Hey are you free for lunch tomorrow? Let's do 12 if that works for you",
      avatarBg: 'bg-[#fce7f3]',
      avatarText: 'text-[#9d174d]',
      linkedStyleId: 'style-balanced',
    },
    {
      id: 'very-casual',
      title: 'very casual',
      subtitle: 'No Caps + Less punctuation',
      sampleText: "hey are you free for lunch tomorrow? let's do 12 if that works for you",
      avatarBg: 'bg-[#881337]',
      avatarText: 'text-white',
      linkedStyleId: 'style-casual',
    },
  ];

  const handleSelectPreset = (presetId: 'formal' | 'casual' | 'very-casual', linkedStyleId: string) => {
    setSelectedPreset(presetId);
    onSelectActiveStyle(linkedStyleId);
  };

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Title */}
      <div className="flex items-center justify-between">
        <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
          Style
        </h1>
      </div>

      {/* Category Tabs matching Screenshot 4 */}
      <div className="flex items-center gap-6 border-b border-[#ede8e1] overflow-x-auto">
        {categories.map(cat => (
          <button
            key={cat.id}
            onClick={() => setActiveCategory(cat.id as any)}
            className={`pb-3 text-[14.5px] font-semibold transition-colors relative whitespace-nowrap flex items-center gap-1.5 ${
              activeCategory === cat.id
                ? 'text-[#1c1917]'
                : 'text-[#78716c] hover:text-[#1c1917]'
            }`}
          >
            <span>{cat.label}</span>
            {cat.badge && (
              <span className="px-2 py-0.5 text-[10px] font-bold rounded-full bg-[#f3e8ff] text-[#7e22ce]">
                {cat.badge}
              </span>
            )}
            {activeCategory === cat.id && (
              <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
            )}
          </button>
        ))}
      </div>

      {/* Hero Banner Card matching Screenshot 4 */}
      <div className="relative rounded-2xl overflow-hidden shadow-xs min-h-[160px] flex items-center justify-between p-7 text-white">
        {/* Warm Cinematic Blurred Background */}
        <div 
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: `radial-gradient(circle at 10% 50%, rgba(180, 83, 9, 0.4), transparent 70%),
                              radial-gradient(circle at 90% 20%, rgba(30, 64, 175, 0.35), transparent 60%),
                              linear-gradient(115deg, #1c1917 0%, #292524 60%, #1e1b4b 100%)`
          }}
        >
          <div className="absolute inset-0 bg-black/25 backdrop-blur-[2px]"></div>
        </div>

        {/* Banner Left Content */}
        <div className="relative z-10 max-w-lg space-y-1.5">
          <h2 className="text-[26px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
            This style applies in personal messengers
          </h2>
          <p className="text-[13.5px] text-white/80 font-normal leading-relaxed">
            Style formatting only applies in English. More languages coming soon.
          </p>
        </div>

        {/* Messenger App Icons matching Screenshot 4 */}
        <div className="relative z-10 hidden sm:flex items-center -space-x-1.5 shrink-0">
          {/* WhatsApp */}
          <div className="w-10 h-10 rounded-full bg-[#25D366] ring-2 ring-white/30 flex items-center justify-center text-white shadow-md">
            <svg viewBox="0 0 24 24" className="w-5 h-5 fill-current">
              <path d="M12.04 2c-5.46 0-9.91 4.45-9.91 9.91 0 1.75.46 3.45 1.32 4.95L2.05 22l5.25-1.38c1.45.79 3.08 1.21 4.74 1.21 5.46 0 9.91-4.45 9.91-9.91 0-2.65-1.03-5.14-2.9-7.01A9.816 9.816 0 0 0 12.04 2z"/>
            </svg>
          </div>
          {/* Telegram */}
          <div className="w-10 h-10 rounded-full bg-[#229ED9] ring-2 ring-white/30 flex items-center justify-center text-white shadow-md">
            <svg viewBox="0 0 24 24" className="w-5 h-5 fill-current">
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8c-.15 1.58-.8 5.42-1.13 7.19-.14.75-.42 1-.68 1.03-.58.05-1.02-.38-1.58-.75-.88-.58-1.38-.94-2.23-1.5-.99-.65-.35-1.01.22-1.59.15-.15 2.71-2.48 2.76-2.69a.2.2 0 0 0-.05-.18c-.06-.05-.14-.03-.21-.02-.09.02-1.49.95-4.22 2.79-.4.27-.76.41-1.08.4-.36-.01-1.04-.2-1.55-.37-.63-.2-1.12-.31-1.08-.66.02-.18.27-.36.75-.55 2.92-1.27 4.86-2.11 5.83-2.51 2.78-1.16 3.35-1.36 3.73-1.36.08 0 .27.02.39.12.1.08.13.19.14.27-.01.06.01.24 0 .38z"/>
            </svg>
          </div>
          {/* Discord */}
          <div className="w-10 h-10 rounded-full bg-[#5865F2] ring-2 ring-white/30 flex items-center justify-center text-white shadow-md">
            <svg viewBox="0 0 24 24" className="w-5 h-5 fill-current">
              <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.929 1.793 8.18 1.793 12.061 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.894.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.028z"/>
            </svg>
          </div>
          {/* Instagram */}
          <div className="w-10 h-10 rounded-full bg-gradient-to-tr from-[#f09433] via-[#e6683c] to-[#bc1888] ring-2 ring-white/30 flex items-center justify-center text-white shadow-md">
            <svg viewBox="0 0 24 24" className="w-5 h-5 fill-current">
              <path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zm0-2.163c-3.259 0-3.667.014-4.947.072-4.358.2-6.78 2.618-6.98 6.98-.059 1.281-.073 1.689-.073 4.948 0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98 1.281.058 1.689.072 4.948.072 3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98-1.281-.059-1.69-.073-4.949-.073zm0 5.838c-3.403 0-6.162 2.759-6.162 6.162s2.759 6.163 6.162 6.163 6.162-2.759 6.162-6.163c0-3.403-2.759-6.162-6.162-6.162zm0 10.162c-2.209 0-4-1.79-4-4 0-2.209 1.791-4 4-4s4 1.791 4 4c0 2.21-1.791 4-4 4zm6.406-11.845c-.796 0-1.441.645-1.441 1.44s.645 1.44 1.441 1.44c.795 0 1.439-.645 1.439-1.44s-.644-1.44-1.439-1.44z"/>
            </svg>
          </div>
          {/* Add app */}
          <div className="w-10 h-10 rounded-full bg-white/20 backdrop-blur-md ring-2 ring-white/30 flex items-center justify-center text-white shadow-md">
            <Plus className="w-4 h-4" />
          </div>
        </div>
      </div>

      {/* 3 Interactive Style Cards matching Screenshot 4 */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5 items-stretch pt-2">
        {presets.map(preset => {
          const isSelected = selectedPreset === preset.id;
          return (
            <div
              key={preset.id}
              onClick={() => handleSelectPreset(preset.id as any, preset.linkedStyleId)}
              className={`rounded-2xl p-6 cursor-pointer transition-all duration-200 flex flex-col justify-between min-h-[300px] ${
                isSelected
                  ? 'border-2 border-[#9333ea] bg-white shadow-md ring-4 ring-[#9333ea]/10'
                  : 'border border-[#ede8e1] bg-[#f9f8f6] hover:border-[#d6cfc4]'
              }`}
            >
              {/* Card Header */}
              <div className="space-y-1">
                <div className="flex items-center justify-between">
                  <h3 className="text-[17px] font-bold text-[#1c1917]">
                    {preset.title}
                  </h3>
                  {isSelected && (
                    <div className="w-5 h-5 rounded-full bg-[#9333ea] text-white flex items-center justify-center">
                      <Check className="w-3.5 h-3.5 stroke-[3]" />
                    </div>
                  )}
                </div>
                <p className="text-[13px] text-[#78716c] font-normal">
                  {preset.subtitle}
                </p>
              </div>

              {/* Chat Message Bubble & Avatar matching Screenshot 4 */}
              <div className="pt-6 space-y-3">
                <div className="bg-[#f7f5f2] border border-[#ede8e1] rounded-2xl rounded-tr-xs p-4 text-[13.5px] leading-relaxed text-[#292524]">
                  {preset.sampleText}
                </div>

                {/* Sender Avatar badge at bottom right */}
                <div className="flex justify-end">
                  <div className={`w-9 h-9 rounded-full flex items-center justify-center font-bold text-sm shadow-xs ${preset.avatarBg} ${preset.avatarText}`}>
                    J
                  </div>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
