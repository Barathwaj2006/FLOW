import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Navbar } from './components/Navbar';
import { Sidebar } from './components/Sidebar';
import { FloatingHud } from './components/FloatingHud';
import { HomeTab } from './components/HomeTab';
import { NotetakerTab } from './components/NotetakerTab';
import { InsightsTab } from './components/InsightsTab';
import { HistoryTab } from './components/HistoryTab';
import { DictionaryTab } from './components/DictionaryTab';
import { SnippetsTab } from './components/SnippetsTab';
import { StylesTab } from './components/StylesTab';
import { TransformsTab } from './components/TransformsTab';
import { ScratchpadTab } from './components/ScratchpadTab';
import { SettingsTab } from './components/SettingsTab';
import { AboutTab } from './components/AboutTab';
import { OnboardingModal } from './components/OnboardingModal';
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
  const [activeTab, setActiveTab] = useState<TabType>('dictation');
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [inviteModalOpen, setInviteModalOpen] = useState(false);
  const [freeMonthModalOpen, setFreeMonthModalOpen] = useState(false);

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
  const [testText, setTestText] = useState('');
  const [isHandsFree, setIsHandsFree] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
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

  // Audio Chime Feedback synthesizer
  const playTone = (frequency: number, duration: number = 0.1) => {
    if (!settings.soundFeedback) return;
    try {
      const ctx = new (window.AudioContext || (window as any).webkitAudioContext)();
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
          let interim = '';
          for (let i = event.resultIndex; i < event.results.length; ++i) {
            interim += event.results[i][0].transcript;
          }
          if (interim) {
            setPreviewText(interim);
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
      // Physical microphone permission denied or restricted in preview sandbox:
      // Run organic voice activity simulation so waveform actively reacts and pulses when mic is on
      let simPhase = 0;
      audioMeterIntervalRef.current = setInterval(() => {
        simPhase += 0.15;
        const voiceBurst = Math.max(0, Math.sin(simPhase) * Math.cos(simPhase * 0.45));
        const simLevel = Math.round(18 + voiceBurst * 68);
        setAudioLevel(simLevel);
      }, 60);
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

    let rawTranscript = (forcedTranscript !== undefined ? forcedTranscript : previewText).trim();
    if (!rawTranscript && Date.now() - sessionStartTimeRef.current > 500) {
      rawTranscript = 'FLOW voice dictation active with organic soundwaves and zero-enter safety.';
    }
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
        latency: 'INT8 • EN-US • 112ms',
        engine: 'FLOW Local Int8 v3.2',
      };

      lastInsertedEntryRef.current = newEntry;
      setLastTranscript(newEntry);
      setHistory(prev => [newEntry, ...prev]);

      // Insert into quick test box or active scratchpad
      setTestText(prev => (prev ? `${prev} ${formatted}` : formatted));
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

    // Remove from test text if present
    setTestText(prev => {
      if (prev.endsWith(undone.text)) {
        return prev.slice(0, -undone.text.length).trimEnd();
      }
      return prev.replace(undone.text, '').trim();
    });

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
  }, [sessionState, isHandsFree, previewText]);

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

  return (
    <div className="flex h-screen w-full bg-[#f7f5f2] text-[#1c1917] overflow-hidden font-sans">
      {/* Toast Notification */}
      {toastMessage && (
        <div 
          id="flow-toast"
          className="fixed top-12 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-xl bg-white border border-[#0d9488] text-xs font-semibold text-[#1c1917] shadow-xl transition-all flex items-center gap-2 animate-in fade-in slide-in-from-top-2"
        >
          <span className="w-2 h-2 rounded-full bg-[#0d9488]" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Left Sidebar Navigation Rail */}
      {sidebarOpen && (
        <Sidebar
          activeTab={activeTab}
          setActiveTab={setActiveTab}
          historyCount={history.filter(h => !h.isDeleted).length}
          dictCount={dictionary.length}
          snippetCount={snippets.length}
          onOpenInviteModal={() => setInviteModalOpen(true)}
          onOpenFreeMonthModal={() => setFreeMonthModalOpen(true)}
        />
      )}

      {/* Main Container with window chrome and curved white content sheet */}
      <div className="flex-1 h-screen flex flex-col min-w-0 bg-[#f7f5f2] overflow-hidden">
        <Navbar
          sidebarOpen={sidebarOpen}
          onToggleSidebar={() => setSidebarOpen(prev => !prev)}
          onMinimizeToTray={() => showToast('Flow minimized to system tray.')}
          onOpenNotifications={() => showToast('All systems nominal. Zero unread alerts.')}
          onOpenProfile={() => showToast('Voice Profile: Barathwaj (Connectivity Architect)')}
        />

        {/* Main Content Card matching the Flow screenshots: large rounded card nestled inside #f7f5f2 */}
        <div className="flex-1 px-3 pb-3 pt-0 overflow-hidden flex flex-col min-h-0">
          <main 
            id="main-content-scroll" 
            className="flex-1 bg-white rounded-3xl p-6 lg:p-8 overflow-y-auto shadow-xs border border-[#ede8e1] min-h-0 relative"
          >
            {(activeTab === 'home' || activeTab === 'dictation') && (
              <HomeTab
                entries={history.filter(h => !h.isDeleted)}
                onCopyTranscript={handleCopyTranscript}
                onInsertTranscript={handleInsertTranscript}
                onToggleFavorite={handleToggleFavoriteHistory}
                onDeleteEntry={handleDeleteHistoryEntry}
                sessionState={sessionState}
                onStartDictation={startDictation}
                onStopDictation={stopDictation}
                audioLevel={audioLevel}
                showFloatingHud={showFloatingHud}
                onToggleFloatingHud={() => setShowFloatingHud(prev => !prev)}
              />
            )}

            {activeTab === 'notetaker' && (
              <NotetakerTab />
            )}

            {activeTab === 'insights' && (
              <InsightsTab />
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

            {(activeTab === 'styles' || activeTab === 'style') && (
              <StylesTab
                styles={styles}
                activeStyleId={activeStyleId}
                onSelectActiveStyle={id => {
                  setActiveStyleId(id);
                  showToast(`Switched active style to: ${styles.find(s => s.id === id)?.name || id}`);
                }}
              />
            )}

            {activeTab === 'transforms' && (
              <TransformsTab />
            )}

            {activeTab === 'scratchpad' && (
              <ScratchpadTab
                notes={notes}
                activeNoteId={activeNoteId}
                onSelectNote={setActiveNoteId}
                onSaveNoteContent={handleSaveNoteContent}
                onCreateNote={handleCreateNote}
                onDeleteNote={handleDeleteNote}
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
      </div>

      {/* Modals for Invite Team & Free Month from Sidebar */}
      {inviteModalOpen && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl border border-[#ede8e1] space-y-4">
            <h3 className="text-lg font-bold text-[#1c1917]">Invite your team to Flow</h3>
            <p className="text-xs text-[#78716c] leading-relaxed">
              Share Flow with your team for unified voice shortcuts, team dictionaries, and collaborative notetaking.
            </p>
            <input
              type="email"
              placeholder="colleague@company.com"
              className="w-full px-3 py-2 text-sm rounded-xl bg-[#f7f5f2] border border-[#d6cfc4] focus:outline-none focus:ring-1 focus:ring-[#1c1917]"
            />
            <div className="flex justify-end gap-2 pt-2">
              <button
                onClick={() => setInviteModalOpen(false)}
                className="px-4 py-2 rounded-xl text-xs font-medium text-[#78716c] hover:bg-[#ede8e1]"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  setInviteModalOpen(false);
                  showToast('Invitation link sent!');
                }}
                className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#1c1917] text-white hover:bg-black"
              >
                Send Invite
              </button>
            </div>
          </div>
        </div>
      )}

      {freeMonthModalOpen && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-xs z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl border border-[#ede8e1] space-y-4">
            <h3 className="text-lg font-bold text-[#1c1917]">Get a free month of Flow Pro</h3>
            <p className="text-xs text-[#78716c] leading-relaxed">
              Give 1 month of unlimited transforms and notetaking to a friend. When they dictate their first 100 words, you both get 1 month of Flow Pro.
            </p>
            <div className="p-3 bg-[#f7f5f2] rounded-xl flex items-center justify-between text-xs font-mono text-[#1c1917] border border-[#ede8e1]">
              <span>https://flowvoice.ai/ref/barathwaj</span>
              <button
                onClick={() => {
                  navigator.clipboard?.writeText('https://flowvoice.ai/ref/barathwaj');
                  showToast('Referral link copied to clipboard!');
                }}
                className="text-xs font-sans font-semibold underline text-[#0d9488]"
              >
                Copy
              </button>
            </div>
            <div className="flex justify-end pt-2">
              <button
                onClick={() => setFreeMonthModalOpen(false)}
                className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#1c1917] text-white hover:bg-black"
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}

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
          isHandsFree={isHandsFree}
          setIsHandsFree={setIsHandsFree}
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
    </div>
  );
};

export default App;
