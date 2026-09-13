import React, { useState } from 'react';
import { Wand2, Play, Sparkles, Copy, Check, ArrowRight } from 'lucide-react';

export const TransformsTab: React.FC = () => {
  const [inputText, setInputText] = useState('so um basically we need to ship this on wednesday and make sure the directml backend is completely isolated from any cloud network request');
  const [selectedTransform, setSelectedTransform] = useState('formal');
  const [outputText, setOutputText] = useState('We must ship this release by Wednesday. Please ensure the DirectML inference engine is fully isolated from all cloud network dependencies.');
  const [copied, setCopied] = useState(false);

  const transforms = [
    {
      id: 'formal',
      title: 'Executive Polish',
      desc: 'Removes fillers, structures sentences clearly for leadership',
      prompt: 'Clean, formalize, and summarize clearly',
    },
    {
      id: 'bullets',
      title: 'Actionable Bullet Points',
      desc: 'Converts rambled speech into crisp bullet items',
      prompt: 'Convert to bulleted action items',
    },
    {
      id: 'dev',
      title: 'Technical Specification',
      desc: 'Formats technical terminology, code paths, and requirements',
      prompt: 'Format as technical spec',
    },
    {
      id: 'casual',
      title: 'Conversational Friendly',
      desc: 'Light, relaxed tone with natural phrasing for Slack / Discord',
      prompt: 'Friendly peer conversational tone',
    },
  ];

  const handleRunTransform = (transId: string) => {
    setSelectedTransform(transId);
    if (transId === 'formal') {
      setOutputText('We must ship this release by Wednesday. Please ensure the DirectML inference engine is fully isolated from all cloud network dependencies.');
    } else if (transId === 'bullets') {
      setOutputText('• Target release date: Wednesday\n• Critical requirement: 100% offline DirectML isolation with zero cloud network calls.');
    } else if (transId === 'dev') {
      setOutputText('REQUIREMENT:\n- Milestone: Wednesday Release\n- Component: DirectML Inference Layer\n- Constraint: Strict offline boundary; zero external network ingress/egress.');
    } else {
      setOutputText("Hey team, let's get this shipped by Wednesday! Just need to double check that our DirectML setup is totally offline with no cloud pings.");
    }
  };

  const handleCopy = () => {
    navigator.clipboard?.writeText(outputText);
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  return (
    <div className="w-full max-w-6xl mx-auto flex flex-col space-y-6 select-none pb-20">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-[28px] font-bold text-[#1c1917] tracking-tight">
          Transforms
        </h1>
      </div>

      {/* Hero Banner */}
      <div className="relative rounded-2xl overflow-hidden shadow-xs min-h-[160px] flex items-center justify-between p-7 text-white">
        <div 
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage: `radial-gradient(circle at 80% 30%, rgba(147, 51, 234, 0.45), transparent 70%),
                              linear-gradient(120deg, #1c1917 0%, #292524 60%, #3b0764 100%)`
          }}
        >
          <div className="absolute inset-0 bg-black/25 backdrop-blur-[2px]"></div>
        </div>

        <div className="relative z-10 max-w-lg space-y-1.5">
          <h2 className="text-[24px] font-serif-editorial font-normal tracking-wide text-white leading-tight">
            Transform voice dictations into any format
          </h2>
          <p className="text-[13.5px] text-white/80 font-normal leading-relaxed">
            Apply instant prompt recipes while preserving the exact technical requirements and zero-hallucination guarantee.
          </p>
        </div>
      </div>

      {/* Transform Options */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {transforms.map(t => (
          <div
            key={t.id}
            onClick={() => handleRunTransform(t.id)}
            className={`p-5 rounded-2xl cursor-pointer transition-all ${
              selectedTransform === t.id
                ? 'bg-white border-2 border-[#1c1917] shadow-sm ring-2 ring-black/5'
                : 'bg-[#f9f8f6] border border-[#ede8e1] hover:border-[#d6cfc4]'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <Sparkles className="w-4 h-4 text-amber-600" />
              <h3 className="font-bold text-sm text-[#1c1917]">{t.title}</h3>
            </div>
            <p className="text-xs text-[#78716c] leading-relaxed">{t.desc}</p>
          </div>
        ))}
      </div>

      {/* Interactive Transform Playground */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-5 space-y-3">
          <label className="text-xs font-bold uppercase text-[#78716c] block">
            Raw Voice Input
          </label>
          <textarea
            value={inputText}
            onChange={e => setInputText(e.target.value)}
            rows={6}
            className="w-full text-sm leading-relaxed p-3 rounded-xl bg-white border border-[#ede8e1] focus:outline-none focus:ring-1 focus:ring-[#1c1917]"
          />
          <button
            onClick={() => handleRunTransform(selectedTransform)}
            className="px-4 py-2 rounded-xl bg-[#1c1917] text-white text-xs font-semibold hover:bg-black flex items-center gap-2"
          >
            <Wand2 className="w-3.5 h-3.5" />
            <span>Apply Transform</span>
          </button>
        </div>

        <div className="bg-[#f9f8f6] border border-[#ede8e1] rounded-2xl p-5 space-y-3 flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-3">
              <label className="text-xs font-bold uppercase text-[#78716c]">
                Transformed Output
              </label>
              <button
                onClick={handleCopy}
                className="px-3 py-1 rounded-lg bg-[#ede8e1] text-xs font-medium text-[#1c1917] hover:bg-[#e4ded5] flex items-center gap-1.5"
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-600" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{copied ? 'Copied' : 'Copy'}</span>
              </button>
            </div>
            <div className="p-3.5 rounded-xl bg-white border border-[#ede8e1] text-sm leading-relaxed text-[#1c1917] min-h-[140px] whitespace-pre-line font-normal">
              {outputText}
            </div>
          </div>

          <div className="text-[11px] text-[#a8a29e] flex items-center gap-1.5 pt-2">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            <span>Content Lock Verified • Zero Unmentioned Inventions</span>
          </div>
        </div>
      </div>
    </div>
  );
};
