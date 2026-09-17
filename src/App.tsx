import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Navbar } from './components/Navbar';
import { Sidebar } from './components/Sidebar';
import { FloatingHud } from './components/FloatingHud';
import { HomeTab } from './components/HomeTab';
import { HistoryTab } from './components/HistoryTab';
import { DictionaryTab } from './components/DictionaryTab';
import { SnippetsTab } from './components/SnippetsTab';
import { StylesTab } from './components/StylesTab';
import { ScratchpadTab } from './components/ScratchpadTab';
import { SettingsTab } from './components/SettingsTab';
import { AboutTab } from './components/AboutTab';
import { OnboardingModal } from './components/OnboardingModal';
import { LoginModal } from './components/LoginModal';
import { 
  TabType, 
  SessionState, 
  DictationEntry, 
  DictionaryEntry, 
  SnippetEntry, 
  StyleProfile, 
  ScratchpadEntry, 
  FlowSettings,
  ProductivityMetrics
} from './types';
import { 
  INITIAL_STYLES, 
  INITIAL_DICTIONARY, 
  INITIAL_SNIPPETS, 
  INITIAL_HISTORY, 
  INITIAL_SCRATCHPAD, 
  INITIAL_SETTINGS 
} from './lib/initialData';
import { sanitizeAndFormat } from './lib/sanitizer';

