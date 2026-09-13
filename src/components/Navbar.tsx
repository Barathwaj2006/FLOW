import React from 'react';
import { PanelLeft, User, Bell, Minus, Square, X } from 'lucide-react';

interface NavbarProps {
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
  onMinimizeToTray: () => void;
  onOpenNotifications?: () => void;
  onOpenProfile?: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  sidebarOpen,
  onToggleSidebar,
  onMinimizeToTray,
  onOpenNotifications,
  onOpenProfile,
}) => {
  return (
    <header 
      id="flow-window-chrome"
      className="w-full h-11 bg-[#f4f6f8] flex items-center justify-between px-3 select-none shrink-0 border-b border-[#e2e8f0]"
    >
      {/* Left controls: Sidebar toggle & User Avatar */}
      <div className="flex items-center gap-2">
        <button
          id="btn-toggle-sidebar"
          onClick={onToggleSidebar}
          aria-label={sidebarOpen ? 'Collapse sidebar' : 'Expand sidebar'}
          className="w-8 h-8 rounded-lg flex items-center justify-center text-[#64748b] hover:bg-[#e2e8f0] hover:text-[#0f172a] transition-colors"
          title="Toggle sidebar"
        >
          <PanelLeft className="w-[18px] h-[18px] stroke-[2]" />
        </button>

        <button
          id="btn-user-avatar"
          onClick={onOpenProfile}
          className="w-7 h-7 rounded-full bg-[#e2e8f0] border border-[#cbd5e1] flex items-center justify-center text-[#475569] hover:ring-2 hover:ring-[#4f46e5]/20 transition-all overflow-hidden"
          title="User profile: Barathwaj"
        >
          <User className="w-[15px] h-[15px] stroke-[2]" />
        </button>
      </div>

      {/* Right controls: Bell notification & Windows controls */}
      <div className="flex items-center gap-1">
        <button
          id="btn-notifications"
          onClick={onOpenNotifications}
          aria-label="Notifications"
          className="w-8 h-8 rounded-lg flex items-center justify-center text-[#64748b] hover:bg-[#e2e8f0] hover:text-[#0f172a] transition-colors relative"
          title="Notifications"
        >
          <Bell className="w-[17px] h-[17px] stroke-[2]" />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-[#4f46e5]" />
        </button>

        {/* Standard Windows Controls */}
        <button
          id="btn-win-minimize"
          onClick={onMinimizeToTray}
          aria-label="Minimize"
          className="w-9 h-8 flex items-center justify-center text-[#64748b] hover:bg-[#e2e8f0] hover:text-[#0f172a] transition-colors"
          title="Minimize"
        >
          <Minus className="w-[15px] h-[15px] stroke-[2]" />
        </button>

        <button
          id="btn-win-maximize"
          aria-label="Maximize"
          className="w-9 h-8 flex items-center justify-center text-[#64748b] hover:bg-[#e2e8f0] hover:text-[#0f172a] transition-colors"
          title="Maximize"
        >
          <Square className="w-[13px] h-[13px] stroke-[2]" />
        </button>

        <button
          id="btn-win-close"
          onClick={onMinimizeToTray}
          aria-label="Close"
          className="w-9 h-8 flex items-center justify-center text-[#64748b] hover:bg-rose-100 hover:text-rose-600 transition-colors"
          title="Close"
        >
          <X className="w-[15px] h-[15px] stroke-[2]" />
        </button>
      </div>
    </header>
  );
};
