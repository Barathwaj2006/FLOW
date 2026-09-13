import React, { useState } from 'react';
import { 
  Info, 
  Share2, 
  TrendingUp, 
  Bot, 
  MessageSquare, 
  Infinity as InfinityIcon, 
  FileText, 
  Mail, 
  Briefcase, 
  Laptop, 
  Smartphone,
  ChevronLeft,
  ChevronRight
} from 'lucide-react';

export const InsightsTab: React.FC = () => {
  const [activeSubTab, setActiveSubTab] = useState<'usage' | 'voice'>('usage');
  const [selectedMonth, setSelectedMonth] = useState('Sep');

  // Heatmap generation matching Screenshot 2
  const daysOfWeek = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const months = ['May', 'Jun', 'Jul', 'Aug', 'Sep'];

  // 18 weeks of sample activity data matching the teal heatmap in the screenshot
  const weeksData = [
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 1, 0, 0],
    [0, 0, 0, 2, 0, 0, 3],
    [0, 0, 2, 0, 0, 1, 0],
    [3, 0, 0, 3, 3, 0, 2],
    [3, 0, 3, 0, 2, 3, 0],
    [1, 1, 1, 2, 3, 1, 2],
    [1, 1, 1, 1, 1, 1, 1],
    [1, 1, 1, 1, 1, 1, 1],
    [1, 1, 1, 1, 1, 1, 1],
    [2, 0, 0, 0, 0, 0, 0],
  ];

  const getHeatmapColor = (level: number) => {
    switch (level) {
      case 3:
        return 'bg-[#134e4a]'; // Dark teal
      case 2:
        return 'bg-[#0d9488]'; // Mid teal
      case 1:
        return 'bg-[#99f6e4]'; // Soft teal
      default:
        return 'bg-[#edeae5]'; // Inactive beige square
    }
  };

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Top Title & Circular Share Badge */}
      <div className="flex items-center justify-between">
        <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
          Insights
        </h1>

        {/* Circular "SHARE" badge matching screenshot */}
        <div className="relative w-12 h-12 flex items-center justify-center cursor-pointer hover:scale-105 active:scale-95 transition-transform" title="Share your Flow stats">
          <svg className="absolute inset-0 w-full h-full animate-[spinSlow_18s_linear_infinite]" viewBox="0 0 100 100">
            <path
              id="circlePath"
              d="M 50, 50 m -37, 0 a 37,37 0 1,1 74,0 a 37,37 0 1,1 -74,0"
              fill="none"
            />
            <text className="text-[10px] font-bold uppercase tracking-[2.8px] fill-[#44403c]">
              <textPath href="#circlePath">
                • SHARE • SHARE • SHARE
              </textPath>
            </text>
          </svg>
          <div className="w-6 h-6 rounded-full flex items-center justify-center text-[#1c1917]">
            <Share2 className="w-3.5 h-3.5 stroke-[2.4]" />
          </div>
        </div>
      </div>

      {/* Sub Tabs: Your usage | Your voice */}
      <div className="flex items-center gap-6 border-b border-[#ede8e1]">
        <button
          onClick={() => setActiveSubTab('usage')}
          className={`pb-3 text-[15px] font-semibold transition-colors relative ${
            activeSubTab === 'usage'
              ? 'text-[#1c1917]'
              : 'text-[#78716c] hover:text-[#1c1917]'
          }`}
        >
          Your usage
          {activeSubTab === 'usage' && (
            <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
          )}
        </button>

        <button
          onClick={() => setActiveSubTab('voice')}
          className={`pb-3 text-[15px] font-semibold transition-colors relative ${
            activeSubTab === 'voice'
              ? 'text-[#1c1917]'
              : 'text-[#78716c] hover:text-[#1c1917]'
          }`}
        >
          Your voice
          {activeSubTab === 'voice' && (
            <span className="absolute bottom-0 left-0 right-0 h-[2.5px] bg-[#1c1917] rounded-full" />
          )}
        </button>
      </div>

      {activeSubTab === 'usage' ? (
        <div className="space-y-5">
          {/* Top Row: 3 Metric Cards */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            {/* Card 1: 134 WORDS PER MINUTE + Donut Arc Gauge */}
            <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 flex flex-col justify-between min-h-[220px]">
              <div>
                <div className="text-[38px] font-serif-editorial font-bold text-[#1c1917] leading-none">
                  134
                </div>
                <div className="flex items-center gap-1.5 mt-2 text-[11px] font-bold tracking-wider uppercase text-[#78716c]">
                  <span>Words per minute</span>
                  <Info className="w-3.5 h-3.5 text-[#a8a29e]" />
                </div>
              </div>

              {/* Arc Gauge in deep teal matching Screenshot 2 */}
              <div className="relative flex flex-col items-center justify-center pt-2">
                <svg className="w-36 h-20" viewBox="0 0 160 90">
                  {/* Gauge Background Track */}
                  <path
                    d="M 20 80 A 60 60 0 0 1 140 80"
                    fill="none"
                    stroke="#e7e3dc"
                    strokeWidth="14"
                    strokeLinecap="round"
                  />
                  {/* Gauge Active Arc (Teal) */}
                  <path
                    d="M 20 80 A 60 60 0 0 1 135 70"
                    fill="none"
                    stroke="#134e4a"
                    strokeWidth="14"
                    strokeLinecap="round"
                  />
                </svg>
                <div className="absolute bottom-1 text-center">
                  <span className="text-[11px] font-medium text-[#78716c] block">Top</span>
                  <span className="text-[18px] font-bold text-[#1c1917] leading-none">0.3%</span>
                </div>
              </div>
            </div>

            {/* Card 2: 2,019 FIXES MADE BY FLOW */}
            <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 flex flex-col justify-between min-h-[220px]">
              <div>
                <div className="text-[38px] font-serif-editorial font-bold text-[#1c1917] leading-none">
                  2,019
                </div>
                <div className="text-[11px] font-bold tracking-wider uppercase text-[#78716c] mt-2">
                  Fixes made by Flow
                </div>
              </div>

              <div className="space-y-3 pt-4 border-t border-[#ede8e1]">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-[17px] font-bold text-[#1c1917]">1,188</span>
                    <span className="text-[13px] text-[#78716c]">words corrected</span>
                  </div>
                  <Info className="w-3.5 h-3.5 text-[#a8a29e]" />
                </div>

                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-[17px] font-bold text-[#1c1917]">831</span>
                    <span className="text-[13px] text-[#78716c]">dictionary fixes</span>
                  </div>
                  <Info className="w-3.5 h-3.5 text-[#a8a29e]" />
                </div>
              </div>
            </div>

            {/* Card 3: 18,134 TOTAL WORDS DICTATED + Desktop/Mobile Split */}
            <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 flex flex-col justify-between min-h-[220px]">
              <div>
                <div className="flex items-start justify-between">
                  <div className="text-[38px] font-serif-editorial font-bold text-[#1c1917] leading-none">
                    18,134
                  </div>
                  <span className="px-2.5 py-1 rounded-full bg-[#e6f4f1] text-[#0d9488] text-[11px] font-bold flex items-center gap-1">
                    <TrendingUp className="w-3 h-3 stroke-[2.5]" />
                    98% this month
                  </span>
                </div>
                <div className="text-[11px] font-bold tracking-wider uppercase text-[#78716c] mt-2">
                  Total words dictated
                </div>
              </div>

              <div className="space-y-3">
                <p className="text-[13.5px] text-[#44403c] font-normal">
                  You've written 2 Dr. Seuss books!
                </p>

                {/* Progress bar split */}
                <div className="w-full flex h-8 rounded-lg overflow-hidden gap-1">
                  <div className="bg-[#134e4a] text-white flex items-center px-3 gap-1.5 text-xs font-semibold flex-1">
                    <Laptop className="w-3.5 h-3.5" />
                    <span>Desktop</span>
                  </div>
                  <div className="bg-[#5eead4] text-[#134e4a] flex items-center px-2.5 gap-1 text-xs font-semibold w-20 justify-center">
                    <Smartphone className="w-3 h-3" />
                    <span>Mobile</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Bottom Row: 2 Wide Cards (Desktop Usage & Streak Heatmap) */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
            {/* Bottom Left Card: Desktop usage (App breakdown) */}
            <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-[22px] font-bold text-[#1c1917]">
                  Desktop usage
                </h2>
                <span className="text-[11px] font-bold tracking-wider uppercase text-[#78716c]">
                  Total apps used | 23
                </span>
              </div>

              {/* Stacked Bars */}
              <div className="space-y-3 pt-2">
                {/* 1. AI Prompts (87%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <Bot className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#134e4a] text-white text-[11px] font-bold h-full flex items-center justify-center w-[87%]">
                        87%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#44403c] w-36 shrink-0 uppercase tracking-tight">
                      258 AI Prompts
                    </span>
                  </div>
                </div>

                {/* 2. Personal Messages (7%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <MessageSquare className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#0d9488] text-white text-[11px] font-bold h-full flex items-center justify-center w-[18%]">
                        7%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#44403c] w-36 shrink-0 uppercase tracking-tight">
                      27 Personal Messages
                    </span>
                  </div>
                </div>

                {/* 3. Other Tasks (6%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <InfinityIcon className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#0d9488] text-white text-[11px] font-bold h-full flex items-center justify-center w-[16%]">
                        6%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#44403c] w-36 shrink-0 uppercase tracking-tight">
                      19 Other Tasks
                    </span>
                  </div>
                </div>

                {/* 4. Documents (0%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <FileText className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#99f6e4] text-[#134e4a] text-[11px] font-bold h-full flex items-center justify-center w-[9%]">
                        0%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#78716c] w-36 shrink-0 uppercase tracking-tight">
                      0 Documents
                    </span>
                  </div>
                </div>

                {/* 5. Emails (0%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <Mail className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#99f6e4] text-[#134e4a] text-[11px] font-bold h-full flex items-center justify-center w-[9%]">
                        0%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#78716c] w-36 shrink-0 uppercase tracking-tight">
                      0 Emails
                    </span>
                  </div>
                </div>

                {/* 6. Work Messages (0%) */}
                <div className="flex items-center gap-3">
                  <div className="w-7 h-7 rounded-lg bg-[#ede8e1] flex items-center justify-center text-[#1c1917] shrink-0">
                    <Briefcase className="w-4 h-4" />
                  </div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="flex-1 bg-[#ede8e1] rounded-md h-7 overflow-hidden">
                      <div className="bg-[#99f6e4] text-[#134e4a] text-[11px] font-bold h-full flex items-center justify-center w-[9%]">
                        0%
                      </div>
                    </div>
                    <span className="text-[12px] font-bold text-[#78716c] w-36 shrink-0 uppercase tracking-tight">
                      0 Work Messages
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Bottom Right Card: 1 day streak (Contribution Heatmap) */}
            <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 flex flex-col justify-between">
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h2 className="text-[22px] font-bold text-[#1c1917]">
                    1 day streak
                  </h2>
                  <span className="text-[11px] font-bold tracking-wider uppercase text-[#78716c]">
                    Longest streak | 6 days
                  </span>
                </div>

                {/* Heatmap Month header */}
                <div className="flex items-center justify-between text-xs text-[#78716c] font-medium pt-2">
                  <ChevronLeft className="w-4 h-4 cursor-pointer hover:text-[#1c1917]" />
                  <div className="flex items-center gap-7">
                    {months.map(m => (
                      <span 
                        key={m} 
                        onClick={() => setSelectedMonth(m)}
                        className={`cursor-pointer ${selectedMonth === m ? 'text-[#1c1917] font-bold' : 'hover:text-[#1c1917]'}`}
                      >
                        {m}
                      </span>
                    ))}
                  </div>
                  <ChevronRight className="w-4 h-4 cursor-pointer hover:text-[#1c1917]" />
                </div>

                {/* Heatmap Squares Grid */}
                <div className="flex items-start gap-2 pt-2 overflow-x-auto pb-2">
                  {/* Days labels */}
                  <div className="flex flex-col gap-1.5 text-[10px] text-[#a8a29e] font-medium pt-0.5">
                    {daysOfWeek.map(day => (
                      <span key={day} className="h-3.5 leading-none">
                        {day}
                      </span>
                    ))}
                  </div>

                  {/* 18 Weeks Columns */}
                  <div className="flex items-center gap-1.5">
                    {weeksData.map((week, wIdx) => (
                      <div key={wIdx} className="flex flex-col gap-1.5">
                        {week.map((level, dIdx) => (
                          <div
                            key={dIdx}
                            className={`w-3.5 h-3.5 rounded-[3px] transition-colors ${getHeatmapColor(level)}`}
                            title={`Activity level: ${level}`}
                          />
                        ))}
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              {/* Heatmap Legend */}
              <div className="flex items-center gap-2 text-xs text-[#78716c] pt-4 mt-2 border-t border-[#ede8e1]">
                <span>More</span>
                <div className="flex items-center gap-1">
                  <div className="w-3 h-3 rounded-[2px] bg-[#134e4a]" />
                  <div className="w-3 h-3 rounded-[2px] bg-[#0d9488]" />
                  <div className="w-3 h-3 rounded-[2px] bg-[#99f6e4]" />
                  <div className="w-3 h-3 rounded-[2px] bg-[#edeae5]" />
                </div>
                <span>Less</span>
              </div>
            </div>
          </div>
        </div>
      ) : (
        /* Voice Analysis Sub-Tab */
        <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-8 text-center space-y-4">
          <div className="w-12 h-12 rounded-full bg-[#ede8e1] flex items-center justify-center mx-auto text-[#1c1917]">
            <Info className="w-6 h-6" />
          </div>
          <h3 className="text-lg font-bold text-[#1c1917]">Voice Fingerprint Calibration</h3>
          <p className="text-sm text-[#78716c] max-w-md mx-auto">
            Flow is continuously adapting to your vocal cadence, acoustic timbre, and terminology.
            Speech clarity is currently calibrated at 99.4% confidence across 18,134 dictated words.
          </p>
        </div>
      )}
    </div>
  );
};
