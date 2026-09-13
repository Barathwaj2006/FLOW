import React, { useState } from 'react';
import { StyleProfile } from '../types';
import { sanitizeAndFormat } from '../lib/sanitizer';

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
  const [testRawText, setTestRawText] = useState("um i don't think we can't ship this today comma because camel case user id isn't ready period");

  const getStyleIcon = (style: StyleProfile) => {
    if (style.isDeveloperMode) return 'code';
    if (style.formalityLevel === 'formal') return 'business_center';
    if (style.formalityLevel === 'casual') return 'chat_bubble';
    if (style.useBulletPoints) return 'format_list_bulleted';
    return 'article';
  };

  return (
    <div id="styles-tab-content" className="flex flex-col w-full pb-16 max-w-6xl mx-auto space-y-5 select-none pt-1">
      {/* 1. Top Header */}
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Writing Styles &amp; Tone Profiles</h1>
          <span className="px-2 py-0.5 rounded bg-[#e0f2fe] text-[#0284c7] border border-[#bae6fd] text-[11px] font-medium">
            Dynamic Context Rules
          </span>
        </div>
        <p className="text-xs text-slate-500 max-w-2xl">
          Choose your default dictation tone or assign specific formatting rules to active Windows applications (e.g. Developer Mode for VS Code, Formal for Outlook).
        </p>
      </div>

      {/* 2. Style Profile Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
        {styles.map(style => {
          const isSelected = style.id === activeStyleId;
          return (
            <div
              key={style.id}
              onClick={() => onSelectActiveStyle(style.id)}
              className={`cursor-pointer rounded-xl p-4.5 border transition-all duration-150 flex flex-col justify-between ${
                isSelected
                  ? 'bg-sky-50/60 border-[#0284c7] shadow-sm ring-1 ring-[#0284c7]'
                  : 'bg-white border-slate-200 hover:border-slate-300 hover:bg-slate-50/70 shadow-xs'
              }`}
            >
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className={`w-7 h-7 rounded-lg flex items-center justify-center ${
                      isSelected ? 'bg-[#0284c7] text-white' : 'bg-slate-100 text-slate-600'
                    }`}>
                      <span className="material-symbols-outlined text-[18px]">
                        {getStyleIcon(style)}
                      </span>
                    </div>
                    <h3 className="text-sm font-bold text-slate-900">{style.name}</h3>
                  </div>

                  {isSelected && (
                    <span className="px-2 py-0.5 rounded-full bg-[#0284c7] text-white text-[10px] font-semibold flex items-center gap-1">
                      <span className="material-symbols-outlined text-[12px]">check</span>
                      <span>Active</span>
                    </span>
                  )}
                </div>

                <p className="text-xs text-slate-600 leading-relaxed pt-1">
                  {style.description}
                </p>
              </div>

              <div className="mt-4 pt-3 border-t border-slate-100 flex flex-wrap items-center gap-1.5">
                <span className="text-[10px] text-slate-400 font-medium">Mapped Apps:</span>
                {style.appMappings.map((app, idx) => (
                  <span
                    key={idx}
                    className="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-200 text-slate-700 text-[10px] font-mono"
                  >
                    {app}
                  </span>
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {/* 3. Live Style Comparison Playground */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">compare_arrows</span>
            <h2 className="text-sm font-bold text-slate-900">Multi-Style Real-Time Formatting Preview</h2>
          </div>
          <span className="text-[11px] text-slate-500">Live preview of all active profiles</span>
        </div>

        <div>
          <label className="block text-xs text-slate-700 mb-1 font-medium">
            Test Speech Input (with filler words, spoken punctuation, and variable tokens):
          </label>
          <input
            type="text"
            value={testRawText}
            onChange={e => setTestRawText(e.target.value)}
            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none font-mono"
          />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5 pt-1">
          {styles.map(style => {
            const formatted = sanitizeAndFormat(testRawText, { style });
            const isCurrent = style.id === activeStyleId;
            return (
              <div
                key={style.id}
                className={`p-3.5 rounded-lg border text-xs font-mono space-y-1.5 ${
                  isCurrent 
                    ? 'bg-sky-50/50 border-[#0284c7]/50 shadow-2xs' 
                    : 'bg-slate-50/80 border-slate-200'
                }`}
              >
                <div className="flex items-center justify-between text-xs">
                  <span className="font-semibold text-slate-900 font-sans flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[16px] text-[#0284c7]">
                      {getStyleIcon(style)}
                    </span>
                    <span>{style.name}</span>
                  </span>
                  {isCurrent && (
                    <span className="text-[10px] text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.2 rounded font-bold font-sans">
                      Active
                    </span>
                  )}
                </div>
                <div className="text-slate-800 break-words pt-1 font-sans text-xs leading-relaxed">
                  {formatted || '<empty>'}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
