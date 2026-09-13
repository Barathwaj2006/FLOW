import React from 'react';
import { SessionState } from '../types';

interface NavbarProps {
  sessionState: SessionState;
  showFloatingHud: boolean;
  setShowFloatingHud: (show: boolean) => void;
  onMinimizeToTray: () => void;
  zeroEnterActive: boolean;
  onOpenShortcutSettings?: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  sessionState,
  showFloatingHud,
  setShowFloatingHud,
  onMinimizeToTray,
  zeroEnterActive,
  onOpenShortcutSettings,
}) => {
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
          
          <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-[#f1f5f9] border border-[#e2e8f0]">
            <div className={`w-1.5 h-1.5 rounded-full ${
              sessionState === 'listening' ? 'bg-[#ef4444] animate-ping' :
              sessionState === 'processing' ? 'bg-[#0284c7] animate-spin' :
              'bg-[#0284c7] animate-pulse'
            }`}></div>
            <span className="text-[10px] font-medium text-[#64748b]">
              {sessionState === 'listening' ? 'Recording Audio' :
               sessionState === 'processing' ? 'Whisper Inference' :
               'Local Engine'}
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
            className="flex items-center gap-1 px-2.5 py-1 rounded bg-[#f8fafc] border border-[#e2e8f0] text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] transition-colors"
            title="Global Hotkey: Hold Right Alt or Ctrl+Space"
          >
            <span className="font-mono text-xs text-[#64748b] font-medium">Ctrl + Space</span>
          </button>

          {/* User Profile Avatar */}
          <div 
            className="w-8 h-8 rounded-full bg-[#e0f2fe] border border-[#bae6fd] flex items-center justify-center text-[#0284c7] shadow-xs cursor-pointer hover:bg-[#bae6fd] transition-colors"
            title="Barathwaj (Local Profile)"
          >
            <span className="material-symbols-outlined text-[18px]">person</span>
          </div>
        </div>
      </header>
    </>
  );
};
