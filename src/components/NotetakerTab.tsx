import React, { useState } from 'react';
import { Mic, Disc, Play, Pause, Download, Copy, Sparkles, Check, Clock } from 'lucide-react';

export const NotetakerTab: React.FC = () => {
  const [isRecording, setIsRecording] = useState(false);
  const [activeMeeting, setActiveMeeting] = useState('Product Design Sync');
  const [copied, setCopied] = useState(false);

  const sampleNotes = [
    {
      time: '00:12',
      speaker: 'Barathwaj',
      text: 'Kickoff: Discussing the new compact glassmorphic Flow bar and native Windows UI alignment.',
    },
    {
      time: '01:45',
      speaker: 'Alex (Design)',
      text: 'Confirmed that the waveform will run dynamically during voice dictation with zero latency.',
    },
    {
      time: '03:10',
      speaker: 'Sarah (Eng)',
      text: 'DirectML local inference engine handles speech recognition offline without cloud dependencies.',
    },
  ];

  const actionItems = [
    'Deploy the updated Windows 11 title chrome and sidebar',
    'Verify zero-enter invariant prevents automatic submission',
    'Test multi-harmonic audio waves in both light and dark backgrounds',
  ];

  const handleCopy = () => {
    const text = sampleNotes.map(n => `[${n.time}] ${n.speaker}: ${n.text}`).join('\n');
    navigator.clipboard?.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
            Notetaker
          </h1>
          <span className="px-2 py-0.5 text-[10px] font-bold rounded-full bg-[#f97316] text-white">
            New!
          </span>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setIsRecording(!isRecording)}
            className={`px-5 py-2 rounded-full text-xs font-semibold flex items-center gap-2 transition-all shadow-xs ${
              isRecording
                ? 'bg-rose-600 text-white animate-pulse'
                : 'bg-[#1c1917] text-white hover:bg-black'
            }`}
          >
            <Disc className="w-4 h-4" />
            <span>{isRecording ? 'Stop Recording' : 'Record Meeting'}</span>
          </button>
        </div>
      </div>

      {/* Hero Banner */}
      <div className="relative rounded-2xl overflow-hidden shadow-xs min-h-[160px] flex items-center justify-between p-7 text-white">
        <div 
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: `radial-gradient(circle at 80% 30%, rgba(217, 119, 6, 0.45), transparent 70%),
                              linear-gradient(120deg, #1c1917 0%, #292524 60%, #451a03 100%)`
          }}
        >
          <div className="absolute inset-0 bg-black/25 backdrop-blur-[2px]"></div>
        </div>

        <div className="relative z-10 max-w-lg space-y-1.5">
          <h2 className="text-[24px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
            Meetings transcribed and summarized automatically
          </h2>
          <p className="text-[13.5px] text-white/80 font-normal leading-relaxed">
            Record audio meetings locally with multi-speaker detection and instant bulleted action items.
          </p>
        </div>
      </div>

      {/* Meeting Transcript & Action Items Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left 2 Cols: Transcript */}
        <div className="lg:col-span-2 bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 space-y-4">
          <div className="flex items-center justify-between border-b border-[#ede8e1] pb-3">
            <div>
              <h3 className="text-base font-bold text-[#1c1917]">{activeMeeting}</h3>
              <span className="text-xs text-[#78716c]">Recorded today • 14 mins</span>
            </div>
            <button
              onClick={handleCopy}
              className="px-3 py-1.5 rounded-lg bg-[#ede8e1] text-xs font-medium text-[#1c1917] hover:bg-[#e4ded5] flex items-center gap-1.5"
            >
              {copied ? <Check className="w-3.5 h-3.5 text-emerald-600" /> : <Copy className="w-3.5 h-3.5" />}
              <span>{copied ? 'Copied' : 'Copy'}</span>
            </button>
          </div>

          <div className="space-y-4 pt-2">
            {sampleNotes.map((note, idx) => (
              <div key={idx} className="flex items-start gap-4">
                <span className="text-xs font-mono text-[#a8a29e] pt-0.5 w-12 shrink-0">
                  {note.time}
                </span>
                <div className="space-y-1 flex-1">
                  <span className="text-xs font-bold text-[#1c1917]">
                    {note.speaker}
                  </span>
                  <p className="text-[13.5px] text-[#44403c] leading-relaxed">
                    {note.text}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right Col: AI Summary & Action Items */}
        <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-6 space-y-4">
          <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-[#a8a29e]">
            <Sparkles className="w-3.5 h-3.5 text-amber-600" />
            <span>Key Takeaways</span>
          </div>

          <div className="space-y-2.5">
            {actionItems.map((item, idx) => (
              <div key={idx} className="flex items-start gap-2 text-[13px] text-[#44403c]">
                <span className="w-1.5 h-1.5 rounded-full bg-[#134e4a] mt-1.5 shrink-0" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
