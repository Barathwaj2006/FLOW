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
import { HudTab } from './components/HudTab';
import { SettingsTab } from './components/SettingsTab';
import { AboutTab } from './components/AboutTab';
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
  const [showFloatingHud, setShowFloatingHud] = useState(true);

  // Session & Dictation States
  const [sessionState, setSessionState] = useState<SessionState>('idle');
  const [audioLevel, setAudioLevel] = useState(0);
  const [previewText, setPreviewText] = useState('');
  const [testText, setTestText] = useState('');
  const [isHandsFree, setIsHandsFree] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

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

  // Track last inserted entry for Backtrack Undo
  const lastInsertedEntryRef = useRef<DictationEntry | null>(null);
  const recognitionRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const simulationIntervalRef = useRef<any>(null);
  const audioMeterIntervalRef = useRef<any>(null);
  const sessionStartTimeRef = useRef<number>(0);

  // Sync to localStorage
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

        recognition.onerror = () => {
          // Fall back gracefully
        };

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
      // Audio level simulation fallback if microphone permission not granted in iframe
      audioMeterIntervalRef.current = setInterval(() => {
        setAudioLevel(Math.floor(25 + Math.random() * 65));
      }, 90);
    }

    // If speech recognition didn't capture or isn't supported, provide realistic continuous preview
    if (!recognitionStarted) {
      const samplePhases = [
        'investigating',
        'investigating low latency',
        'investigating low latency wasapi audio capture',
        'investigating low latency wasapi audio capture on windows eleven',
      ];
      let step = 0;
      simulationIntervalRef.current = setInterval(() => {
        if (step < samplePhases.length) {
          setPreviewText(samplePhases[step]);
          step++;
        }
      }, 700);
    }
  };

  // Stop Dictation and finalize formatted insertion
  const stopDictation = (forcedTranscript?: string) => {
    if (sessionState !== 'listening') return;
    setSessionState('processing');
    playTone(440, 0.1); // A4 pitch

    // Clear intervals and streams
    if (simulationIntervalRef.current) {
      clearInterval(simulationIntervalRef.current);
      simulationIntervalRef.current = null;
    }
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

    const rawTranscript = forcedTranscript || previewText || 'Hey Jordan, could you make sure the pull request for the zero-latency audio engine gets merged into staging before our 3:30 sync? We already ran the Int8 inference benchmark tests locally.';
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
        application: activeStyle.isDeveloperMode ? 'Visual Studio Code' : 'Microsoft Teams',
        applicationCategory: activeStyle.isDeveloperMode ? 'Developer' : 'Communication',
        target: activeStyle.isDeveloperMode ? 'audio_buffer.cpp' : 'Direct Message',
        mode: activeStyle.name,
        state: 'Completed',
        isFavorite: false,
        text: formatted,
        latency: 'INT8 • EN-US • 112ms',
        engine: 'FLOW Local Int8 v3.2',
      };

      lastInsertedEntryRef.current = newEntry;
      setHistory(prev => [newEntry, ...prev]);

      // Insert into quick test box or active scratchpad
      setTestText(prev => (prev ? `${prev} ${formatted}` : formatted));
      setPreviewText(formatted);
      setSessionState('inserted');
      showToast('Inserted with Zero-Enter Protection ✓');

      setTimeout(() => {
        setSessionState('idle');
      }, 1500);
    }, 450);
  };

  // Simulate dictating a specific test phrase
  const handleSimulateDictation = (phrase: string) => {
    startDictation();
    setPreviewText(phrase);
    setTimeout(() => {
      stopDictation(phrase);
    }, 1200);
  };

  // Backtrack Undo: Revert the previous insertion
  const handleBacktrack = () => {
    if (!lastInsertedEntryRef.current) {
      showToast('No recent insertion to backtrack.');
      return;
    }
    const undone = lastInsertedEntryRef.current;
    lastInsertedEntryRef.current = null;

    // Remove from history
    setHistory(prev => prev.filter(e => e.id !== undone.id));

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

  // Global Hotkey Listener: Hold Alt (Right Alt) or Shift+Alt
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Shift + Alt = Backtrack
      if (e.altKey && e.shiftKey) {
        e.preventDefault();
        handleBacktrack();
        return;
      }
      // Alt key down = Start dictation (Push-to-Talk)
      if (e.key === 'Alt' && !e.repeat && sessionState === 'idle') {
        e.preventDefault();
        startDictation();
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      // Alt key release = Stop dictation if not in hands-free mode
      if (e.key === 'Alt' && sessionState === 'listening' && !isHandsFree) {
        e.preventDefault();
        stopDictation();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, [sessionState, isHandsFree, previewText]);

  // Productivity Metrics calculation
  const metrics: ProductivityMetrics = useMemo(() => {
    const totalWords = history.reduce((acc, h) => acc + (h.isDeleted ? 0 : h.wordCount), 0);
    const totalSessions = history.filter(h => !h.isDeleted).length;
    const wordsToday = history
      .filter(h => !h.isDeleted)
      .reduce((acc, h) => acc + h.wordCount, 0);
    const sessionsToday = history.filter(h => !h.isDeleted).length;

    return {
      totalWords,
      totalSessions,
      averageWpm: 162,
      currentStreak: 5,
      wordsToday: wordsToday || 320,
      sessionsToday: sessionsToday || 8,
    };
  }, [history]);

  // Handlers for History
  const handleToggleFavoriteHistory = (id: string) => {
    setHistory(prev =>
      prev.map(item => (item.id === id ? { ...item, isFavorite: !item.isFavorite } : item))
    );
  };

  const handleDeleteHistoryEntry = (id: string) => {
    setHistory(prev => prev.filter(item => item.id !== id));
    showToast('Record deleted.');
  };

  // Handlers for Dictionary
  const handleAddDictionaryEntry = (term: string, replacement?: string, category?: string) => {
    const newEntry: DictionaryEntry = {
      id: `dict-${Date.now()}`,
      term,
      replacement: replacement || term,
      category: category || 'Tech',
      isCorrection: false,
      isFavorite: false,
      createdAt: new Date().toISOString(),
      matchesHint: `Rule created for ${term}`,
      tag: 'Exact Case',
    };
    setDictionary(prev => [newEntry, ...prev]);
    showToast(`Added "${term}" to local dictionary.`);
  };

  const handleToggleFavoriteDictionary = (id: string) => {
    setDictionary(prev =>
      prev.map(item => (item.id === id ? { ...item, isFavorite: !item.isFavorite } : item))
    );
  };

  const handleDeleteDictionaryEntry = (id: string) => {
    setDictionary(prev => prev.filter(item => item.id !== id));
    showToast('Dictionary word removed.');
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
    <div className="flex h-screen w-full bg-[#f8fafc] text-slate-900 overflow-hidden font-sans">
      {/* Toast Notification */}
      {toastMessage && (
        <div 
          id="flow-toast"
          className="fixed top-12 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-lg bg-white border border-[#0284c7] text-xs font-semibold text-slate-900 shadow-xl shadow-slate-900/10 transition-all flex items-center gap-2 animate-in fade-in slide-in-from-top-2"
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
              onStartDictation={startDictation}
              onStopDictation={stopDictation}
              audioLevel={audioLevel}
              testText={testText}
              setTestText={setTestText}
              onSimulateDictation={handleSimulateDictation}
              metrics={metrics}
              recentEntries={history.filter(h => !h.isDeleted)}
              onNavigateToHistory={() => setActiveTab('history')}
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

          {activeTab === 'hud' && (
            <HudTab
              onStartDictation={startDictation}
              onStopDictation={stopDictation}
            />
          )}

          {activeTab === 'settings' && (
            <SettingsTab
              settings={settings}
              onUpdateSettings={newConf => {
                setSettings(prev => ({ ...prev, ...newConf }));
                showToast('Settings saved.');
              }}
            />
          )}

          {activeTab === 'about' && <AboutTab />}
        </main>
      </div>

      {/* Non-Activating Floating Flow Bar HUD */}
      {showFloatingHud && (
        <FloatingHud
          sessionState={sessionState}
          onStartDictation={startDictation}
          onStopDictation={stopDictation}
          onBacktrack={handleBacktrack}
          onClose={() => setShowFloatingHud(false)}
          audioLevel={audioLevel}
          previewText={previewText}
          isHandsFree={isHandsFree}
          setIsHandsFree={setIsHandsFree}
          activeStyleName={activeStyle.name}
        />
      )}
    </div>
  );
};

export default App;
