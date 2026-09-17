import React, { useState, useEffect, useRef } from 'react';
import { FlowSettings, HardwareProfile, ModelStatusInfo, ModelDownloadProgress, UpdateStatusInfo } from '../types';
import {
  fetchHardwareProfile,
  fetchModelsList,
  startModelDownload,
  fetchDownloadProgress,
  cancelModelDownload,
  selectActiveModel,
  fetchUpdateStatus,
  checkForUpdates,
  downloadUpdate,
  applyUpdateAndRestart,
} from '../lib/flowApiClient';
import { onNativeEvent } from '../lib/nativeBridge';
import { ModelDownloadWizardModal } from './ModelDownloadWizardModal';

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
  const [themeMode, setThemeMode] = useState<'light' | 'dark'>(() => {
    return (localStorage.getItem('flow_theme_mode') as 'light' | 'dark') || 'light';
  });
  const [startMinimized, setStartMinimized] = useState<boolean>(() => {
    const saved = localStorage.getItem('flow_start_minimized');
    return saved !== null ? saved === 'true' : true;
  });
  const [launchAtStartup, setLaunchAtStartup] = useState<boolean>(() => {
    const saved = localStorage.getItem('flow_launch_at_startup');
    return saved !== null ? saved === 'true' : true;
  });

  const handleThemeChange = (mode: 'light' | 'dark') => {
    setThemeMode(mode);
    localStorage.setItem('flow_theme_mode', mode);
  };

  const handleLaunchAtStartupChange = (val: boolean) => {
    setLaunchAtStartup(val);
    localStorage.setItem('flow_launch_at_startup', String(val));
  };

  const handleStartMinimizedChange = (val: boolean) => {
    setStartMinimized(val);
    localStorage.setItem('flow_start_minimized', String(val));
  };

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

  const [hardware, setHardware] = useState<HardwareProfile | null>(null);
  const [models, setModels] = useState<ModelStatusInfo[]>([]);
  const [downloadProgress, setDownloadProgress] = useState<ModelDownloadProgress | null>(null);
  const [downloadingModelName, setDownloadingModelName] = useState<string | null>(null);
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [loadingHardware, setLoadingHardware] = useState(false);
  const pollIntervalRef = useRef<any>(null);

  // Software Updates State (Velopack)
  const [updateInfo, setUpdateInfo] = useState<UpdateStatusInfo>({
    status: 'Idle',
    currentVersion: '1.0.0',
    downloadProgressPercent: 0,
  });
  const [isCheckingUpdate, setIsCheckingUpdate] = useState(false);
  const [isDownloadingUpdate, setIsDownloadingUpdate] = useState(false);
  const [updateError, setUpdateError] = useState<string | null>(null);

  const loadHardwareAndModels = async () => {
    setLoadingHardware(true);
    try {
      const [hw, mdls] = await Promise.all([
        fetchHardwareProfile(),
        fetchModelsList(),
      ]);
      setHardware(hw);
      setModels(mdls);
    } finally {
      setLoadingHardware(false);
    }
  };

  useEffect(() => {
    loadHardwareAndModels();

    fetchUpdateStatus().then(info => setUpdateInfo(info));
    const unsubscribe = onNativeEvent('update-status-changed', (info: UpdateStatusInfo) => {
      setUpdateInfo(info);
      if (info.status === 'Downloading') {
        setIsDownloadingUpdate(true);
      } else if (info.status === 'ReadyToRestart') {
        setIsDownloadingUpdate(false);
      }
    });

    return () => {
      if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
      unsubscribe();
    };
  }, []);

  const handleCheckForUpdates = async () => {
    setIsCheckingUpdate(true);
    setUpdateError(null);
    try {
      const info = await checkForUpdates();
      setUpdateInfo(info);
      if (info.status === 'Failed' && info.errorMessage) {
        setUpdateError(info.errorMessage);
      }
    } catch (err: any) {
      setUpdateError(err?.message || 'Failed to check for updates');
    } finally {
      setIsCheckingUpdate(false);
    }
  };

  const handleDownloadUpdate = async () => {
    setIsDownloadingUpdate(true);
    setUpdateError(null);
    const success = await downloadUpdate();
    if (!success) {
      setIsDownloadingUpdate(false);
      setUpdateError('Failed to initiate update download');
    }
  };

  const handleApplyUpdate = async () => {
    await applyUpdateAndRestart();
  };

  const handleDownloadModel = async (modelName: string) => {
    setDownloadingModelName(modelName);
    const success = await startModelDownload(modelName);
    if (!success) {
      setDownloadingModelName(null);
      return;
    }

    if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
    pollIntervalRef.current = setInterval(async () => {
      const prog = await fetchDownloadProgress();
      if (!prog) return;

      setDownloadProgress(prog);

      if (prog.status === 'Ready' || (prog.percent >= 100 && !prog.isActive)) {
        clearInterval(pollIntervalRef.current);
        setDownloadingModelName(null);
        setDownloadProgress(null);
        await loadHardwareAndModels();
      } else if (prog.status === 'Failed' || prog.errorMessage) {
        clearInterval(pollIntervalRef.current);
        setDownloadingModelName(null);
        setDownloadProgress(null);
        await loadHardwareAndModels();
      }
    }, 400);
  };

  const handleCancelDownload = async () => {
    if (pollIntervalRef.current) clearInterval(pollIntervalRef.current);
    await cancelModelDownload();
    setDownloadingModelName(null);
    setDownloadProgress(null);
    await loadHardwareAndModels();
  };

  const handleSelectModel = async (modelName: string) => {
    await selectActiveModel(modelName);
    await loadHardwareAndModels();
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
                onClick={() => handleThemeChange('light')}
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
                onClick={() => handleThemeChange('dark')}
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
                onChange={e => handleLaunchAtStartupChange(e.target.checked)}
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
                onChange={e => handleStartMinimizedChange(e.target.checked)}
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

      {/* 5. Hardware Acceleration & Local Neural Models */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">neurology</span>
            <h2>Hardware Acceleration &amp; Neural Models</h2>
          </div>

          <button
            id="btn-open-model-wizard"
            onClick={() => setIsWizardOpen(true)}
            className="h-8 px-3 rounded-xl bg-sky-50 border border-sky-200 hover:bg-sky-100 text-[#0284c7] text-xs font-semibold flex items-center gap-1.5 transition shadow-2xs"
          >
            <span className="material-symbols-outlined text-[15px]">auto_fix_high</span>
            <span>Launch Setup Wizard</span>
          </button>
        </div>

        {/* Hardware Detection Badge */}
        {hardware && (
          <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 space-y-2">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-[#0284c7] text-[18px]">
                  {hardware.isDiscreteGpu ? 'developer_board' : 'memory'}
                </span>
                <span className="text-xs font-bold text-slate-800">{hardware.primaryGpuName}</span>
              </div>
              <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-200 text-[10px] font-bold uppercase tracking-wider">
                {hardware.recommendedBackend.replace('_', ' ')}
              </span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1 text-[11px] text-slate-600">
              <div>
                <span className="text-slate-400 block text-[10px]">VRAM</span>
                <span className="font-semibold text-slate-800">
                  {hardware.dedicatedVramMB > 0 ? `${hardware.dedicatedVramMB} MB` : 'Shared RAM'}
                </span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">DirectML</span>
                <span className="font-semibold text-emerald-600">
                  {hardware.directMLSupported ? 'Hardware Accelerated' : 'CPU Mode'}
                </span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">Optimal Threads</span>
                <span className="font-semibold text-slate-800">{hardware.optimalCpuThreads} Threads</span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">Architecture</span>
                <span className="font-semibold text-slate-800">{hardware.cpuArchitecture}</span>
              </div>
            </div>
          </div>
        )}

        {/* Model Cards Grid */}
        <div className="space-y-3 pt-1">
          <div className="flex items-center justify-between">
            <label className="text-xs font-medium text-slate-700">Available Whisper Models</label>
            <span className="text-[11px] text-slate-400">All models verified with SHA-256</span>
          </div>

          <div className="grid grid-cols-1 gap-3">
            {models.map(m => {
              const isDownloadingThis = downloadingModelName === m.name;
              return (
                <div
                  key={m.name}
                  className={`p-4 rounded-xl border transition ${
                    m.isActive
                      ? 'bg-sky-50/60 border-[#0284c7] shadow-xs'
                      : 'bg-slate-50/50 border-slate-200 hover:border-slate-300'
                  }`}
                >
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-bold text-slate-900">{m.displayName}</span>
                        <span className="text-[10px] px-1.5 py-0.2 rounded bg-slate-200 text-slate-700 font-mono">
                          {m.sizeMB} MB
                        </span>
                        {m.isActive && (
                          <span className="px-2 py-0.5 rounded bg-[#0284c7] text-white text-[10px] font-bold">
                            Active Engine
                          </span>
                        )}
                        {m.isValid && (
                          <span className="px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-200 text-[10px] font-bold flex items-center gap-1">
                            <span className="material-symbols-outlined text-[12px]">verified</span>
                            <span>SHA-256 Valid</span>
                          </span>
                        )}
                      </div>
                      <p className="text-[11px] text-slate-500">
                        {m.name === 'ggml-tiny.en.bin' && 'Ultra-low latency (~150ms) for English dictation on any hardware.'}
                        {m.name === 'ggml-tiny.bin' && '99 languages including Tamil, Spanish, Hindi, French with language auto-detection.'}
                        {m.name === 'ggml-base.en.bin' && 'Balanced English accuracy and speed for modern desktop workflows.'}
                        {m.name === 'ggml-small.bin' && 'High-precision recognition across regional accents and technical terms.'}
                      </p>
                      <span className="text-[10px] font-mono text-slate-400 block">
                        SHA-256: {m.sha256.substring(0, 16)}...
                      </span>
                    </div>

                    <div className="shrink-0 flex items-center gap-2">
                      {isDownloadingThis ? (
                        <div className="flex items-center gap-2">
                          <button
                            onClick={handleCancelDownload}
                            className="h-8 px-3 rounded-lg border border-rose-200 text-rose-600 hover:bg-rose-50 text-xs font-medium transition"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : m.isValid ? (
                        m.isActive ? (
                          <span className="h-8 px-3 rounded-lg bg-emerald-50 text-emerald-700 text-xs font-bold flex items-center gap-1 border border-emerald-200">
                            <span className="material-symbols-outlined text-[15px]">check_circle</span>
                            <span>Selected</span>
                          </span>
                        ) : (
                          <button
                            onClick={() => handleSelectModel(m.name)}
                            className="h-8 px-3 rounded-lg bg-white border border-slate-300 hover:border-[#0284c7] hover:text-[#0284c7] text-slate-700 text-xs font-semibold transition shadow-2xs"
                          >
                            Set as Active
                          </button>
                        )
                      ) : (
                        <button
                          onClick={() => handleDownloadModel(m.name)}
                          className="h-8 px-3 rounded-lg bg-[#0284c7] hover:bg-[#0369a1] text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-xs"
                        >
                          <span className="material-symbols-outlined text-[15px]">download</span>
                          <span>Download</span>
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Inline Download Progress */}
                  {isDownloadingThis && downloadProgress && (
                    <div className="mt-3 pt-3 border-t border-slate-200/80 space-y-2">
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-slate-700 flex items-center gap-1.5">
                          <span className="w-2 h-2 rounded-full bg-[#0284c7] animate-ping"></span>
                          <span>{downloadProgress.status}...</span>
                        </span>
                        <div className="flex items-center gap-2 font-mono text-[11px]">
                          {downloadProgress.speedMBps > 0 && (
                            <span className="text-[#0284c7] font-bold">⚡ {downloadProgress.speedMBps} MB/s</span>
                          )}
                          <span className="font-bold text-slate-800">{downloadProgress.percent}%</span>
                        </div>
                      </div>
                      <div className="h-2 w-full bg-slate-200 rounded-full overflow-hidden">
                        <div
                          className="h-full rounded-full bg-[#0284c7] transition-all duration-150"
                          style={{ width: `${Math.max(2, downloadProgress.percent)}%` }}
                        />
                      </div>
                      <div className="flex items-center justify-between text-[10px] text-slate-400">
                        <span>
                          {(downloadProgress.bytesDownloaded / (1024 * 1024)).toFixed(1)} MB /{' '}
                          {(downloadProgress.totalBytes / (1024 * 1024)).toFixed(1)} MB
                        </span>
                        <span>Resumable HTTP Range • SHA-256</span>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* 6. Privacy & Data Retention */}
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

      {/* 6. Software Updates (Velopack) */}
      <section className="bg-white border border-slate-200 rounded-2xl p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <span className="material-symbols-outlined text-[#0284c7] text-[18px]">system_update</span>
            <h2>Software Updates</h2>
          </div>
          <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-slate-100 text-slate-600 border border-slate-200">
            v{updateInfo.currentVersion}
          </span>
        </div>

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 rounded-xl bg-slate-50 border border-slate-200">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className={`w-2 h-2 rounded-full ${
                updateInfo.status === 'ReadyToRestart' ? 'bg-emerald-500 animate-pulse' :
                updateInfo.status === 'Downloading' ? 'bg-sky-500 animate-pulse' :
                updateInfo.status === 'UpdateAvailable' ? 'bg-amber-500' :
                updateInfo.status === 'Checking' ? 'bg-sky-400 animate-ping' :
                updateInfo.status === 'Failed' ? 'bg-rose-500' : 'bg-slate-400'
              }`} />
              <span className="text-xs font-semibold text-slate-900">
                {updateInfo.status === 'ReadyToRestart' && 'Update Ready to Install'}
                {updateInfo.status === 'Downloading' && `Downloading Update (${updateInfo.downloadProgressPercent}%)`}
                {updateInfo.status === 'UpdateAvailable' && `New Version Available: v${updateInfo.availableVersion}`}
                {updateInfo.status === 'Checking' && 'Checking for updates...'}
                {updateInfo.status === 'Failed' && 'Update Check Failed'}
                {updateInfo.status === 'Idle' && 'FLOW is up-to-date'}
              </span>
            </div>
            <p className="text-[11px] text-slate-500">
              {updateInfo.status === 'ReadyToRestart' && `Version ${updateInfo.availableVersion ?? 'update'} is ready. Restart FLOW to finish updating.`}
              {updateInfo.status === 'Downloading' && 'Velopack is downloading delta packages in the background.'}
              {updateInfo.status === 'UpdateAvailable' && `A new release (v${updateInfo.availableVersion}) is available on GitHub.`}
              {updateInfo.status === 'Checking' && 'Connecting to update release channels...'}
              {updateInfo.status === 'Failed' && (updateError || 'An error occurred while communicating with the update server.')}
              {updateInfo.status === 'Idle' && 'You are running the latest version with automatic delta binary patching.'}
            </p>
          </div>

          <div className="flex items-center gap-2 shrink-0">
            {updateInfo.status === 'ReadyToRestart' ? (
              <button
                onClick={handleApplyUpdate}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold rounded-xl shadow-xs transition flex items-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">restart_alt</span>
                Restart & Update
              </button>
            ) : updateInfo.status === 'UpdateAvailable' ? (
              <button
                onClick={handleDownloadUpdate}
                disabled={isDownloadingUpdate}
                className="px-4 py-2 bg-[#0284c7] hover:bg-[#0369a1] disabled:opacity-50 text-white text-xs font-semibold rounded-xl shadow-xs transition flex items-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">download</span>
                Download Update
              </button>
            ) : (
              <button
                onClick={handleCheckForUpdates}
                disabled={isCheckingUpdate || updateInfo.status === 'Downloading'}
                className="px-4 py-2 bg-slate-200 hover:bg-slate-300 disabled:opacity-50 text-slate-800 text-xs font-semibold rounded-xl transition flex items-center gap-1.5 cursor-pointer"
              >
                <span className={`material-symbols-outlined text-[16px] ${isCheckingUpdate ? 'animate-spin' : ''}`}>
                  sync
                </span>
                {isCheckingUpdate ? 'Checking...' : 'Check for Updates'}
              </button>
            )}
          </div>
        </div>

        {/* Download Progress Bar */}
        {updateInfo.status === 'Downloading' && (
          <div className="space-y-1.5">
            <div className="flex justify-between text-[11px] font-medium text-slate-600">
              <span>Downloading delta patch...</span>
              <span>{updateInfo.downloadProgressPercent}%</span>
            </div>
            <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-[#0284c7] rounded-full transition-all duration-300"
                style={{ width: `${Math.max(0, Math.min(100, updateInfo.downloadProgressPercent))}%` }}
              />
            </div>
          </div>
        )}

        {/* Error notification if any */}
        {updateError && updateInfo.status === 'Failed' && (
          <div className="p-3 bg-rose-50 border border-rose-200 rounded-xl flex items-center gap-2 text-rose-700 text-xs">
            <span className="material-symbols-outlined text-[16px] shrink-0">error</span>
            <span>{updateError}</span>
          </div>
        )}
      </section>

      {/* First-Run Model Download & Acceleration Wizard */}
      <ModelDownloadWizardModal
        isOpen={isWizardOpen}
        onClose={() => {
          setIsWizardOpen(false);
          loadHardwareAndModels();
        }}
        onModelReady={() => {
          loadHardwareAndModels();
        }}
      />
    </div>
  );
};
