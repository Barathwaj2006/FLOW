import React, { useState, useEffect } from 'react';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  userEmail: string | null;
  credits: number;
  onLoginSuccess: (email: string) => void;
  onClaimCredits: (amount: number) => void;
  onSignOut: () => void;
}

export const LoginModal: React.FC<LoginModalProps> = ({
  isOpen,
  onClose,
  userEmail,
  credits,
  onLoginSuccess,
  onClaimCredits,
  onSignOut,
}) => {
  const [step, setStep] = useState<'email' | 'code' | 'account'>(userEmail ? 'account' : 'email');
  const [emailInput, setEmailInput] = useState('');
  const [codeInput, setCodeInput] = useState('');
  const [demoCode, setDemoCode] = useState('482910');
  const [countdown, setCountdown] = useState(45);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const [claimedToday, setClaimedToday] = useState(false);

  useEffect(() => {
    if (userEmail) {
      setStep('account');
    } else {
      setStep('email');
    }
  }, [userEmail, isOpen]);

  useEffect(() => {
    let timer: any;
    if (step === 'code' && countdown > 0) {
      timer = setInterval(() => {
        setCountdown(prev => prev - 1);
      }, 1000);
    }
    return () => clearInterval(timer);
  }, [step, countdown]);

  if (!isOpen) return null;

  const handleSendCode = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanEmail = emailInput.trim().toLowerCase();
    if (!cleanEmail || !cleanEmail.includes('@') || !cleanEmail.includes('.')) {
      setErrorMsg('Please enter a valid email address.');
      return;
    }

    setErrorMsg(null);
    setIsSending(true);

    // Simulate sending 6-digit verification code
    setTimeout(() => {
      const generated = Math.floor(100000 + Math.random() * 900000).toString();
      setDemoCode(generated);
      setIsSending(false);
      setStep('code');
      setCountdown(45);
      setCodeInput('');
    }, 600);
  };

  const handleVerifyCode = (e: React.FormEvent) => {
    e.preventDefault();
    if (codeInput.trim().length !== 6) {
      setErrorMsg('Please enter the 6-digit verification code.');
      return;
    }

    if (codeInput.trim() !== demoCode && codeInput.trim() !== '123456') {
      setErrorMsg('Incorrect code. Please enter the verification code sent to your email.');
      return;
    }

    setErrorMsg(null);
    onLoginSuccess(emailInput.trim().toLowerCase());
    setStep('account');
  };

  const handleResend = () => {
    if (countdown > 0) return;
    const generated = Math.floor(100000 + Math.random() * 900000).toString();
    setDemoCode(generated);
    setCountdown(45);
    setErrorMsg(null);
  };

  const handleClaim = () => {
    if (claimedToday) return;
    onClaimCredits(25);
    setClaimedToday(true);
  };

  return (
    <div 
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in select-none"
      onClick={e => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div 
        className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden transform transition-all duration-200"
        role="dialog"
        onClick={e => e.stopPropagation()}
      >
        {/* Modal Top Bar */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-50 border-b border-slate-200">
          <div className="flex items-center gap-2.5">
            <div className="w-7 h-7 rounded-lg bg-sky-100 border border-sky-200 flex items-center justify-center text-sky-600">
              <span className="material-symbols-outlined text-[18px]">
                {step === 'account' ? 'account_circle' : 'mark_email_read'}
              </span>
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-900 leading-tight">
                {step === 'email' && 'Sign In to FLOW'}
                {step === 'code' && 'Verify Your Email'}
                {step === 'account' && 'Account & Cloud Credits'}
              </h2>
              <p className="text-[11px] text-slate-500">
                {step === 'email' && 'Enter your email for passwordless verification'}
                {step === 'code' && `6-digit code sent to ${emailInput}`}
                {step === 'account' && userEmail}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-200 hover:text-slate-700 transition-colors"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6">
          {/* STEP 1: EMAIL ENTRY */}
          {step === 'email' && (
            <form onSubmit={handleSendCode} className="flex flex-col gap-4">
              <div className="p-3.5 rounded-xl bg-sky-50 border border-sky-200 flex items-start gap-2.5 text-sky-900">
                <span className="material-symbols-outlined text-[20px] text-sky-600 shrink-0 mt-0.5">stars</span>
                <div className="text-xs space-y-1">
                  <div className="font-semibold">50 Free Cloud AI Credits Included</div>
                  <div className="text-sky-700 leading-relaxed text-[11px]">
                    Local voice dictation is 100% offline &amp; unlimited forever. Sign in to unlock cloud transformations, prompt formatting, and device syncing.
                  </div>
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-800" htmlFor="flow-login-email">
                  Email address
                </label>
                <div className="relative">
                  <span className="material-symbols-outlined absolute left-3 top-2.5 text-[18px] text-slate-400">
                    mail
                  </span>
                  <input
                    id="flow-login-email"
                    type="email"
                    required
                    autoFocus
                    value={emailInput}
                    onChange={e => setEmailInput(e.target.value)}
                    placeholder="name@company.com"
                    className="w-full h-10 pl-9 pr-3 rounded-xl bg-white border border-slate-300 text-slate-900 text-xs placeholder:text-slate-400 focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition-all"
                  />
                </div>
              </div>

              {errorMsg && (
                <div className="text-xs text-red-600 bg-red-50 border border-red-200 px-3 py-2 rounded-lg flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[16px]">error</span>
                  <span>{errorMsg}</span>
                </div>
              )}

              <button
                type="submit"
                disabled={isSending}
                className="w-full h-10 rounded-xl bg-sky-600 hover:bg-sky-700 active:scale-[0.985] text-white text-xs font-semibold flex items-center justify-center gap-2 shadow-xs transition-all disabled:opacity-50"
              >
                {isSending ? (
                  <>
                    <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                    <span>Sending code...</span>
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[16px]">send</span>
                    <span>Send Verification Code</span>
                  </>
                )}
              </button>

              <div className="text-center">
                <span className="text-[11px] text-slate-400">
                  Passwordless security • Instant 6-digit one-time code
                </span>
              </div>
            </form>
          )}

          {/* STEP 2: CODE VERIFICATION */}
          {step === 'code' && (
            <form onSubmit={handleVerifyCode} className="flex flex-col gap-4">
              {/* Demo banner showing the simulated verification code for fast offline testing */}
              <div className="p-3 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 text-xs flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-[18px] text-amber-600">key</span>
                  <span>One-Time Code:</span>
                  <span className="font-mono font-bold text-amber-800 tracking-wider text-sm">{demoCode}</span>
                </div>
                <button
                  type="button"
                  onClick={() => setCodeInput(demoCode)}
                  className="px-2 py-0.5 rounded bg-amber-200/60 hover:bg-amber-200 text-amber-900 text-[11px] font-semibold transition-colors"
                >
                  Auto-fill
                </button>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-800" htmlFor="flow-login-code">
                  Enter 6-digit code
                </label>
                <input
                  id="flow-login-code"
                  type="text"
                  maxLength={6}
                  autoFocus
                  value={codeInput}
                  onChange={e => setCodeInput(e.target.value.replace(/\D/g, ''))}
                  placeholder="• • • • • •"
                  className="w-full h-12 text-center font-mono text-lg font-bold tracking-[0.4em] rounded-xl bg-slate-50 border border-slate-300 text-slate-900 focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition-all"
                />
              </div>

              {errorMsg && (
                <div className="text-xs text-red-600 bg-red-50 border border-red-200 px-3 py-2 rounded-lg flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[16px]">error</span>
                  <span>{errorMsg}</span>
                </div>
              )}

              <button
                type="submit"
                className="w-full h-10 rounded-xl bg-sky-600 hover:bg-sky-700 active:scale-[0.985] text-white text-xs font-semibold flex items-center justify-center gap-2 shadow-xs transition-all"
              >
                <span className="material-symbols-outlined text-[16px]">verified</span>
                <span>Verify &amp; Sign In</span>
              </button>

              <div className="flex items-center justify-between pt-1 text-xs">
                <button
                  type="button"
                  onClick={() => setStep('email')}
                  className="text-slate-500 hover:text-slate-800 text-[11px] font-medium"
                >
                  Change email
                </button>

                <button
                  type="button"
                  disabled={countdown > 0}
                  onClick={handleResend}
                  className={`text-[11px] font-medium ${
                    countdown > 0 ? 'text-slate-400 cursor-not-allowed' : 'text-sky-600 hover:underline'
                  }`}
                >
                  {countdown > 0 ? `Resend code in ${countdown}s` : 'Resend code'}
                </button>
              </div>
            </form>
          )}

          {/* STEP 3: ACCOUNT & CREDITS DASHBOARD */}
          {step === 'account' && (
            <div className="flex flex-col gap-4">
              {/* Credits Balance Card */}
              <div className="p-4 rounded-2xl bg-gradient-to-br from-sky-500 to-sky-700 text-white shadow-md flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] uppercase tracking-wider font-semibold opacity-90">
                    Available AI Credits
                  </span>
                  <span className="px-2 py-0.5 rounded-full bg-white/20 text-white text-[10px] font-bold">
                    Active Plan: Pro
                  </span>
                </div>

                <div className="flex items-baseline gap-2">
                  <span className="text-3xl font-extrabold tracking-tight font-mono">{credits}</span>
                  <span className="text-xs opacity-80">Cloud AI Credits</span>
                </div>

                <div className="pt-2 border-t border-white/20 flex items-center justify-between text-xs">
                  <span className="text-[11px] opacity-90">Local Dictation: Unlimited (Free)</span>
                  <button
                    onClick={handleClaim}
                    disabled={claimedToday}
                    className={`px-3 py-1 rounded-lg text-xs font-semibold shadow-xs transition-all ${
                      claimedToday
                        ? 'bg-white/20 text-white/70 cursor-not-allowed'
                        : 'bg-white text-sky-700 hover:bg-sky-50 active:scale-95'
                    }`}
                  >
                    {claimedToday ? 'Claimed Today ✓' : 'Claim Daily Bonus (+25)'}
                  </button>
                </div>
              </div>

              {/* Account Details */}
              <div className="rounded-xl border border-slate-200 divide-y divide-slate-100 text-xs">
                <div className="p-3 flex items-center justify-between">
                  <span className="text-slate-500">Account Email</span>
                  <span className="font-semibold text-slate-800">{userEmail}</span>
                </div>
                <div className="p-3 flex items-center justify-between">
                  <span className="text-slate-500">Offline Voice Model</span>
                  <span className="font-medium text-emerald-600 flex items-center gap-1">
                    <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                    DirectML Local Whisper
                  </span>
                </div>
                <div className="p-3 flex items-center justify-between">
                  <span className="text-slate-500">Security Standard</span>
                  <span className="font-mono text-slate-700">Windows DPAPI Protected</span>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2 pt-1">
                <button
                  type="button"
                  onClick={onClose}
                  className="flex-1 h-9 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-semibold transition-colors shadow-2xs"
                >
                  Done
                </button>

                <button
                  type="button"
                  onClick={() => {
                    onSignOut();
                    setStep('email');
                  }}
                  className="h-9 px-3 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 text-xs font-semibold transition-colors flex items-center gap-1.5 shadow-2xs"
                >
                  <span className="material-symbols-outlined text-[16px]">logout</span>
                  <span>Sign Out</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
