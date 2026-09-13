import React from 'react';
import { TabType } from '../types';
import { 
  Mic, 
  Disc, 
  BarChart2, 
  BookOpen, 
  Scissors, 
  Type, 
  Wand2, 
  StickyNote, 
  Users, 
  Gift, 
  Settings as SettingsIcon, 
  HelpCircle 
} from 'lucide-react';

interface SidebarProps {
  activeTab: TabType;
  setActiveTab: (tab: TabType) => void;
  onOpenInvite?: () => void;
  onOpenFreeMonth?: () => void;
  onOpenHelp?: () => void;
  onOpenInviteModal?: () => void;
  onOpenFreeMonthModal?: () => void;
  historyCount?: number;
  dictCount?: number;
  snippetCount?: number;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  setActiveTab,
  onOpenInvite,
  onOpenFreeMonth,
  onOpenHelp,
  onOpenInviteModal,
  onOpenFreeMonthModal,
}) => {
  const handleInvite = onOpenInviteModal || onOpenInvite;
  const handleFreeMonth = onOpenFreeMonthModal || onOpenFreeMonth;
  // Normalize tab for matching:
  // 'home' maps to 'dictation', 'styles' maps to 'style'
  const currentTab = activeTab === 'home' ? 'dictation' : activeTab === 'styles' ? 'style' : activeTab;

  const navItems = [
    { id: 'dictation' as TabType, label: 'Dictation', icon: Mic },
    { id: 'notetaker' as TabType, label: 'Notetaker', icon: Disc, badge: 'New!' },
    { id: 'insights' as TabType, label: 'Insights', icon: BarChart2 },
    { id: 'dictionary' as TabType, label: 'Dictionary', icon: BookOpen },
    { id: 'snippets' as TabType, label: 'Snippets', icon: Scissors },
    { id: 'style' as TabType, label: 'Style', icon: Type },
    { id: 'transforms' as TabType, label: 'Transforms', icon: Wand2 },
    { id: 'scratchpad' as TabType, label: 'Scratchpad', icon: StickyNote },
  ];

  return (
    <aside 
      id="flow-sidebar" 
      className="w-56 h-full flex flex-col justify-between py-5 px-3 select-none shrink-0 bg-[#f4f6f8] border-r border-[#e2e8f0]"
    >
      {/* Top Brand & Main Navigation */}
      <div className="flex flex-col">
        {/* Modern Original Sonic Wave Glyph */}
        <div className="flex items-center gap-2.5 px-3 py-2 mb-4">
          <div className="w-7 h-7 rounded-xl bg-gradient-to-tr from-[#4f46e5] to-[#06b6d4] flex items-center justify-center shadow-xs">
            <div className="flex items-center gap-[2px] h-3.5">
              <span className="w-[2px] h-2 bg-white rounded-full"></span>
              <span className="w-[2px] h-3.5 bg-white rounded-full"></span>
              <span className="w-[2px] h-2.5 bg-white rounded-full"></span>
              <span className="w-[2px] h-3 bg-white rounded-full"></span>
            </div>
          </div>
          <div className="flex flex-col leading-none">
            <span className="text-[18px] font-bold tracking-tight text-[#0f172a] font-sans">
              Flow
            </span>
            <span className="text-[9.5px] font-bold tracking-wider uppercase text-[#6366f1]">
              Voice Studio
            </span>
          </div>
        </div>

        {/* Primary Navigation Rail */}
        <nav className="flex flex-col space-y-1">
          {navItems.map(item => {
            const Icon = item.icon;
            const isActive = currentTab === item.id;
            return (
              <button
                key={item.id}
                id={`nav-${item.id}`}
                onClick={() => setActiveTab(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-xl text-[14px] transition-all duration-150 ${
                  isActive
                    ? 'bg-[#e2e8f0] text-[#0f172a] font-semibold shadow-2xs'
                    : 'text-[#475569] hover:bg-[#e2e8f0]/60 hover:text-[#0f172a] font-normal'
                }`}
              >
                <div className="flex items-center gap-3">
                  <Icon 
                    className={`w-[18px] h-[18px] stroke-[2.2] ${
                      isActive ? 'text-[#4f46e5]' : 'text-[#64748b]'
                    }`} 
                  />
                  <span>{item.label}</span>
                </div>

                {item.badge && (
                  <span className="px-2 py-0.5 text-[10px] font-bold rounded-full bg-[#6366f1] text-white shadow-2xs leading-none">
                    {item.badge}
                  </span>
                )}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Bottom Footer Navigation */}
      <div className="flex flex-col space-y-1 pt-4 border-t border-[#e2e8f0]">
        <button
          id="nav-invite-team"
          onClick={handleInvite}
          className="w-full flex items-center gap-3 px-3 py-2 rounded-xl text-[13.5px] text-[#475569] hover:bg-[#e2e8f0]/60 hover:text-[#0f172a] transition-colors"
        >
          <Users className="w-[17px] h-[17px] text-[#64748b] stroke-[2]" />
          <span>Invite your team</span>
        </button>

        <button
          id="nav-free-month"
          onClick={handleFreeMonth}
          className="w-full flex items-center gap-3 px-3 py-2 rounded-xl text-[13.5px] text-[#475569] hover:bg-[#e2e8f0]/60 hover:text-[#0f172a] transition-colors"
        >
          <Gift className="w-[17px] h-[17px] text-[#64748b] stroke-[2]" />
          <span>Get a free month</span>
        </button>

        <button
          id="nav-settings"
          onClick={() => setActiveTab('settings')}
          className={`w-full flex items-center justify-between px-3 py-2 rounded-xl text-[13.5px] transition-colors ${
            currentTab === 'settings'
              ? 'bg-[#e2e8f0] text-[#0f172a] font-semibold'
              : 'text-[#475569] hover:bg-[#e2e8f0]/60 hover:text-[#0f172a]'
          }`}
        >
          <div className="flex items-center gap-3">
            <SettingsIcon className="w-[17px] h-[17px] text-[#64748b] stroke-[2]" />
            <span>Settings</span>
          </div>
          <span className="w-4.5 h-4.5 rounded-full bg-[#4f46e5] text-white text-[10px] font-bold flex items-center justify-center">
            1
          </span>
        </button>

        <button
          id="nav-help"
          onClick={() => {
            if (onOpenHelp) onOpenHelp();
            else setActiveTab('about');
          }}
          className={`w-full flex items-center gap-3 px-3 py-2 rounded-xl text-[13.5px] transition-colors ${
            currentTab === 'about'
              ? 'bg-[#e2e8f0] text-[#0f172a] font-semibold'
              : 'text-[#475569] hover:bg-[#e2e8f0]/60 hover:text-[#0f172a]'
          }`}
        >
          <HelpCircle className="w-[17px] h-[17px] text-[#64748b] stroke-[2]" />
          <span>Help</span>
        </button>
      </div>
    </aside>
  );
};
