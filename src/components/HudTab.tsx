import React, { useState } from 'react';

interface HudTabProps {
  onStartDictation?: () => void;
  onStopDictation?: () => void;
}

type PreviewStateType = 'ready' | 'recording' | 'processing' | 'done';

export const HudTab: React.FC<HudTabProps> = ({
  onStartDictation,
  onStopDictation,
}) => {
  const [previewState, setPreviewState] = useState<PreviewStateType>('ready');
  const [dockPosition, setDockPosition] = useState<'top-right' | 'bottom-center' | 'follow-cursor'>('bottom-center');
  const [layerOpacity, setLayerOpacity] = useState<number>(100);
  const [isClickThrough, setIsClickThrough] = useState<boolean>(true);
  const [triggerKey, setTriggerKey] = useState<'right-alt' | 'caps-lock' | 'fn'>('right-alt');

  const statesConfig = {
    ready: {
      icon: 'graphic_eq',
      streamText: '"System idle. Awaiting push-to-talk invocation..."',
    },
    recording: {
      icon: 'mic',
      streamText: '"Capturing speech frames directly from high-definition array microphone..."',
    },
    processing: {
      icon: 'sync',
      streamText: '"Running quantized transformer decoding on local CPU tensor engine..."',
    },
    done: {
      icon: 'check_circle',
      streamText: '"Integrated whisper.cpp into thread pool with zero allocations."',
    },
  };

  return (
    <div id="hud-showcase-view" className="flex flex-col w-full pb-16 space-y-8 pt-1 select-none">
      {/* 1. Hero & Status Header */}
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2">
            <span className="text-[11px] text-[#0284c7] uppercase tracking-wider font-semibold">
              Component Surface • Windows 11 Fluent
            </span>
            <span className="text-slate-300">•</span>
            <span className="text-[11px] text-slate-500 font-medium">HUD Shell Overlay (Layer 2)</span>
          </div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Flow Bar HUD &amp; Dictation Engine</h1>
          <p className="text-xs text-slate-600 max-w-2xl leading-relaxed">
            Ultra-low latency, persistent floating acrylic pill designed for Windows 11. Adapts responsively across speech recording, real-time local Whisper inference, secure credential blocking, and hardware recovery states.
          </p>
        </div>

        {/* Live HUD Diagnostic Quick Stats */}
        <div className="flex items-center gap-3 bg-white px-4 py-2.5 rounded-xl border border-slate-200 shadow-xs">
          <div className="flex flex-col">
            <span className="text-[10px] text-slate-400 uppercase font-semibold">Latency (Local)</span>
            <span className="font-mono text-xs text-[#0284c7] font-semibold">14.2 ms</span>
          </div>
          <div className="w-[1px] h-6 bg-slate-200"></div>
          <div className="flex flex-col">
            <span className="text-[10px] text-slate-400 uppercase font-semibold">Hotkey</span>
            <kbd className="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-200 text-[11px] font-mono text-slate-800 font-semibold shadow-2xs">
              Right Alt
            </kbd>
          </div>
          <div className="w-[1px] h-6 bg-slate-200"></div>
          <div className="flex flex-col">
            <span className="text-[10px] text-slate-400 uppercase font-semibold">VRAM</span>
            <span className="font-mono text-xs text-slate-700 font-medium">342 MB</span>
          </div>
        </div>
      </div>

      {/* 2. Primary Interactive Canvas: Simulated Windows 11 Workspace with Active Floating HUD */}
      <div className="relative w-full rounded-2xl bg-slate-100/70 border border-slate-200/80 overflow-hidden shadow-xs p-6">
        {/* Dot grid background */}
        <div className="absolute inset-0 opacity-20 bg-[radial-gradient(#0284c7_1px,transparent_1px)] [background-size:20px_20px]"></div>

        {/* Workspace Header */}
        <div className="relative flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 pb-3 mb-5 border-b border-slate-200/60">
          <div className="flex items-center gap-2">
            <div className="w-2.5 h-2.5 rounded-full bg-[#0284c7]"></div>
            <span className="text-[11px] text-slate-800 uppercase tracking-wider font-semibold">
              Simulated Workspace: VS Code Light &amp; Notion
            </span>
          </div>

          {/* HUD Interactive Switcher Buttons */}
          <div className="flex items-center gap-1 bg-white p-1 rounded-lg border border-slate-200 shadow-xs">
            <span className="text-[11px] text-slate-400 px-2 font-medium">Simulate State:</span>
            {(['ready', 'recording', 'processing', 'done'] as PreviewStateType[]).map(key => (
              <button
                key={key}
                id={`btn-state-${key}`}
                onClick={() => setPreviewState(key)}
                className={`px-3 py-1 text-xs rounded font-medium capitalize transition-all ${
                  previewState === key
                    ? 'bg-[#0284c7] text-white shadow-xs'
                    : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
                }`}
              >
                {key}
              </button>
            ))}
          </div>
        </div>

        {/* Mock Desktop Active Window Background */}
        <div className="relative rounded-xl bg-white border border-slate-200/80 p-5 shadow-xs max-w-4xl mx-auto flex flex-col gap-3">
          <div className="flex items-center justify-between text-slate-500 pb-2 border-b border-slate-100 text-xs">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-[#0284c7]">description</span>
              <span className="font-mono text-slate-800 font-semibold">release-notes-v1.4.md</span>
              <span className="text-[11px] px-2 py-0.5 rounded bg-slate-100 text-slate-600 font-medium">
                Markdown
              </span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-[#0284c7]"></span>
              <span className="font-mono text-slate-400">Ln 42, Col 18</span>
            </div>
          </div>

          {/* Editor Content lines */}
          <div className="font-mono text-xs text-slate-600 flex flex-col gap-1.5 select-text">
            <p><span className="text-[#0284c7] font-bold">##</span> Architecture Sprint Highlights</p>
            <p className="text-slate-500">Local speech synthesis pipeline hooked directly into Win32 low-level audio hook.</p>
            <div className="flex items-center gap-2 text-slate-900 bg-slate-50 p-2.5 rounded-lg border border-[#0284c7]/20">
              <span className="text-[#0284c7] font-bold">&gt;</span>
              <span className="text-[#0284c7] font-semibold" id="transcription-stream-text">
                {statesConfig[previewState].streamText}
              </span>
              <span className="w-2 h-4 bg-[#0284c7] inline-block animate-pulse"></span>
            </div>
          </div>
        </div>

        {/* Centerpiece: Interactive Floating Flow Bar HUD Pill */}
        <div className="relative flex flex-col items-center justify-center pt-8 pb-4">
          <div className="text-[11px] text-slate-500 mb-3 flex items-center gap-1.5 bg-white/90 px-3 py-1 rounded-full border border-slate-200 shadow-xs">
            <span className="material-symbols-outlined text-[14px] text-[#0284c7]">pan_tool</span>
            <span>Windows Acrylic Light Pill • Drag Anchor Enabled • Always on Top</span>
          </div>

          {/* THE HUD PILL CONTAINER: Windows 11 Acrylic Light Mode */}
          <div 
            id="flowbar-pill"
            className="relative group flex items-center gap-3 px-4 py-2.5 rounded-full bg-white/95 backdrop-blur-2xl border border-slate-200 shadow-[0_12px_32px_rgba(19,27,46,0.12),0_2px_8px_rgba(0,97,148,0.08)] transition-all duration-300"
          >
            {/* Leading Gripper & Logo */}
            <div className="flex items-center gap-1.5 text-slate-400 cursor-grab active:cursor-grabbing">
              <span className="material-symbols-outlined text-[15px] opacity-40 hover:opacity-100">drag_indicator</span>
              <div className="w-7 h-7 rounded-full bg-sky-50 flex items-center justify-center transition-colors">
                <span className="material-symbols-outlined text-[#0284c7] text-[16px]">
                  {statesConfig[previewState].icon}
                </span>
              </div>
            </div>

            {/* Dynamic Content Section */}
            <div className="flex items-center gap-3 min-w-[280px] h-7" id="hud-state-container">
              {previewState === 'ready' && (
                <div className="flex items-center gap-3">
                  <div className="flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full bg-[#0284c7]"></span>
                    <span className="text-xs text-slate-900 font-semibold tracking-wide">Ready</span>
                  </div>
                  <span className="text-slate-300">|</span>
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs text-slate-600">Hold</span>
                    <kbd className="px-1.5 py-0.5 rounded bg-slate-100 border border-slate-200 text-[11px] font-mono text-slate-900 font-semibold shadow-2xs">
                      Right Alt
                    </kbd>
                    <span className="text-xs text-slate-600">to speak</span>
                  </div>
                </div>
              )}

              {previewState === 'recording' && (
                <div className="flex items-center gap-3 w-full justify-between">
                  <div className="flex items-center gap-1.5">
                    <span className="w-2.5 h-2.5 rounded-full bg-rose-500 animate-ping"></span>
                    <span className="font-mono text-xs font-bold text-slate-900">0:03.4s</span>
                  </div>

                  {/* 12-bar active waveform */}
                  <div className="flex items-center gap-[2px] h-6 px-2">
                    <div className="w-[2px] h-2 bg-[#0284c7] rounded-full animate-pulse"></div>
                    <div className="w-[2px] h-4 bg-sky-500 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }}></div>
                    <div className="w-[2px] h-6 bg-sky-600 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }}></div>
                    <div className="w-[2px] h-3 bg-[#0284c7] rounded-full animate-pulse"></div>
                    <div className="w-[2px] h-5 bg-sky-400 rounded-full animate-bounce" style={{ animationDelay: '0.15s' }}></div>
                    <div className="w-[2px] h-6 bg-sky-500 rounded-full animate-bounce" style={{ animationDelay: '0.05s' }}></div>
                    <div className="w-[2px] h-2 bg-[#0284c7] rounded-full animate-pulse"></div>
                    <div className="w-[2px] h-4 bg-sky-600 rounded-full animate-bounce" style={{ animationDelay: '0.25s' }}></div>
                    <div className="w-[2px] h-5 bg-sky-400 rounded-full animate-bounce" style={{ animationDelay: '0.18s' }}></div>
                    <div className="w-[2px] h-3 bg-[#0284c7] rounded-full animate-pulse"></div>
                    <div className="w-[2px] h-6 bg-[#0284c7] rounded-full animate-bounce" style={{ animationDelay: '0.3s' }}></div>
                    <div className="w-[2px] h-2 bg-[#0284c7] rounded-full animate-pulse"></div>
                  </div>

                  <div className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[15px] text-[#0284c7]">volume_up</span>
                    <span className="font-mono text-[11px] text-[#0284c7] font-semibold">-14.6 dB</span>
                  </div>
                </div>
              )}

              {previewState === 'processing' && (
                <div className="flex items-center gap-3 w-full justify-between">
                  <div className="flex items-center gap-2">
                    <svg className="animate-spin h-3.5 w-3.5 text-[#0284c7]" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                    </svg>
                    <span className="text-xs text-slate-900 font-semibold">Local Whisper Inference…</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <span className="font-mono text-[10px] text-slate-500">INT8 AVX-512</span>
                    <span className="text-slate-300">•</span>
                    <span className="font-mono text-[10px] text-[#0284c7] font-semibold">18ms</span>
                  </div>
                </div>
              )}

              {previewState === 'done' && (
                <div className="flex items-center gap-3 w-full justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-4 h-4 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600">
                      <span className="material-symbols-outlined text-[12px]">check</span>
                    </div>
                    <span className="text-xs text-slate-900 font-semibold">Inserted into Editor</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <span className="font-mono text-[10px] text-[#0284c7] font-semibold">18 tokens</span>
                    <span className="text-slate-300">•</span>
                    <span className="font-mono text-[10px] text-slate-500">SendInput API</span>
                  </div>
                </div>
              )}
            </div>

            {/* Trailing Chip & Dismiss */}
            <div className="flex items-center gap-1 pl-2 border-l border-slate-200">
              <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-medium">
                Whisper-v3
              </span>
              <button 
                className="w-6 h-6 rounded-full flex items-center justify-center text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
                title="Settings Flyout"
              >
                <span className="material-symbols-outlined text-[16px]">more_vert</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* 3. Visual State Matrix (All 8 States Grid) */}
      <div className="flex flex-col gap-3">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2">
          <div>
            <h2 className="text-lg font-bold text-slate-900 tracking-tight">Full State Matrix &amp; Edge Cases</h2>
            <p className="text-xs text-slate-500">
              Deterministic system states conforming to Windows 11 accessibility, error handling, and privacy telemetry contracts.
            </p>
          </div>
          <span className="font-mono text-xs text-slate-600 bg-white border border-slate-200 px-2.5 py-1 rounded font-semibold shadow-2xs">
            8 Scenarios Loaded
          </span>
        </div>

        {/* 8 Grid Cards Mosaic */}
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
          {/* State 1: Idle */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-slate-500 uppercase tracking-wider font-semibold">State 01 • Idle</span>
              <span className="w-2 h-2 rounded-full bg-[#0284c7]"></span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5">
                <div className="w-5 h-5 rounded-full bg-sky-50 flex items-center justify-center text-[#0284c7]">
                  <span className="material-symbols-outlined text-[13px]">mic</span>
                </div>
                <span className="text-xs text-slate-900 font-semibold">Ready</span>
              </div>
              <kbd className="px-1.5 py-0.5 rounded bg-white border border-slate-200 text-[10px] font-mono text-slate-600 font-medium">
                Right Alt
              </kbd>
            </div>
            <p className="text-[11px] text-slate-500">Minimal footprint. Listens for global hotkey without allocating GPU compute threads.</p>
          </div>

          {/* State 2: Capturing */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-[#0284c7] uppercase tracking-wider font-semibold">State 02 • Capturing</span>
              <span className="w-2 h-2 rounded-full bg-rose-500 animate-ping"></span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded-full bg-rose-500 animate-pulse"></span>
                <span className="font-mono text-xs text-slate-900 font-bold">0:03s</span>
              </div>
              <div className="flex items-center gap-0.5 h-4 px-1">
                <div className="w-[2px] h-2 bg-[#0284c7] rounded-full animate-bounce"></div>
                <div className="w-[2px] h-4 bg-sky-500 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }}></div>
                <div className="w-[2px] h-3 bg-sky-600 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }}></div>
                <div className="w-[2px] h-4 bg-sky-400 rounded-full animate-bounce" style={{ animationDelay: '0.3s' }}></div>
                <div className="w-[2px] h-2 bg-[#0284c7] rounded-full animate-bounce" style={{ animationDelay: '0.15s' }}></div>
              </div>
              <span className="font-mono text-[10px] text-[#0284c7] font-semibold">-18 dB</span>
            </div>
            <p className="text-[11px] text-slate-500">Real-time dynamic amplitude sampling with hardware decibel readout and running timecode.</p>
          </div>

          {/* State 3: Inference */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-slate-500 uppercase tracking-wider font-semibold">State 03 • Inference</span>
              <span className="material-symbols-outlined text-[#0284c7] text-[14px] animate-spin">sync</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5">
                <svg className="animate-spin h-3.5 w-3.5 text-[#0284c7]" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                  <path className="opacity-75" d="M4 12a8 8 0 018-8v8H4z" fill="currentColor"></path>
                </svg>
                <span className="text-xs text-slate-900 font-semibold">Transcribing…</span>
              </div>
              <span className="font-mono text-[10px] text-[#0284c7] font-medium">GPU 88%</span>
            </div>
            <p className="text-[11px] text-slate-500">Subtle indeterminate Fluent spin indicator showing GPU tensor utilization during translation.</p>
          </div>

          {/* State 4: Success */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-emerald-600 uppercase tracking-wider font-semibold">State 04 • Success</span>
              <span className="material-symbols-outlined text-emerald-600 text-[16px]">done_all</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5">
                <div className="w-4 h-4 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600">
                  <span className="material-symbols-outlined text-[12px]">check</span>
                </div>
                <span className="text-xs text-slate-900 font-semibold">Pasted</span>
              </div>
              <span className="font-mono text-[10px] text-slate-500 font-medium">24 wpm</span>
            </div>
            <p className="text-[11px] text-slate-500">Haptic &amp; visual feedback confirming successful virtual keystroke insertion into active focus target.</p>
          </div>

          {/* State 5: Aborted */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-slate-500 uppercase tracking-wider font-semibold">State 05 • Aborted</span>
              <span className="material-symbols-outlined text-slate-400 text-[14px]">cancel</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5 text-slate-500">
                <span className="material-symbols-outlined text-[14px]">block</span>
                <span className="text-xs font-medium">Cancelled</span>
              </div>
              <kbd className="px-1.5 py-0.5 rounded bg-white border border-slate-200 text-[10px] font-mono text-slate-500">Esc</kbd>
            </div>
            <p className="text-[11px] text-slate-500">Immediate discard without clipboard pollution if hotkey was held for less than 400ms or cancelled.</p>
          </div>

          {/* State 6: Device Error */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-red-600 uppercase tracking-wider font-semibold">State 06 • Device Error</span>
              <span className="material-symbols-outlined text-red-600 text-[16px]">mic_off</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-red-50/80 border border-red-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5 text-red-600">
                <span className="material-symbols-outlined text-[14px]">mic_off</span>
                <span className="text-[11px] font-semibold">No Audio Input</span>
              </div>
              <div className="flex items-center gap-1">
                <button className="px-2 py-0.5 rounded bg-white border border-slate-200 hover:bg-slate-100 text-[10px] text-slate-800 font-medium">
                  Select
                </button>
                <button className="px-2 py-0.5 rounded bg-red-600 text-white text-[10px] font-medium shadow-xs">
                  Retry
                </button>
              </div>
            </div>
            <p className="text-[11px] text-slate-500">WASAPI device disconnect recovery bar offering direct inline device selection or re-polling.</p>
          </div>

          {/* State 7: Model Error */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-amber-600 uppercase tracking-wider font-semibold">State 07 • Model Error</span>
              <span className="material-symbols-outlined text-amber-600 text-[16px]">memory</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5 text-amber-700">
                <span className="material-symbols-outlined text-[14px]">download_for_offline</span>
                <span className="text-[11px] font-medium">Model missing</span>
              </div>
              <button className="px-2 py-0.5 rounded bg-white border border-slate-200 hover:bg-slate-100 text-[10px] text-slate-800 font-medium">
                Check GGUF
              </button>
            </div>
            <p className="text-[11px] text-slate-500">Guides user to on-device weights folder without crashing desktop runtime.</p>
          </div>

          {/* State 8: Privacy Shield */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-white border border-slate-200 shadow-xs hover:border-sky-300 transition-colors">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] text-[#0284c7] uppercase tracking-wider font-semibold">State 08 • Privacy Shield</span>
              <span className="material-symbols-outlined text-[#0284c7] text-[16px]">shield</span>
            </div>
            <div className="flex items-center justify-between px-3 py-1.5 rounded-full bg-slate-50 border border-slate-200 shadow-2xs mb-3">
              <div className="flex items-center gap-1.5 text-[#0284c7]">
                <span className="material-symbols-outlined text-[15px]">lock</span>
                <span className="text-xs text-slate-900 font-semibold">Password Protected</span>
              </div>
              <span className="text-[10px] text-slate-500 bg-white border border-slate-200 px-2 py-0.5 rounded font-medium">
                Bypassed
              </span>
            </div>
            <p className="text-[11px] text-slate-500">Windows UI Automation detects sensitive input fields; microphone halts strictly to protect credentials.</p>
          </div>
        </div>
      </div>

      {/* 4. Customization HUD Bar Flyout & Settings Sheet */}
      <div className="w-full rounded-2xl bg-white border border-slate-200 p-6 shadow-xs">
        <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-3 mb-5 pb-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-slate-50 border border-slate-200 flex items-center justify-center shadow-xs">
              <span className="material-symbols-outlined text-[#0284c7] text-[20px]">tune</span>
            </div>
            <div>
              <h3 className="text-sm font-bold text-slate-900">HUD Customization Flyout Overlay</h3>
              <p className="text-xs text-slate-500">Fine-grained positioning, compositor opacity, and global interception triggers.</p>
            </div>
          </div>
          <div className="flex items-center gap-1.5 bg-slate-50 px-3 py-1 rounded-lg border border-slate-200">
            <span className="text-[11px] text-slate-500">Settings sync:</span>
            <span className="font-mono text-xs text-[#0284c7] font-semibold">Local %APPDATA%/flow</span>
          </div>
        </div>

        {/* Control Rows in Bento Style */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {/* Position Selector */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-slate-50 border border-slate-200">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold text-slate-900">Screen Docking</span>
              <span className="material-symbols-outlined text-[18px] text-slate-400">dock_to_bottom</span>
            </div>
            <p className="text-[11px] text-slate-500 mb-3">
              Determine where the floating HUD anchors across multi-display arrangements.
            </p>
            <div className="grid grid-cols-3 gap-1 bg-slate-200/70 p-1 rounded-lg">
              {(['top-right', 'bottom-center', 'follow-cursor'] as const).map(pos => (
                <button
                  key={pos}
                  onClick={() => setDockPosition(pos)}
                  className={`py-1 px-1 rounded text-center text-[11px] transition-all ${
                    dockPosition === pos
                      ? 'bg-white text-[#0284c7] font-semibold shadow-xs'
                      : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  {pos === 'top-right' ? 'Top Right' : pos === 'bottom-center' ? 'Bottom Center' : 'Follow Cursor'}
                </button>
              ))}
            </div>
          </div>

          {/* Opacity & Click-Through */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-slate-50 border border-slate-200">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold text-slate-900">Opacity &amp; Click-Through</span>
              <span className="material-symbols-outlined text-[18px] text-slate-400">opacity</span>
            </div>
            <p className="text-[11px] text-slate-500 mb-3">
              Enable mouse events to pass directly through the acrylic layer when idle.
            </p>
            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between">
                <span className="text-[11px] text-slate-600">Layer Opacity:</span>
                <div className="flex items-center gap-1 bg-slate-200/70 p-0.5 rounded">
                  <button 
                    onClick={() => setLayerOpacity(100)}
                    className={`px-2.5 py-0.5 rounded font-mono text-[10px] ${
                      layerOpacity === 100 ? 'bg-white text-[#0284c7] font-bold shadow-xs' : 'text-slate-600'
                    }`}
                  >
                    100%
                  </button>
                  <button 
                    onClick={() => setLayerOpacity(80)}
                    className={`px-2.5 py-0.5 rounded font-mono text-[10px] ${
                      layerOpacity === 80 ? 'bg-white text-[#0284c7] font-bold shadow-xs' : 'text-slate-600'
                    }`}
                  >
                    80%
                  </button>
                </div>
              </div>

              <div className="flex items-center justify-between pt-1">
                <span className="text-[11px] text-slate-600 font-mono">WS_EX_TRANSPARENT</span>
                <label className="relative inline-flex items-center cursor-pointer">
                  <input 
                    type="checkbox" 
                    checked={isClickThrough} 
                    onChange={e => setIsClickThrough(e.target.checked)} 
                    className="sr-only peer" 
                  />
                  <div className="w-9 h-5 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:bg-[#0284c7] transition-colors"></div>
                  <div className="absolute left-1 top-1 bg-white w-3 h-3 rounded-full shadow-xs transition-transform peer-checked:translate-x-4"></div>
                </label>
              </div>
            </div>
          </div>

          {/* Activation Trigger Hotkey */}
          <div className="flex flex-col justify-between p-4 rounded-xl bg-slate-50 border border-slate-200">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold text-slate-900">Activation Trigger Hotkey</span>
              <span className="material-symbols-outlined text-[18px] text-slate-400">keyboard</span>
            </div>
            <p className="text-[11px] text-slate-500 mb-3">
              Select instant push-to-talk key mapping. Does not interfere with standard typing.
            </p>
            <div className="flex items-center gap-1 bg-slate-200/70 p-1 rounded-lg">
              {(['right-alt', 'caps-lock', 'fn'] as const).map(key => (
                <button
                  key={key}
                  onClick={() => setTriggerKey(key)}
                  className={`flex-1 py-1 px-1 rounded text-center text-xs font-mono transition-all ${
                    triggerKey === key
                      ? 'bg-white text-[#0284c7] font-bold shadow-xs'
                      : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  {key === 'right-alt' ? 'Right Alt' : key === 'caps-lock' ? 'Caps Lock' : 'Fn Key'}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
