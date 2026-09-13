import React from 'react';
import { TabType } from '../types';

interface SidebarProps {
  activeTab: TabType;
  setActiveTab: (tab: TabType) => void;
  historyCount: number;
  dictCount: number;
  snippetCount: number;
  onOpenHelp?: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  setActiveTab,
  historyCount,
  dictCount,
  snippetCount,
  onOpenHelp,
}) => {
  const mainNavItems: { id: TabType; label: string; icon: string; badge?: number }[] = [
    { id: 'home', label: 'Home', icon: 'home' },
    { id: 'history', label: 'History', icon: 'history', badge: historyCount },
    { id: 'dictionary', label: 'Dictionary', icon: 'menu_book', badge: dictCount },
    { id: 'snippets', label: 'Snippets', icon: 'data_object', badge: snippetCount },
    { id: 'styles', label: 'Styles', icon: 'tune' },
    { id: 'scratchpad', label: 'Scratchpad', icon: 'edit_note' },
    { id: 'hud', label: 'Flow Bar HUD', icon: 'graphic_eq' },
    { id: 'settings', label: 'Settings', icon: 'settings' },
  ];

  return (
    <aside 
      id="flow-sidebar" 
      className="fixed left-0 top-8 bottom-0 w-60 bg-white border-r border-[#e2e8f0] z-40 flex flex-col justify-between py-3 px-2 select-none"
    >
      {/* Top Brand Block */}
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2.5 px-3 py-1.5">
          <div className="w-7 h-7 rounded-lg bg-[#0284c7] flex items-center justify-center shadow-xs text-white">
            <span className="material-symbols-outlined text-[18px]">mic</span>
          </div>
          <div className="flex flex-col">
            <span className="text-base font-bold text-[#0f172a] leading-none tracking-tight">FLOW</span>
            <div className="flex items-center gap-1.5 mt-1">
              <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></div>
              <span className="text-[11px] font-medium text-[#64748b]">Ready</span>
            </div>
          </div>
        </div>

        {/* Primary Navigation Rail */}
        <nav className="flex flex-col gap-0.5 mt-2">
          {mainNavItems.map(item => {
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                id={`nav-${item.id}`}
                onClick={() => setActiveTab(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-md transition-colors text-[13px] ${
                  isActive
                    ? 'bg-[#e0f2fe] text-[#0284c7] font-semibold border-l-2 border-[#0284c7]'
                    : 'text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] font-normal'
                }`}
              >
                <div className="flex items-center gap-2.5">
                  <span className={`material-symbols-outlined text-[18px] ${
                    isActive ? 'text-[#0284c7]' : 'text-[#64748b]'
                  }`}>
                    {item.icon}
                  </span>
                  <span>{item.label}</span>
                </div>

                {typeof item.badge === 'number' && item.badge > 0 && (
                  <span className={`text-[10px] font-mono px-1.5 py-0.2 rounded-full font-medium ${
                    isActive ? 'bg-[#0284c7] text-white' : 'bg-[#f1f5f9] text-[#64748b]'
                  }`}>
                    {item.badge}
                  </span>
                )}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Bottom Footer Section */}
      <div className="flex flex-col gap-1 pt-2 border-t border-[#e2e8f0]">
        <nav className="flex flex-col gap-0.5">
          <button
            id="nav-help"
            onClick={() => {
              if (onOpenHelp) onOpenHelp();
              else setActiveTab('about');
            }}
            className="w-full flex items-center gap-2.5 px-3 py-1.5 rounded-md text-[13px] text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a] transition-colors"
          >
            <span className="material-symbols-outlined text-[18px] text-[#64748b]">help_outline</span>
            <span>Help</span>
          </button>
          
          <button
            id="nav-about"
            onClick={() => setActiveTab('about')}
            className={`w-full flex items-center gap-2.5 px-3 py-1.5 rounded-md text-[13px] transition-colors ${
              activeTab === 'about'
                ? 'bg-[#e0f2fe] text-[#0284c7] font-semibold'
                : 'text-[#64748b] hover:bg-[#f1f5f9] hover:text-[#0f172a]'
            }`}
          >
            <span className="material-symbols-outlined text-[18px]">info</span>
            <span>About</span>
          </button>
        </nav>

        <div className="px-3 pt-2">
          <p className="text-[11px] text-[#94a3b8] truncate font-medium">
            v1.2.0 • Offline/Local Engine
          </p>
        </div>
      </div>
    </aside>
  );
};
