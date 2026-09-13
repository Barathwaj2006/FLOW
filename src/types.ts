export type TabType = 
  | 'home'
  | 'history'
  | 'dictionary'
  | 'snippets'
  | 'styles'
  | 'scratchpad'
  | 'hud'
  | 'settings'
  | 'about';

export type SessionState = 'idle' | 'listening' | 'processing' | 'inserted';

export interface DictationEntry {
  id: string;
  sessionId: string;
  createdAt: string;
  timeShort?: string;
  durationMs: number;
  durationText?: string;
  characterCount: number;
  wordCount: number;
  language: string;
  application: string;
  applicationCategory: string;
  target?: string;
  mode: string;
  state: 'Completed' | 'Cancelled' | 'Failed';
  isFavorite: boolean;
  text: string;
  isDeleted?: boolean;
  latency?: string;
  engine?: string;
}

export interface DictionaryEntry {
  id: string;
  term: string;
  phonetic?: string;
  matchesHint?: string;
  replacement?: string;
  category?: string;
  tag?: string;
  isCorrection: boolean;
  isFavorite: boolean;
  createdAt: string;
}

export interface SnippetEntry {
  id: string;
  trigger: string;
  expansion: string;
  createdAt: string;
}

export type ContractionPolicy = 'preserve' | 'expand' | 'contract';
export type FormalityLevel = 'casual' | 'balanced' | 'formal';

export interface StyleProfile {
  id: string;
  name: string;
  description: string;
  formalityLevel: FormalityLevel;
  contractionPolicy: ContractionPolicy;
  useBulletPoints: boolean;
  isDeveloperMode?: boolean;
  isEnabled: boolean;
  appMappings: string[];
}

export interface ScratchpadEntry {
  id: string;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  wordCount: number;
  characterCount: number;
}

export interface ProductivityMetrics {
  totalWords: number;
  totalSessions: number;
  averageWpm: number;
  currentStreak: number;
  wordsToday: number;
  sessionsToday: number;
}

export interface FlowSettings {
  activeMic: string;
  sensitivity: number; // 0-100
  hotkey: string;
  handsFreeHotkey: string;
  backtrackHotkey: string;
  language: string;
  zeroEnterInvariant: boolean;
  retentionPolicy: '30days' | '90days' | 'unlimited';
  activeStyleId: string;
  soundFeedback: boolean;
}