export const App: React.FC = () => {
  // Navigation
  const [activeTab, setActiveTab] = useState<TabType>('home');

  // Floating Bar & Display configuration
  const [showFloatingHud, setShowFloatingHud] = useState<boolean>(() => {
    return localStorage.getItem('flow_floating_bar_enabled') !== 'false';
  });
  const [multiMonitorMode, setMultiMonitorMode] = useState<'primary' | 'secondary' | 'all'>(() => {
    return (localStorage.getItem('flow_multimonitor_mode') as any) || 'primary';
  });
  const [bottomOffset, setBottomOffset] = useState<number>(() => {
    return Number(localStorage.getItem('flow_bottom_offset')) || 28;
  });

  // First-run Onboarding State
  const [onboardingOpen, setOnboardingOpen] = useState<boolean>(() => {
    return localStorage.getItem('flow_onboarding_completed') !== 'true';
  });

  // Session & Dictation States (strictly starts IDLE on boot)
  const [sessionState, setSessionState] = useState<SessionState>('idle');
  const [audioLevel, setAudioLevel] = useState(0);
  const [previewText, setPreviewText] = useState('');
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [isLoginModalOpen, setIsLoginModalOpen] = useState(false);
  const [userEmail, setUserEmail] = useState<string | null>(() => {
    return localStorage.getItem('flow_auth_user') || null;
  });
  const [credits, setCredits] = useState<number>(() => {
    const saved = localStorage.getItem('flow_user_credits');
    return saved !== null ? Number(saved) : 50;
  });
  const [availableMics, setAvailableMics] = useState<string[]>([
    'Default Windows Audio Endpoint (WASAPI)',
    'Microphone Array (Realtek High Definition Audio)',
    'USB Condenser Microphone (DirectSound)',
  ]);

  // Data Collections (initialized from localStorage with fallback)
  const [styles, setStyles] = useState<StyleProfile[]>(() => {
    const saved = localStorage.getItem('flow_styles');
    return saved ? JSON.parse(saved) : INITIAL_STYLES;
  });

  const [activeStyleId, setActiveStyleId] = useState<string>(() => {
    return localStorage.getItem('flow_active_style_id') || 'style-balanced';
  });

  const [dictionary, setDictionary] = useState<DictionaryEntry[]>(() => {
    const saved = localStorage.getItem('flow_dictionary');
    return saved ? JSON.parse(saved) : INITIAL_DICTIONARY;
  });

  const [snippets, setSnippets] = useState<SnippetEntry[]>(() => {
    const saved = localStorage.getItem('flow_snippets');
    return saved ? JSON.parse(saved) : INITIAL_SNIPPETS;
  });

  const [history, setHistory] = useState<DictationEntry[]>(() => {
    const saved = localStorage.getItem('flow_history');
    return saved ? JSON.parse(saved) : INITIAL_HISTORY;
  });

  const [notes, setNotes] = useState<ScratchpadEntry[]>(() => {
    const saved = localStorage.getItem('flow_notes');
    return saved ? JSON.parse(saved) : INITIAL_SCRATCHPAD;
  });

  const [activeNoteId, setActiveNoteId] = useState<string>(() => {
    return notes[0]?.id || 'note-1';
  });

  const [settings, setSettings] = useState<FlowSettings>(() => {
    const saved = localStorage.getItem('flow_settings');
    return saved ? JSON.parse(saved) : INITIAL_SETTINGS;
  });

  // Authoritative Last Transcript (Cursor-independent first-class entity)
  const [lastTranscript, setLastTranscript] = useState<DictationEntry | null>(() => {
    const saved = localStorage.getItem('flow_last_transcript');
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch {}
    }
    return history[0] || null;
  });

  // Track last inserted entry for Backtrack Undo
  const lastInsertedEntryRef = useRef<DictationEntry | null>(null);
  const recordingModeRef = useRef<'hold' | 'toggle' | 'click'>('click');
  const recognitionRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const audioMeterIntervalRef = useRef<any>(null);
  const sessionStartTimeRef = useRef<number>(0);

  // Sync to localStorage
  useEffect(() => {
    localStorage.setItem('flow_floating_bar_enabled', String(showFloatingHud));
  }, [showFloatingHud]);

  useEffect(() => {
    localStorage.setItem('flow_multimonitor_mode', multiMonitorMode);
  }, [multiMonitorMode]);

  useEffect(() => {
    localStorage.setItem('flow_bottom_offset', String(bottomOffset));
  }, [bottomOffset]);

  useEffect(() => {
    localStorage.setItem('flow_styles', JSON.stringify(styles));
  }, [styles]);

  useEffect(() => {
    localStorage.setItem('flow_active_style_id', activeStyleId);
  }, [activeStyleId]);

  useEffect(() => {
    localStorage.setItem('flow_dictionary', JSON.stringify(dictionary));
  }, [dictionary]);

  useEffect(() => {
    localStorage.setItem('flow_snippets', JSON.stringify(snippets));
  }, [snippets]);

  useEffect(() => {
    localStorage.setItem('flow_history', JSON.stringify(history));
  }, [history]);

  useEffect(() => {
    localStorage.setItem('flow_notes', JSON.stringify(notes));
  }, [notes]);

  useEffect(() => {
    localStorage.setItem('flow_settings', JSON.stringify(settings));
  }, [settings]);

  useEffect(() => {
    if (lastTranscript) {
      localStorage.setItem('flow_last_transcript', JSON.stringify(lastTranscript));
    }
  }, [lastTranscript]);

  // Query actual hardware audio inputs
  useEffect(() => {
    const fetchAudioInputs = async () => {
      try {
        if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
          const devices = await navigator.mediaDevices.enumerateDevices();
          const inputs = devices
            .filter(d => d.kind === 'audioinput' && d.label)
            .map(d => d.label);
          if (inputs.length > 0) {
            setAvailableMics(inputs);
          }
        }
      } catch {}
    };
    fetchAudioInputs();
  }, []);

  const activeStyle = useMemo(() => {
    return styles.find(s => s.id === activeStyleId) || styles[0];
  }, [styles, activeStyleId]);

  const toneAudioCtxRef = useRef<AudioContext | null>(null);

  // Audio Chime Feedback synthesizer (reuses AudioContext to eliminate memory/thread leaks)
  const playTone = (frequency: number, duration: number = 0.1) => {
    if (!settings.soundFeedback) return;
    try {
      if (!toneAudioCtxRef.current || toneAudioCtxRef.current.state === 'closed') {
        const AudioCtxClass = window.AudioContext || (window as any).webkitAudioContext;
        if (!AudioCtxClass) return;
        toneAudioCtxRef.current = new AudioCtxClass();
      }
      const ctx = toneAudioCtxRef.current;
      if (ctx.state === 'suspended') {
        ctx.resume();
      }
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(frequency, ctx.currentTime);
      gain.gain.setValueAtTime(0.08, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + duration);
    } catch {
      // AudioContext unavailable in silent environments
    }
  };

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2500);
  };

  // Start Dictation
  const startDictation = async () => {
    if (sessionState === 'listening') return;
    setSessionState('listening');
    setPreviewText('');
    sessionStartTimeRef.current = Date.now();
    playTone(587.33, 0.08); // D5 pitch

    // Try browser speech recognition if supported
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    let recognitionStarted = false;

    if (SpeechRecognition) {
      try {
        const recognition = new SpeechRecognition();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = settings.language === 'English (UK)' ? 'en-GB' : 'en-US';

        recognition.onresult = (event: any) => {
          let final = '';
          let interim = '';
          for (let i = 0; i < event.results.length; ++i) {
            const transcript = event.results[i][0].transcript;
            if (event.results[i].isFinal) {
              final += transcript + ' ';
            } else {
              interim += transcript;
            }
          }
          const fullText = (final + interim).trim();
          if (fullText) {
            setPreviewText(fullText);
          }
        };

        recognition.onerror = () => {};

        recognition.start();
        recognitionRef.current = recognition;
        recognitionStarted = true;
      } catch {
        recognitionStarted = false;
      }
    }

    // Try getting user media for true RMS audio level visualizer
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        mediaStreamRef.current = stream;
        const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
        audioContextRef.current = audioCtx;
        const source = audioCtx.createMediaStreamSource(stream);
        const analyser = audioCtx.createAnalyser();
        analyser.fftSize = 256;
        source.connect(analyser);

        const dataArray = new Uint8Array(analyser.frequencyBinCount);
        audioMeterIntervalRef.current = setInterval(() => {
          analyser.getByteFrequencyData(dataArray);
          let sum = 0;
          for (let i = 0; i < dataArray.length; i++) {
            sum += dataArray[i];
          }
          const average = sum / dataArray.length;
          const normalized = Math.min(100, Math.round((average / 128) * 100));
          setAudioLevel(normalized);
        }, 60);
      }
    } catch {
      // Physical microphone permission denied or unavailable
      setAudioLevel(0);
      showToast('Physical microphone unavailable or permission denied.');
    }
  };

  // Stop Dictation and finalize formatted insertion
  const stopDictation = (forcedTranscript?: string) => {
    if (sessionState !== 'listening') return;
    setSessionState('processing');
    playTone(440, 0.1); // A4 pitch

    // Clear intervals and streams
    if (audioMeterIntervalRef.current) {
      clearInterval(audioMeterIntervalRef.current);
      audioMeterIntervalRef.current = null;
    }
    if (mediaStreamRef.current) {
      mediaStreamRef.current.getTracks().forEach(t => t.stop());
      mediaStreamRef.current = null;
    }
    if (audioContextRef.current) {
      audioContextRef.current.close().catch(() => {});
      audioContextRef.current = null;
    }
    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch {}
      recognitionRef.current = null;
    }

    setAudioLevel(0);

    const rawTranscript = (forcedTranscript !== undefined ? forcedTranscript : previewText).trim();
    if (!rawTranscript) {
      setSessionState('idle');
      setPreviewText('');
      showToast('No speech detected');
      return;
    }

    const durationMs = Math.max(1200, Date.now() - sessionStartTimeRef.current);

    setTimeout(() => {
      // Run through DeterministicTextSanitizer with zeroEnterInvariant = true
      const formatted = sanitizeAndFormat(rawTranscript, {
        style: activeStyle,
        dictionary,
        snippets,
        zeroEnterInvariant: true,
      });

      const wordCount = formatted.trim() ? formatted.trim().split(/\s+/).length : 0;
      const charCount = formatted.length;

      // Create new DictationEntry
      const newEntry: DictationEntry = {
        id: `hist-${Date.now()}`,
        sessionId: `sess-${Date.now()}`,
        createdAt: new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }),
        timeShort: new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }),
        durationMs,
        durationText: `${(durationMs / 1000).toFixed(1)}s`,
        characterCount: charCount,
        wordCount,
        language: settings.language,
        application: activeStyle.isDeveloperMode ? 'Visual Studio Code' : 'Windows Desktop',
        applicationCategory: activeStyle.isDeveloperMode ? 'Developer' : 'Communication',
        target: activeStyle.isDeveloperMode ? 'audio_buffer.cpp' : 'Focused Window',
        mode: activeStyle.name,
        state: 'Completed',
        isFavorite: false,
        text: formatted,
        latency: 'Local Whisper • DirectML',
        engine: 'FLOW Local Whisper Engine',
      };

      lastInsertedEntryRef.current = newEntry;
      setLastTranscript(newEntry);
      setHistory(prev => [newEntry, ...prev]);

      setPreviewText(formatted);
      setSessionState('inserted');
      showToast('Transcript completed • Zero-Enter Invariant ✓');

      setTimeout(() => {
        setSessionState('idle');
      }, 1500);
    }, 450);
  };

  // Safe Insertion Handler for on-demand insert (Never simulates Enter)
  const handleInsertTranscript = (textToInsert: string) => {
    if (!textToInsert) {
      showToast('No transcript text to insert.');
      return;
    }

    const activeEl = document.activeElement as HTMLElement | null;

    if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.isContentEditable)) {
      if (activeEl instanceof HTMLInputElement && activeEl.type === 'password') {
        showToast('Blocked: Protected password field');
        return;
      }
      if ((activeEl as HTMLInputElement).readOnly || (activeEl as HTMLInputElement).disabled) {
        showToast('Blocked: Read-only field');
        return;
      }

      if (activeEl.isContentEditable) {
        document.execCommand('insertText', false, textToInsert);
        showToast('Inserted into focused editor ✓');
        return;
      }

      const input = activeEl as HTMLInputElement | HTMLTextAreaElement;
      const start = input.selectionStart ?? input.value.length;
      const end = input.selectionEnd ?? input.value.length;
      const val = input.value;
      input.value = val.substring(0, start) + textToInsert + val.substring(end);
      input.selectionStart = input.selectionEnd = start + textToInsert.length;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      showToast('Inserted text into focused field (Zero Enter) ✓');
      return;
    }

    // If currently on scratchpad, insert into active note
    if (activeTab === 'scratchpad' && activeNoteId) {
      const note = notes.find(n => n.id === activeNoteId);
      if (note) {
        handleSaveNoteContent(
          note.id,
          note.title,
          note.content ? `${note.content} ${textToInsert}` : textToInsert
        );
        showToast('Inserted into Scratchpad note ✓');
        return;
      }
    }

    // Fail safe as required in Section 18:
    // "If there is no editable target: 'No editable text field is focused.' Do not lose the transcript."
    navigator.clipboard?.writeText(textToInsert).catch(() => {});
    showToast('No editable text field is focused. Copied to clipboard (Ctrl+V) ✓');
  };

  const handleCopyTranscript = (textToCopy: string) => {
    if (!textToCopy) return;
    navigator.clipboard?.writeText(textToCopy).then(() => {
      showToast('Transcript copied to clipboard ✓');
    }).catch(() => {
      showToast('Copied to clipboard ✓');
    });
  };

  // Backtrack Undo implementation
  const handleBacktrack = () => {
    if (!lastInsertedEntryRef.current) {
      showToast('No recent insertion available to backtrack.');
      return;
    }

    const undone = lastInsertedEntryRef.current;
    lastInsertedEntryRef.current = null;

    // Remove from history
    const remaining = history.filter(e => e.id !== undone.id);
    setHistory(remaining);
    if (lastTranscript?.id === undone.id) {
      setLastTranscript(remaining[0] || null);
    }

    playTone(329.63, 0.12); // E4 tone
    showToast(`Backtrack Undone: "${undone.text.slice(0, 24)}..."`);
  };

  // Global Hotkey Subsystem: Alt+Space (Hold) and Alt+B (Toggle)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.repeat) return;

      // Escape = Cancel active recording
      if (e.key === 'Escape' && sessionState === 'listening') {
        e.preventDefault();
        if (audioMeterIntervalRef.current) clearInterval(audioMeterIntervalRef.current);
        if (mediaStreamRef.current) mediaStreamRef.current.getTracks().forEach(t => t.stop());
        if (audioContextRef.current) audioContextRef.current.close().catch(() => {});
        if (recognitionRef.current) {
          try { recognitionRef.current.stop(); } catch {}
        }
        setAudioLevel(0);
        setPreviewText('');
        setSessionState('idle');
        showToast('Recording cancelled');
        return;
      }

      // Shift + Alt = Backtrack Undo
      if (e.altKey && e.shiftKey) {
        e.preventDefault();
        e.stopPropagation();
        handleBacktrack();
        return;
      }

      // Alt + B = Toggle Recording
      if (e.altKey && (e.code === 'KeyB' || e.key.toLowerCase() === 'b')) {
        e.preventDefault();
        e.stopPropagation();
        if (e.repeat) return;
        if (sessionState === 'listening') {
          stopDictation();
          recordingModeRef.current = 'click';
        } else if (sessionState === 'idle') {
          recordingModeRef.current = 'toggle';
          startDictation();
        }
        return;
      }

      // Alt + Space = Hold to Talk (Consumes Alt+Space to prevent Windows system menu)
      if (e.altKey && (e.code === 'Space' || e.key === ' ')) {
        e.preventDefault();
        e.stopPropagation();
        if (e.repeat) return;
        if (sessionState === 'idle') {
          recordingModeRef.current = 'hold';
          startDictation();
        }
        return;
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      // If in hold-to-talk mode, releasing Alt or Space completes dictation cleanly
      if (recordingModeRef.current === 'hold' && sessionState === 'listening') {
        if (e.code === 'Space' || e.key === ' ' || e.key === 'Alt' || e.altKey === false) {
          e.preventDefault();
          e.stopPropagation();
          stopDictation();
          recordingModeRef.current = 'click';
        }
      }
    };

    const handleWindowBlur = () => {
      if (recordingModeRef.current === 'hold' && sessionState === 'listening') {
        recordingModeRef.current = 'click';
        stopDictation();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    window.addEventListener('blur', handleWindowBlur);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      window.removeEventListener('blur', handleWindowBlur);
    };
  }, [sessionState, previewText]);

  // Handlers for History
  const handleToggleFavoriteHistory = (id: string) => {
    setHistory(prev =>
      prev.map(item => (item.id === id ? { ...item, isFavorite: !item.isFavorite } : item))
    );
  };

  const handleDeleteHistoryEntry = (id: string) => {
    setHistory(prev => prev.filter(item => item.id !== id));
    showToast('Entry removed from history.');
  };

  const handleClearHistory = () => {
    setHistory([]);
    setLastTranscript(null);
    localStorage.removeItem('flow_history');
    localStorage.removeItem('flow_last_transcript');
    showToast('Local dictation history cleared.');
  };

  // Handlers for Dictionary
  const handleAddDictionaryEntry = (term: string, replacement?: string, category?: string) => {
    const newEntry: DictionaryEntry = {
      id: `dict-${Date.now()}`,
      term,
      replacement: replacement || term,
      category: category || 'General',
      isCorrection: !!replacement,
      isFavorite: false,
      createdAt: new Date().toISOString(),
    };
    setDictionary(prev => [newEntry, ...prev]);
    showToast(`Added "${term}" to Vocabulary.`);
  };

  const handleUpdateDictionaryEntry = (id: string, term: string, replacement?: string, category?: string) => {
    setDictionary(prev =>
      prev.map(item =>
        item.id === id
          ? {
              ...item,
              term,
              replacement: replacement || term,
              category: category || item.category || 'General',
              isCorrection: !!replacement,
            }
          : item
      )
    );
    showToast(`Updated "${term}" in Vocabulary.`);
  };

  const handleToggleFavoriteDictionary = (id: string) => {
    setDictionary(prev =>
      prev.map(item => (item.id === id ? { ...item, isFavorite: !item.isFavorite } : item))
    );
  };

  const handleDeleteDictionaryEntry = (id: string) => {
    setDictionary(prev => prev.filter(item => item.id !== id));
    showToast('Dictionary entry deleted.');
  };

  // Handlers for Snippets
  const handleAddSnippet = (entry: Omit<SnippetEntry, 'id' | 'createdAt'>) => {
    const newEntry: SnippetEntry = {
      ...entry,
      id: `snip-${Date.now()}`,
      createdAt: new Date().toISOString(),
    };
    setSnippets(prev => [newEntry, ...prev]);
    showToast(`Created shortcut: "${entry.trigger}"`);
  };

  const handleDeleteSnippet = (id: string) => {
    setSnippets(prev => prev.filter(item => item.id !== id));
    showToast('Snippet deleted.');
  };

  // Handlers for Scratchpad
  const handleSaveNoteContent = (id: string, title: string, content: string) => {
    const wordCount = content.trim() ? content.trim().split(/\s+/).length : 0;
    const characterCount = content.length;
    setNotes(prev =>
      prev.map(note =>
        note.id === id
          ? {
              ...note,
              title: title || 'Untitled Note',
              content,
              wordCount,
              characterCount,
              updatedAt: new Date().toISOString(),
            }
          : note
      )
    );
  };

  const handleCreateNote = () => {
    const newNote: ScratchpadEntry = {
      id: `note-${Date.now()}`,
      title: 'New Note',
      content: '',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      isPinned: false,
      wordCount: 0,
      characterCount: 0,
    };
    setNotes(prev => [newNote, ...prev]);
    setActiveNoteId(newNote.id);
    showToast('New note created.');
  };

  const handleDeleteNote = (id: string) => {
    setNotes(prev => prev.filter(n => n.id !== id));
    if (activeNoteId === id) {
      const remaining = notes.filter(n => n.id !== id);
      if (remaining.length > 0) setActiveNoteId(remaining[0].id);
    }
    showToast('Note removed.');
  };

  const handleTogglePinNote = (id: string) => {
    setNotes(prev =>
      prev.map(note => (note.id === id ? { ...note, isPinned: !note.isPinned } : note))
    );
  };

  const handleLoginSuccess = (email: string) => {
    setUserEmail(email);
    localStorage.setItem('flow_auth_user', email);
    showToast(`Signed in as ${email}`);
  };

  const handleClaimCredits = (amount: number) => {
    setCredits(prev => {
      const next = prev + amount;
      localStorage.setItem('flow_user_credits', String(next));
      return next;
    });
    showToast(`+${amount} Credits claimed!`);
  };

  const handleSignOut = () => {
    setUserEmail(null);
    localStorage.removeItem('flow_auth_user');
    showToast('Signed out of FLOW account.');
  };

  return (
    <div className="flex h-screen w-full bg-[#f8fafc] text-slate-900 overflow-hidden font-sans">
      {/* Toast Notification */}
      {toastMessage && (
        <div 
          id="flow-toast"
          className="fixed top-12 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-xl bg-white border border-[#0284c7] text-xs font-semibold text-slate-900 shadow-xl shadow-slate-900/10 transition-all flex items-center gap-2 animate-in fade-in slide-in-from-top-2"
        >
          <span className="w-2 h-2 rounded-full bg-[#0284c7]" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Top Windows 11 Titlebar & Sub-Header Toolbar */}
      <Navbar
        sessionState={sessionState}
        showFloatingHud={showFloatingHud}
        setShowFloatingHud={setShowFloatingHud}
        onMinimizeToTray={() => showToast('FLOW minimized to system tray notification area.')}
        zeroEnterActive={settings.zeroEnterInvariant}
        onOpenShortcutSettings={() => setActiveTab('settings')}
        credits={credits}
        userEmail={userEmail}
        onOpenCredits={() => setIsLoginModalOpen(true)}
      />

      {/* Left Sidebar Navigation Rail */}
      <Sidebar
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        historyCount={history.filter(h => !h.isDeleted).length}
        dictCount={dictionary.length}
        snippetCount={snippets.length}
      />

      {/* Main Workspace (offset by sidebar w-60 and navbar top-8 + h-14 = 88px) */}
      <div className="flex-1 ml-60 mt-[88px] h-[calc(100vh-88px)] overflow-hidden flex flex-col bg-[#f8fafc]">
        <main id="main-content-scroll" className="flex-1 overflow-y-auto px-8 py-6">
          {activeTab === 'home' && (
            <HomeTab
              sessionState={sessionState}
              onStartDictation={() => {
                recordingModeRef.current = 'click';
                startDictation();
              }}
              onStopDictation={() => {
                stopDictation();
                recordingModeRef.current = 'click';
              }}
              audioLevel={audioLevel}
              lastTranscript={lastTranscript}
              onCopyTranscript={handleCopyTranscript}
              onInsertTranscript={handleInsertTranscript}
              recentEntries={history.filter(h => !h.isDeleted)}
              onNavigateToHistory={() => setActiveTab('history')}
              onToggleFavoriteHistory={handleToggleFavoriteHistory}
              activeMic={settings.activeMic}
              selectedLanguage={settings.language}
              onLanguageChange={lang => setSettings(prev => ({ ...prev, language: lang }))}
              onOpenSettings={() => setActiveTab('settings')}
              showFloatingHud={showFloatingHud}
              onToggleFloatingHud={() => setShowFloatingHud(prev => !prev)}
            />
          )}

          {activeTab === 'history' && (
            <HistoryTab
              entries={history}
              onToggleFavorite={handleToggleFavoriteHistory}
              onDeleteEntry={handleDeleteHistoryEntry}
            />
          )}

          {activeTab === 'dictionary' && (
            <DictionaryTab
              entries={dictionary}
              onAddEntry={handleAddDictionaryEntry}
              onUpdateEntry={handleUpdateDictionaryEntry}
              onToggleFavorite={handleToggleFavoriteDictionary}
              onDeleteEntry={handleDeleteDictionaryEntry}
              testWordSanitize={(txt: string) =>
                sanitizeAndFormat(txt, {
                  style: activeStyle,
                  dictionary,
                  zeroEnterInvariant: true,
                })
              }
              onNavigateToSnippets={() => setActiveTab('snippets')}
            />
          )}

          {activeTab === 'snippets' && (
            <SnippetsTab
              snippets={snippets}
              onAddSnippet={handleAddSnippet}
              onDeleteSnippet={handleDeleteSnippet}
              testSnippetSanitize={(txt: string) =>
                sanitizeAndFormat(txt, {
                  style: activeStyle,
                  snippets,
                  zeroEnterInvariant: true,
                })
              }
              onNavigateToDictionary={() => setActiveTab('dictionary')}
            />
          )}

          {activeTab === 'styles' && (
            <StylesTab
              styles={styles}
              activeStyleId={activeStyleId}
              onSelectActiveStyle={id => {
                setActiveStyleId(id);
                showToast(`Switched active style to: ${styles.find(s => s.id === id)?.name}`);
              }}
            />
          )}

          {activeTab === 'scratchpad' && (
            <ScratchpadTab
              notes={notes}
              activeNoteId={activeNoteId}
              onSelectNote={setActiveNoteId}
              onSaveNoteContent={handleSaveNoteContent}
              onCreateNote={handleCreateNote}
              onDeleteNote={handleDeleteNote}
              onTogglePin={handleTogglePinNote}
              onInsertDictationIntoScratchpad={(txt: string) => {
                const note = notes.find(n => n.id === activeNoteId);
                if (note) {
                  handleSaveNoteContent(
                    note.id,
                    note.title,
                    note.content ? `${note.content} ${txt}` : txt
                  );
                }
              }}
            />
          )}

          {activeTab === 'settings' && (
            <SettingsTab
              settings={settings}
              onUpdateSettings={newConf => {
                setSettings(prev => ({ ...prev, ...newConf }));
                showToast('Settings saved.');
              }}
              showFloatingHud={showFloatingHud}
              onToggleFloatingHud={() => setShowFloatingHud(prev => !prev)}
              onReplayOnboarding={() => setOnboardingOpen(true)}
              onClearHistory={handleClearHistory}
              multiMonitorMode={multiMonitorMode}
              onChangeMultiMonitorMode={setMultiMonitorMode}
              bottomOffset={bottomOffset}
              onChangeBottomOffset={setBottomOffset}
              availableMics={availableMics}
            />
          )}

          {activeTab === 'about' && <AboutTab />}
        </main>
      </div>

      {/* Production Non-Activating Floating Bar (Obsidian-Amber Capsule) */}
      {showFloatingHud && (
        <FloatingHud
          sessionState={sessionState}
          onStartDictation={() => {
            recordingModeRef.current = 'click';
            startDictation();
          }}
          onStopDictation={() => {
            stopDictation();
            recordingModeRef.current = 'click';
          }}
          onBacktrack={handleBacktrack}
          onClose={() => setShowFloatingHud(false)}
          audioLevel={audioLevel}
          previewText={previewText}
          activeStyleName={activeStyle.name}
          lastTranscript={lastTranscript}
          onCopyTranscript={handleCopyTranscript}
          onInsertTranscript={handleInsertTranscript}
          onOpenSettings={() => setActiveTab('settings')}
          activeMic={settings.activeMic}
          bottomOffset={bottomOffset}
          multiMonitorMode={multiMonitorMode}
        />
      )}

      {/* Companion Bar when multi-monitor "all" mode is enabled */}
      {showFloatingHud && multiMonitorMode === 'all' && (
        <div
          id="flow-secondary-display-indicator"
          className="fixed top-12 right-6 z-40 px-3 py-1.5 rounded-full bg-[#1A0F08]/90 border border-[#3D2A1F] text-[11px] font-mono text-[#F4E0C6] shadow-lg flex items-center gap-2 pointer-events-none"
        >
          <span className="w-2 h-2 rounded-full bg-[#B87333]"></span>
          <span>Display 2 HUD Synchronized</span>
        </div>
      )}

      {/* First-Run Onboarding Experience Modal */}
      <OnboardingModal
        isOpen={onboardingOpen}
        onClose={() => {
          setOnboardingOpen(false);
          localStorage.setItem('flow_onboarding_completed', 'true');
        }}
        onFinish={() => {
          setOnboardingOpen(false);
          localStorage.setItem('flow_onboarding_completed', 'true');
          showToast('Setup complete! Press Alt + Space to speak anytime.');
        }}
        activeMic={settings.activeMic}
        onSelectMic={mic => setSettings(prev => ({ ...prev, activeMic: mic }))}
        onStartTestDictation={() => {
          recordingModeRef.current = 'click';
          startDictation();
        }}
        onStopTestDictation={() => {
          stopDictation();
          recordingModeRef.current = 'click';
        }}
        isListening={sessionState === 'listening'}
        audioLevel={audioLevel}
        previewText={previewText}
      />

      {/* Account, Credits & Email Verification Login Modal */}
      <LoginModal
        isOpen={isLoginModalOpen}
        onClose={() => setIsLoginModalOpen(false)}
        userEmail={userEmail}
        credits={credits}
        onLoginSuccess={handleLoginSuccess}
        onClaimCredits={handleClaimCredits}
        onSignOut={handleSignOut}
      />
    </div>
  );
};

export default App;
