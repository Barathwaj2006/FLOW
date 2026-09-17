import React from 'react';
import { SessionState } from '../types';

interface NavbarProps {
  sessionState: SessionState;
  showFloatingHud: boolean;
  setShowFloatingHud: (show: boolean) => void;
  onMinimizeToTray: () => void;
  zeroEnterActive: boolean;
  onOpenShortcutSettings?: () => void;
  credits?: number;
  userEmail?: string | null;
  onOpenCredits?: () => void;
  isEngineConnected?: boolean;
  onToggleMaximize?: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  sessionState,
  showFloatingHud,
  setShowFloatingHud,
  onMinimizeToTray,
  zeroEnterActive,
  onOpenShortcutSettings,
  credits = 50,
  userEmail = null,
  onOpenCredits,
  isEngineConnected = false,
  onToggleMaximize,
}) => {
  const handleToggleMaximize = () => {
    if (onToggleMaximize) {
      onToggleMaximize();
    } else if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch(() => {});
    } else {
      document.exitFullscreen().catch(() => {});
    }
  };
  return (
    <>
      {/* 1. Top Windows 11 Native Titlebar (Fixed 32px height) */}
      <div 
        id="windows-titlebar"
        className="fixed top-0 left-0 right-0 h-8 bg-white/95 backdrop-blur-md z-50 flex items-center justify-between pl-4 pr-0 border-b border-[#e2e8f0] select-none text-[#0f172a]"
      >
        {/* Left branding & status */}
        <div className="flex items-center gap-2 h-full flex-1">
          <div className="w-4 h-4 rounded-sm bg-[#0284c7] flex items-center justify-center shadow-xs">
            <span className="material-symbols-outlined text-white text-[12px]">graphic_eq</span>
          </div>
          <span className="font-semibold text-[11px] tracking-wider uppercase text-[#0f172a]">FLOW</span>
          <div className="h-3 w-[1px] bg-[#e2e8f0] mx-1"></div>
          
          <div 
            className={`flex items-center gap-1.5 px-2 py-0.5 rounded-full border transition-all ${
              sessionState === 'listening'
                ? 'bg-rose-50 border-rose-200 text-rose-700'
                : sessionState === 'processing'
                ? 'bg-sky-50 border-sky-200 text-sky-700'
                : isEngineConnected
                ? 'bg-emerald-50 border-emerald-200 text-emerald-700'
                : 'bg-amber-50 border-amber-200 text-amber-700'
            }`}
            title={
              isEngineConnected
                ? 'Connected to Flow.Host.Windows local Whisper engine (100% Offline)'
                : 'FLOW Host offline; running in Standalone Web Mode'
            }
          >
            <div className={`w-1.5 h-1.5 rounded-full ${
              sessionState === 'listening' ? 'bg-rose-500 animate-ping' :
              sessionState === 'processing' ? 'bg-sky-500 animate-spin' :
              isEngineConnected ? 'bg-emerald-500' : 'bg-amber-500'
            }`}></div>
            <span className="text-[10px] font-medium">
              {sessionState === 'listening' ? 'Recording Audio' :
               sessionState === 'processing' ? 'Whisper Inference' :
               isEngineConnected ? 'Local Whisper Connected' : 'Web Demo Mode'}
            </span>
          </div>

          {zeroEnterActive && (
            <div className="hidden sm:flex items-center gap-1 px-2 py-0.5 rounded-full bg-[#e0f2fe] border border-[#bae6fd] text-[#0369a1] text-[10px] font-medium">
              <span className="material-symbols-outlined text-[11px]">shield</span>
              <span>Zero-Enter Safety</span>
            </div>
          )}
        </div>

        {/* Right Windows Control Buttons (Minimize, Maximize, Close) */}
        <div className="flex items-center h-full">
          <button 
            id="btn-win-minimize"
            aria-label="Minimize" 
            onClick={onMinimizeToTray}
            className="w-11 h-8 flex items-center justify-center text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] transition-colors"
            title="Minimize to system tray"
          >
            <span className="material-symbols-outlined text-[14px]">remove</span>
          </button>
          <button 
            id="btn-win-maximize"
            aria-label="Maximize" 
            onClick={handleToggleMaximize}
            className="w-11 h-8 flex items-center justify-center text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] transition-colors"
            title="Toggle window maximize"
          >
            <span className="material-symbols-outlined text-[12px]">check_box_outline_blank</span>
          </button>
          <button 
            id="btn-win-close"
            aria-label="Close" 
            onClick={onMinimizeToTray}
            className="w-11 h-8 flex items-center justify-center text-[#64748b] hover:bg-[#fee2e2] hover:text-[#ef4444] transition-colors"
            title="Close application"
          >
            <span className="material-symbols-outlined text-[14px]">close</span>
          </button>
        </div>
      </div>

      {/* 2. Sub-Header Toolbar (Fixed top-8 left-60 right-0 h-14) */}
      <header 
        id="app-sub-header"
        className="fixed top-8 left-60 right-0 h-14 bg-white/80 backdrop-blur-xl z-40 flex items-center justify-between px-5 border-b border-[#e2e8f0] shadow-xs select-none"
      >
        <div className="flex items-center gap-3">
          <button
            id="btn-toggle-floating-hud"
            onClick={() => setShowFloatingHud(!showFloatingHud)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium border transition-all ${
              showFloatingHud
                ? 'bg-[#e0f2fe] border-[#bae6fd] text-[#0284c7]'
                : 'bg-white border-[#e2e8f0] text-[#64748b] hover:bg-[#f8fafc] hover:text-[#0f172a]'
            }`}
            title="Toggle persistent floating acrylic HUD"
          >
            <span className="material-symbols-outlined text-[15px]">picture_in_picture_alt</span>
            <span>Floating HUD {showFloatingHud ? 'Active' : 'Hidden'}</span>
          </button>
        </div>

        <div className="flex items-center gap-3">
          {/* Global Hotkey Pill */}
          <button 
            onClick={onOpenShortcutSettings}
            className="flex items-center gap-2 px-2.5 py-1 rounded bg-[#f8fafc] border border-[#e2e8f0] text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] transition-colors"
            title="Global Shortcuts: Hold Alt + Space or Press Alt + B"
          >
            <kbd className="font-mono text-xs text-[#0284c7] font-semibold">Alt + Space</kbd>
            <span className="text-slate-300">•</span>
            <kbd className="font-mono text-xs text-slate-700 font-semibold">Alt + B</kbd>
          </button>

          {/* Credits Pill */}
          <button
            id="btn-navbar-credits"
            onClick={onOpenCredits}
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-50 border border-amber-200 text-amber-800 hover:bg-amber-100 hover:border-amber-300 transition-all shadow-xs cursor-pointer active:scale-95"
            title="Cloud AI Credits & Account"
          >
            <span className="material-symbols-outlined text-amber-600 text-[16px]">stars</span>
            <span className="text-xs font-bold font-mono">{credits} Credits</span>
          </button>

          {/* User Profile Avatar */}
          <div 
            onClick={onOpenCredits}
            className="w-8 h-8 rounded-full bg-[#e0f2fe] border border-[#bae6fd] flex items-center justify-center text-[#0284c7] shadow-xs cursor-pointer hover:bg-[#bae6fd] transition-colors"
            title={userEmail ? `${userEmail} (Signed In)` : "Sign In with Email"}
          >
            <span className="material-symbols-outlined text-[18px]">person</span>
          </div>
        </div>
      </header>
    </>
  );
};
