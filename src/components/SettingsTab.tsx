import React, { useState } from 'react';
import { FlowSettings } from '../types';

interface SettingsTabProps {
  settings: FlowSettings;
  onUpdateSettings: (newSettings: Partial<FlowSettings>) => void;
  showFloatingHud: boolean;
  onToggleFloatingHud: () => void;
  onReplayOnboarding: () => void;
  onClearHistory: () => void;
  multiMonitorMode: 'primary' | 'secondary' | 'all';
  onChangeMultiMonitorMode: (mode: 'primary' | 'secondary' | 'all') => void;
  bottomOffset: number;
  onChangeBottomOffset: (offset: number) => void;
  availableMics?: string[];
}

export const SettingsTab: React.FC<SettingsTabProps> = ({
  settings,
  onUpdateSettings,
  showFloatingHud,
  onToggleFloatingHud,
  onReplayOnboarding,
  onClearHistory,
  multiMonitorMode,
  onChangeMultiMonitorMode,
  bottomOffset,
  onChangeBottomOffset,
  availableMics = [],
}) => {
  const [clearedConfirm, setClearedConfirm] = useState(false);
  const [themeMode, setThemeMode] = useState<'light' | 'dark'>('light');
  const [startMinimized, setStartMinimized] = useState(true);
  const [launchAtStartup, setLaunchAtStartup] = useState(true);

  const languages = [
    { code: 'auto', name: 'Auto-Detect' },
    { code: 'en-US', name: 'English (US)' },
    { code: 'en-GB', name: 'English (UK)' },
    { code: 'ta-IN', name: 'Tamil (தமிழ்)' },
    { code: 'hi-IN', name: 'Hindi (हिन्दी)' },
    { code: 'es-ES', name: 'Spanish (Español)' },
    { code: 'fr-FR', name: 'French (Français)' },
    { code: 'de-DE', name: 'German (Deutsch)' },
  ];

  const defaultDevices = [
    'Default Windows Audio Endpoint (WASAPI)',
    'Realtek High Definition Audio (Array Mic)',
    'USB Audio Device / Headset Microphone',
  ];

  const audioDevices = availableMics.length > 0 ? availableMics : defaultDevices;

  const handleClearHistoryClick = () => {
    if (clearedConfirm) {
      onClearHistory();
      setClearedConfirm(false);
    } else {
      setClearedConfirm(true);
      setTimeout(() => setClearedConfirm(false), 4000);
    }
  };

  return (
    <div id="settings-tab-content" className="flex flex-col w-full pb-16 max-w-4xl mx-auto space-y-6 select-none pt-1">
      {/* Top Banner */}
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Settings</h1>
          <span className="px-2 py-0.5 rounded bg-[#e0f2fe] text-[#0284c7] border border-[#bae6fd] text-[11px] font-semibold">
            Windows 11
          </span>
        </div>
        <p className="text-xs text-slate-500 max-w-2xl">
          Configure dictation behavior, audio devices, global shortcuts, and floating bar preferences.
        </p>
      </div>

      {/* 1. General Preferences */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">tune</span>
          <h2>General</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {/* Theme */}
          <div>
            <label className="block text-xs text-slate-700 mb-1.5 font-medium">Appearance Theme</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => setThemeMode('light')}
                className={`py-2 px-3 rounded-xl border text-xs font-semibold flex items-center justify-center gap-2 transition ${
                  themeMode === 'light'
                    ? 'bg-sky-50 border-[#0284c7] text-[#0284c7] shadow-xs'
                    : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <span className="material-symbols-outlined text-[16px]">light_mode</span>
                <span>Light</span>
              </button>
              <button
                onClick={() => setThemeMode('dark')}
                className={`py-2 px-3 rounded-xl border text-xs font-semibold flex items-center justify-center gap-2 transition ${
                  themeMode === 'dark'
                    ? 'bg-sky-50 border-[#0284c7] text-[#0284c7] shadow-xs'
                    : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <span className="material-symbols-outlined text-[16px]">dark_mode</span>
                <span>Dark</span>
              </button>
            </div>
          </div>

          {/* Replay Introduction */}
          <div>
            <label className="block text-xs text-slate-700 mb-1.5 font-medium">First-Run Introduction</label>
            <button
              id="btn-replay-intro"
              onClick={onReplayOnboarding}
              className="w-full h-9 rounded-xl border border-slate-200 bg-slate-50 hover:bg-slate-100 text-slate-700 text-xs font-medium flex items-center justify-center gap-2 transition shadow-2xs"
            >
              <span className="material-symbols-outlined text-[16px] text-[#0284c7]">replay</span>
              <span>Replay Introduction Walkthrough</span>
            </button>
            <p className="text-[11px] text-slate-400 mt-1">
              Re-run the initial 4-step microphone and shortcut calibration.
            </p>
          </div>
        </div>

        {/* Startup Behaviors */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-slate-100">
          <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200">
            <div className="space-y-0.5">
              <span className="text-xs font-medium text-slate-900">Launch on Windows Startup</span>
              <p className="text-[11px] text-slate-500">Auto-start FLOW when signing in to Windows.</p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={launchAtStartup}
                onChange={e => setLaunchAtStartup(e.target.checked)}
                className="sr-only peer"
              />
              <div className="w-9 h-5 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
              <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
            </label>
          </div>

          <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 border border-slate-200">
            <div className="space-y-0.5">
              <span className="text-xs font-medium text-slate-900">Start Minimized to Tray</span>
              <p className="text-[11px] text-slate-500">Run quietly in system notification area.</p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={startMinimized}
                onChange={e => setStartMinimized(e.target.checked)}
                className="sr-only peer"
              />
              <div className="w-9 h-5 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
              <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
            </label>
          </div>
        </div>
      </section>

      {/* 2. Dictation & Shortcuts */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">keyboard</span>
          <h2>Dictation &amp; Shortcuts</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {/* Hold to Talk */}
          <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 space-y-1">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-900">Primary Hold-to-Talk</span>
              <span className="px-2 py-0.5 rounded bg-sky-100 text-[#0284c7] text-[10px] font-bold">Global</span>
            </div>
            <div>
              <kbd className="px-2.5 py-1 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs">
                {settings.hotkey || 'Alt + Space'}
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500 leading-normal">
              Press and hold while speaking; release to immediately transcribe.
            </p>
          </div>

          {/* Toggle Recording */}
          <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 space-y-1">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-900">Toggle Mode</span>
              <span className="px-2 py-0.5 rounded bg-sky-100 text-[#0284c7] text-[10px] font-bold">Global</span>
            </div>
            <div>
              <kbd className="px-2.5 py-1 rounded-lg bg-white border border-slate-300 text-xs font-mono font-bold text-slate-800 shadow-2xs">
                {settings.handsFreeHotkey || 'Alt + B'}
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500 leading-normal">
              Press once to start microphone; press again when done speaking.
            </p>
          </div>
        </div>

        {/* Language Selection */}
        <div className="pt-2">
          <label className="block text-xs font-medium text-slate-700 mb-2">Recognition Language</label>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            {languages.map(lang => {
              const isSelected = settings.language === lang.name;
              return (
                <button
                  key={lang.code}
                  onClick={() => onUpdateSettings({ language: lang.name })}
                  className={`p-2 rounded-xl border text-xs font-medium transition text-left flex items-center justify-between ${
                    isSelected
                      ? 'bg-sky-50 border-[#0284c7] text-[#0284c7] font-semibold shadow-2xs'
                      : 'bg-slate-50 border-slate-200 text-slate-700 hover:bg-slate-100'
                  }`}
                >
                  <span>{lang.name}</span>
                  {isSelected && <span className="material-symbols-outlined text-[14px]">check</span>}
                </button>
              );
            })}
          </div>
        </div>

        {/* Smart Formatting */}
        <div className="flex items-center justify-between p-3.5 rounded-xl bg-slate-50 border border-slate-200">
          <div className="space-y-0.5">
            <span className="text-xs font-medium text-slate-900">Smart Natural Formatting</span>
            <p className="text-[11px] text-slate-500">
              Automatically capitalizes sentences, formats punctuation, numbers, currencies, and strips filler words.
            </p>
          </div>
          <label className="relative inline-flex items-center cursor-pointer">
            <input
              type="checkbox"
              checked={settings.smartFormatting ?? true}
              onChange={e => onUpdateSettings({ smartFormatting: e.target.checked })}
              className="sr-only peer"
            />
            <div className="w-9 h-5 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
            <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
          </label>
        </div>
      </section>

      {/* 3. Audio & Microphone */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">mic</span>
          <h2>Microphone</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs text-slate-700 mb-1.5 font-medium">Input Device</label>
            <div className="relative">
              <select
                value={settings.activeMic}
                onChange={e => onUpdateSettings({ activeMic: e.target.value })}
                className="w-full h-10 bg-slate-50 border border-slate-200 rounded-xl px-3 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none appearance-none cursor-pointer"
              >
                {audioDevices.map(d => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
              <span className="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px] pointer-events-none">
                expand_more
              </span>
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-xs text-slate-700 font-medium">VAD Input Sensitivity</label>
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
              <span>Quiet environment</span>
              <span>Noisy background</span>
            </div>
          </div>
        </div>
      </section>

      {/* 4. Production Floating Bar (HUD) */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">picture_in_picture_alt</span>
            <h2>Floating Bar</h2>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-600 font-medium">
              {showFloatingHud ? 'Enabled' : 'Disabled'}
            </span>
            <button
              id="btn-toggle-floating-hud"
              onClick={onToggleFloatingHud}
              className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                showFloatingHud ? 'bg-[#0284c7]' : 'bg-slate-300'
              }`}
            >
              <span
                className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                  showFloatingHud ? 'translate-x-5' : 'translate-x-0'
                }`}
              />
            </button>
          </div>
        </div>

        <p className="text-xs text-slate-500 leading-relaxed">
          The non-activating Windows companion capsule floats above all applications, displaying real-time audio waveforms and recording status.
        </p>

        {showFloatingHud && (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
            {/* Multi-Monitor Mode */}
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">Multi-Monitor Display Target</label>
              <div className="grid grid-cols-3 gap-1.5">
                {[
                  { id: 'primary', label: 'Display 1' },
                  { id: 'secondary', label: 'Display 2' },
                  { id: 'all', label: 'All Displays' },
                ].map(mon => (
                  <button
                    key={mon.id}
                    onClick={() => onChangeMultiMonitorMode(mon.id as any)}
                    className={`py-2 px-2.5 rounded-xl border text-xs font-medium transition text-center ${
                      multiMonitorMode === mon.id
                        ? 'bg-sky-50 border-[#0284c7] text-[#0284c7] font-semibold'
                        : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    {mon.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Bottom Offset */}
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="text-xs font-medium text-slate-700">Bottom Margin Offset</label>
                <span className="text-xs font-mono font-bold text-[#0284c7]">{bottomOffset}px</span>
              </div>
              <input
                type="range"
                min={16}
                max={64}
                value={bottomOffset}
                onChange={e => onChangeBottomOffset(Number(e.target.value))}
                className="w-full accent-[#0284c7] cursor-pointer mt-1"
              />
              <div className="flex justify-between text-[10px] text-slate-400 mt-1">
                <span>16px (Close to taskbar)</span>
                <span>64px (Floating higher)</span>
              </div>
            </div>
          </div>
        )}
      </section>

      {/* 5. Privacy & Data Retention */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-emerald-600 text-[18px]">verified_user</span>
          <h2>Privacy &amp; Local Sovereignty</h2>
        </div>

        <div className="bg-emerald-50/70 border border-emerald-200 rounded-xl p-4 space-y-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-emerald-600 text-[18px]">shield</span>
              <span className="text-xs font-bold text-slate-900">Zero Cloud Audio Transmission</span>
            </div>
            <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-300 text-[10px] font-bold uppercase tracking-wider">
              Enforced
            </span>
          </div>
          <p className="text-xs text-slate-600 leading-relaxed">
            All audio captured from your microphone is processed 100% locally on your machine. Audio buffers are discarded immediately following local transcription and never leave your hardware.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
          <div>
            <label className="block text-xs text-slate-700 mb-1.5 font-medium">History Retention Policy</label>
            <div className="relative">
              <select
                value={settings.retentionPolicy}
                onChange={e => onUpdateSettings({ retentionPolicy: e.target.value as any })}
                className="w-full h-10 bg-slate-50 border border-slate-200 rounded-xl px-3 text-xs text-slate-900 focus:border-[#0284c7] focus:bg-white focus:outline-none appearance-none cursor-pointer"
              >
                <option value="30days">Auto-delete after 30 days</option>
                <option value="90days">Auto-delete after 90 days</option>
                <option value="unlimited">Keep all local records</option>
              </select>
              <span className="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px] pointer-events-none">
                expand_more
              </span>
            </div>
          </div>

          <div>
            <label className="block text-xs text-slate-700 mb-1.5 font-medium">Clear Dictation History</label>
            <button
              id="btn-clear-history"
              onClick={handleClearHistoryClick}
              className={`w-full h-10 rounded-xl border text-xs font-semibold flex items-center justify-center gap-2 transition ${
                clearedConfirm
                  ? 'bg-rose-600 border-rose-600 text-white shadow-xs'
                  : 'bg-slate-50 border-slate-200 text-rose-600 hover:bg-rose-50 hover:border-rose-200'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">
                {clearedConfirm ? 'warning' : 'delete'}
              </span>
              <span>{clearedConfirm ? 'Click again to confirm delete' : 'Clear All Local History'}</span>
            </button>
          </div>
        </div>
      </section>
    </div>
  );
};
