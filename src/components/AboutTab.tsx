import React from 'react';

export const AboutTab: React.FC = () => {
  const specs = [
    { label: 'Application Version', value: 'v1.0.0 (Production-Grade Release)' },
    { label: 'Target Platform', value: 'Windows 10 / 11 x64 Native Desktop' },
    { label: 'Audio Engine', value: 'WASAPI Low-Latency Loopback' },
    { label: 'Inference Backend', value: 'Whisper.net / DirectML / AVX2 C++' },
    { label: 'Storage Engine', value: 'SQLite FTS5 Local Storage' },
    { label: 'Text Injection', value: 'UI Automation + SendInput (Zero-Enter Invariant)' },
  ];

  const invariants = [
    {
      title: '100% Offline Sovereignty (Zero Cloud Audio)',
      desc: 'Audio captured from physical microphones is processed strictly on-device. Audio buffers are never streamed, transmitted, or logged to external servers.',
    },
    {
      title: 'Inviolable Zero-Enter Safety Invariant',
      desc: 'The text injection subsystem permanently strips carriage return and line feed characters from simulating submission, ensuring user safety across email and chat applications.',
    },
    {
      title: 'Faithful Content Lock & No-Invention Rule',
      desc: 'Speech transformations strictly preserve technical entities, variable names, code blocks, URLs, and negative constraints without hallucination.',
    },
    {
      title: 'Fail-Closed Privacy Gate',
      desc: 'Password controls and sensitive input fields are automatically detected via UI Automation and fail-closed to prevent accidental credential transcription.',
    },
  ];

  return (
    <div id="about-tab-content" className="flex flex-col w-full pb-16 max-w-4xl mx-auto space-y-4 select-none pt-1">
      {/* Brand Header */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs">
        <div className="flex items-center gap-3.5">
          <div className="w-11 h-11 rounded-xl bg-[#0284c7] flex items-center justify-center shadow-xs text-white">
            <span className="material-symbols-outlined text-[24px]">mic</span>
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-extrabold text-slate-900 tracking-tight">FLOW</h1>
              <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-wider">
                Production-Ready
              </span>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              System-Wide AI Voice Productivity Platform for Windows 10 &amp; 11
            </p>
          </div>
        </div>
      </div>

      {/* Specifications Grid */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-3">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-[#0284c7] text-[18px]">memory</span>
          <h2>Architecture &amp; Engine Specifications</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
          {specs.map((item, idx) => (
            <div key={idx} className="bg-slate-50 border border-slate-200 rounded-lg p-3 space-y-0.5">
              <span className="text-[11px] text-slate-500 font-medium">{item.label}</span>
              <div className="text-xs font-bold text-slate-800 font-mono">{item.value}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Core Safety Invariants */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-3">
        <div className="flex items-center gap-2 text-sm font-bold text-slate-900 border-b border-slate-100 pb-3">
          <span className="material-symbols-outlined text-emerald-600 text-[18px]">verified_user</span>
          <h2>Inviolable Safety &amp; Security Guarantees</h2>
        </div>

        <div className="space-y-2">
          {invariants.map((item, idx) => (
            <div key={idx} className="bg-slate-50 border border-slate-200 rounded-lg p-3.5 space-y-1">
              <div className="flex items-center gap-2 text-xs font-bold text-slate-900">
                <span className="material-symbols-outlined text-emerald-600 text-[16px]">check_circle</span>
                <span>{item.title}</span>
              </div>
              <p className="text-xs text-slate-600 leading-relaxed pl-6">{item.desc}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Footer Info */}
      <div className="text-center py-2 text-xs text-slate-400 font-mono">
        FLOW Open Source Project • Licensed under MIT License • GitHub: Barathwaj2006/FLOW
      </div>
    </div>
  );
};
