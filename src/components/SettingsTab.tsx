import React from 'react';
import { FlowSettings } from '../types';

interface SettingsTabProps {
  settings: FlowSettings;
  onUpdateSettings: (newSettings: Partial<FlowSettings>) => void;
}

export const SettingsTab: React.FC<SettingsTabProps> = ({
  settings,
  onUpdateSettings,
}) => {
  const languages = [
    { code: 'en-US', name: 'English (US)' },
    { code: 'en-GB', name: 'English (UK)' },
    { code: 'ta-IN', name: 'Tamil (தமிழ்)' },
    { code: 'hi-IN', name: 'Hindi (हिन्दी)' },
    { code: 'es-ES', name: 'Spanish (Español)' },
    { code: 'fr-FR', name: 'French (Français)' },
    { code: 'de-DE', name: 'German (Deutsch)' },
    { code: 'auto', name: 'Auto-Detect' },
  ];

  const audioDevices = [
    'Default Windows Audio Endpoint (WASAPI)',
    'Realtek High Definition Audio (Array Mic)',
    'USB Audio Device / Headset Microphone',
    'Virtual Audio Cable Loopback (DirectML)',
  ];

  return (
    <div id="settings-tab-content" className="flex flex-col w-full pb-16 max-w-4xl mx-auto space-y-4 select-none pt-1">
      {/* Top Banner */}
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">System &amp; Dictation Preferences</h1>
          <span className="px-2 py-0.5 rounded bg-[#e0f2fe] text-[#0284c7] border border-[#bae6fd] text-[11px] font-medium">
            Windows 11 Native
          </span>
        </div>
        <p className="text-xs text-slate-500 max-w-2xl">
          Configure local WASAPI audio endpoints, push-to-talk hotkeys, multilingual recognition, and privacy retention rules.
        </p>
      </div>

      {/* 1. Audio & Microphone Section */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">mic</span>
          <h2>Audio Capture &amp; WASAPI Endpoint</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs text-slate-700 mb-1 font-medium">
              Audio Input Device:
            </label>
            <div className="relative">
              <select
                value={settings.activeMic}
                onChange={e => onUpdateSettings({ activeMic: e.target.value })}
                className="w-full h-9 bg-slate-50 border border-slate-200 rounded-lg px-3 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none appearance-none cursor-pointer"
              >
                {audioDevices.map(d => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
              <span className="material-symbols-outlined absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 text-[16px] pointer-events-none">
                expand_more
              </span>
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-xs text-slate-700 font-medium">
                VAD Input Sensitivity (Energy Threshold):
              </label>
              <span className="text-xs text-[#0284c7] font-mono font-bold">{settings.sensitivity}%</span>
            </div>
            <input
              type="range"
              min={10}
              max={100}
              value={settings.sensitivity}
              onChange={e => onUpdateSettings({ sensitivity: Number(e.target.value) })}
              className="w-full accent-[#0284c7] cursor-pointer mt-1"
            />
            <div className="flex justify-between text-[10px] text-slate-400 mt-1">
              <span>Low (Quiet room)</span>
              <span>High (Noisy background)</span>
            </div>
          </div>
        </div>
      </div>

      {/* 2. Keyboard Hotkeys Section */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">keyboard</span>
          <h2>Global Windows System Hotkeys</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3.5 space-y-1.5">
            <span className="text-xs text-slate-500 font-medium">Push-to-Talk Hotkey</span>
            <div>
              <kbd className="px-2 py-1 rounded bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs">
                {settings.hotkey}
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500 leading-normal">Hold to speak; release to insert formatted text.</p>
          </div>

          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3.5 space-y-1.5">
            <span className="text-xs text-slate-500 font-medium">Hands-Free Toggle</span>
            <div>
              <kbd className="px-2 py-1 rounded bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs">
                {settings.handsFreeHotkey}
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500 leading-normal">Double-tap to start; tap once more to stop.</p>
          </div>

          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3.5 space-y-1.5">
            <span className="text-xs text-slate-500 font-medium">Backtrack Undo</span>
            <div>
              <kbd className="px-2 py-1 rounded bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs">
                {settings.backtrackHotkey}
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500 leading-normal">Instantly reverts previous insertion in active app.</p>
          </div>
        </div>
      </div>

      {/* 3. Language Selection */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-3">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">language</span>
          <h2>Recognition Language</h2>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
          {languages.map(lang => {
            const isSelected = settings.language === lang.name;
            return (
              <button
                key={lang.code}
                onClick={() => onUpdateSettings({ language: lang.name })}
                className={`p-2.5 rounded-lg border text-xs font-medium transition-all text-left ${
                  isSelected
                    ? 'bg-sky-50 border-[#0284c7] text-[#0284c7] font-semibold shadow-2xs'
                    : 'bg-slate-50 border-slate-200 text-slate-700 hover:bg-slate-100'
                }`}
              >
                {lang.name}
              </button>
            );
          })}
        </div>
      </div>

      {/* 4. Safety & Invariant Guarantees */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-emerald-600 text-[18px]">verified_user</span>
          <h2>Inviolable Safety &amp; Privacy Invariants</h2>
        </div>

        <div className="bg-emerald-50/70 border border-emerald-200 rounded-lg p-4 space-y-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-emerald-600 text-[18px]">shield</span>
              <span className="text-xs font-bold text-slate-900">Zero-Enter Invariant (Enforced Hardware Lock)</span>
            </div>
            <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-300 text-[10px] font-bold uppercase tracking-wider">
              Enforced
            </span>
          </div>
          <p className="text-xs text-slate-600 leading-relaxed">
            FLOW's text insertion engine strictly prevents simulation of <code className="text-emerald-800 font-mono font-bold bg-emerald-100/60 px-1 py-0.2 rounded">VK_RETURN (0x0D)</code>, Keypad Enter, or accidental form submit buttons. Text is placed cleanly at cursor position without submitting forms or sending messages.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
          <div>
            <label className="block text-xs text-slate-700 mb-1 font-medium">
              Local History Retention Policy:
            </label>
            <div className="relative">
              <select
                value={settings.retentionPolicy}
                onChange={e => onUpdateSettings({ retentionPolicy: e.target.value as any })}
                className="w-full h-9 bg-slate-50 border border-slate-200 rounded-lg px-3 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none appearance-none cursor-pointer"
              >
                <option value="30days">Auto-delete after 30 days</option>
                <option value="90days">Auto-delete after 90 days</option>
                <option value="unlimited">Unlimited (Keep all local records)</option>
              </select>
              <span className="material-symbols-outlined absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 text-[16px] pointer-events-none">
                expand_more
              </span>
            </div>
          </div>

          <div className="flex items-center justify-between p-3 rounded-lg bg-slate-50 border border-slate-200">
            <div className="space-y-0.5">
              <span className="text-xs font-medium text-slate-900 flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[16px] text-[#0284c7]">volume_up</span>
                <span>Audio Tone Feedback</span>
              </span>
              <p className="text-[11px] text-slate-500">Play subtle chime on start and completion.</p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={settings.soundFeedback}
                onChange={e => onUpdateSettings({ soundFeedback: e.target.checked })}
                className="sr-only peer"
              />
              <div className="w-9 h-5 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
              <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
            </label>
          </div>
        </div>
      </div>
    </div>
  );
};
