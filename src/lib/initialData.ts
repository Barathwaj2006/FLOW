import { DictationEntry, DictionaryEntry, SnippetEntry, StyleProfile, ScratchpadEntry, FlowSettings } from '../types';

export const INITIAL_STYLES: StyleProfile[] = [
  {
    id: 'style-balanced',
    name: 'Natural / Balanced',
    description: 'Removes filler words (um, uh) and formats punctuation cleanly while preserving your natural tone.',
    formalityLevel: 'balanced',
    contractionPolicy: 'preserve',
    useBulletPoints: false,
    isEnabled: true,
    appMappings: ['Default', 'Notion', 'Obsidian', 'Chrome'],
  },
  {
    id: 'style-formal',
    name: 'Formal / Professional',
    description: 'Expands contractions, elevates sentence structure, and ensures professional cadence for business emails.',
    formalityLevel: 'formal',
    contractionPolicy: 'expand',
    useBulletPoints: false,
    isEnabled: true,
    appMappings: ['Outlook', 'Microsoft Word', 'Teams'],
  },
  {
    id: 'style-casual',
    name: 'Casual / Conversational',
    description: 'Allows contractions, friendly phrasing, and relaxed punctuation for team chats and messaging.',
    formalityLevel: 'casual',
    contractionPolicy: 'contract',
    useBulletPoints: false,
    isEnabled: true,
    appMappings: ['Slack', 'Discord', 'WhatsApp'],
  },
  {
    id: 'style-concise',
    name: 'Concise / Bulleted',
    description: 'Summarizes key thoughts into tight, actionable items ideal for issue trackers and task lists.',
    formalityLevel: 'balanced',
    contractionPolicy: 'contract',
    useBulletPoints: true,
    isEnabled: true,
    appMappings: ['Jira', 'Linear', 'GitHub'],
  },
  {
    id: 'style-developer',
    name: 'Developer / Coding Mode',
    description: 'Enables spoken casing commands (camel case, snake case, pascal case) and preserves code identifiers & file paths.',
    formalityLevel: 'balanced',
    contractionPolicy: 'preserve',
    useBulletPoints: false,
    isDeveloperMode: true,
    isEnabled: true,
    appMappings: ['VS Code', 'Visual Studio', 'Windows Terminal', 'Cursor'],
  },
];

export const INITIAL_DICTIONARY: DictionaryEntry[] = [
  {
    id: 'dict-1',
    term: 'Kubernetes',
    phonetic: '/kjuːbərˈnɛtiːz/',
    matchesHint: "Matches: 'cooberneties', 'cube nettes'",
    replacement: 'k8s',
    category: 'Tech / Cloud',
    tag: 'Literal',
    isCorrection: true,
    isFavorite: true,
    createdAt: '2026-09-10T10:00:00Z',
  },
  {
    id: 'dict-2',
    term: 'Wispr',
    phonetic: '/wɪspər/',
    matchesHint: 'Context: dictation app references',
    replacement: 'Wispr Flow',
    category: 'Product',
    tag: 'Brand Capital',
    isCorrection: true,
    isFavorite: true,
    createdAt: '2026-09-10T10:05:00Z',
  },
  {
    id: 'dict-3',
    term: 'Dr. Aris Thorne',
    phonetic: '/ɛərɪs θɔːrn/',
    matchesHint: "Matches: 'Doctor Aris Thorn', 'Aeris'",
    replacement: 'Dr. Aris Thorne, MD',
    category: 'Medical / Name',
    tag: 'Title Expansion',
    isCorrection: true,
    isFavorite: true,
    createdAt: '2026-09-11T12:00:00Z',
  },
  {
    id: 'dict-4',
    term: 'GraphQL API',
    phonetic: '/græf kjuː ɛl/',
    matchesHint: "Matches: 'graph q l', 'graph cue ell'",
    replacement: 'GraphQL',
    category: 'Tech',
    tag: 'Exact Case',
    isCorrection: true,
    isFavorite: false,
    createdAt: '2026-09-11T14:30:00Z',
  },
  {
    id: 'dict-5',
    term: 'Antigravity',
    phonetic: '/æn.tiˈɡræv.ə.ti/',
    matchesHint: 'Product library reference',
    replacement: 'Antigravity UI',
    category: 'Design System',
    tag: 'Product',
    isCorrection: true,
    isFavorite: false,
    createdAt: '2026-09-12T09:15:00Z',
  },
];

export const INITIAL_SNIPPETS: SnippetEntry[] = [
  {
    id: 'snip-1',
    trigger: 'meeting link',
    expansion: 'https://meet.google.com/abc-defg-hij',
    createdAt: '2026-09-10T08:00:00Z',
  },
  {
    id: 'snip-2',
    trigger: 'my intro',
    expansion: 'Hi, I am working on system-wide voice productivity for Windows with local-first inference.',
    createdAt: '2026-09-10T08:05:00Z',
  },
  {
    id: 'snip-3',
    trigger: 'bug report template',
    expansion: 'Environment: Windows 11 x64. Steps: 1. Hold Right Alt. 2. Speak. Result: ..., Expected: ...',
    createdAt: '2026-09-11T11:20:00Z',
  },
  {
    id: 'snip-4',
    trigger: 'sign off',
    expansion: 'Best regards, Barathwaj',
    createdAt: '2026-09-11T15:00:00Z',
  },
];

export const INITIAL_HISTORY: DictationEntry[] = [
  {
    id: 'hist-user-1',
    text: "I want to make these components a little smaller and make the flow bar glassomorphic. It should be a glass-like structure, not a solid structure, and enhance the structure, its hapticity, and all those things. I also want to implement wave-like structures when the mic is on and when I'm trying to speak, and remove the timing that shows how many minutes the speaker has used. This timing is running while speaking, right? Remove those things.",
    rawText: "i want to make these components a little smaller and make the flow bar glassomorphic it should be a glass-like structure not a solid structure and enhance the structure its hapticity and all those things i also want to implement wave-like structures when the mic is on and when i'm trying to speak and remove the timing that shows how many minutes the speaker has used this timing is running while speaking right remove those things",
    application: 'Google Chrome',
    appName: 'Chrome',
    styleApplied: 'Balanced',
    durationMs: 14800,
    durationSeconds: 14.8,
    wordCount: 71,
    latencyMs: 168,
    zeroEnterGuaranteed: true,
    isFavorite: false,
    timestamp: '2026-09-13T16:31:00Z',
    timeShort: '4:31 pm',
  },
  {
    id: 'hist-user-2',
    text: "yeah, I don't feel my application is good enough because the voice-to-speech conversion that I am using now is done using Wispr Flow. The prompt I am generating for you now is also done using Wispr Flow, and I want you to know how good this prompt is. Before that, you have to generate me a very good prompt which is used in anti-gravity to build a production grade Windows native voice productivity platform with local-first Whisper inference and strict zero-enter invariant.",
    rawText: "yeah i dont feel my application is good enough because the voice to speech conversion that i am using now is done using wispr flow the prompt i am generating for you now is also done using wispr flow and i want you to know how good this prompt is before that you have to generate me a very good prompt which is used in anti-gravity to build a production grade windows native voice productivity platform with local-first whisper inference and strict zero-enter invariant",
    application: 'Visual Studio Code',
    appName: 'VS Code',
    styleApplied: 'Developer',
    durationMs: 22400,
    durationSeconds: 22.4,
    wordCount: 84,
    latencyMs: 182,
    zeroEnterGuaranteed: true,
    isFavorite: true,
    timestamp: '2026-09-13T14:40:00Z',
    timeShort: '2:40 pm',
  },
];

export const INITIAL_SCRATCHPAD: ScratchpadEntry[] = [
  {
    id: 'note-1',
    title: 'FLOW Windows Engineering Architecture',
    content: `# FLOW — AI Voice Productivity for Windows

## Core Architecture Principles
1. **100% Offline Sovereignty**: Audio captured strictly on-device using local Whisper weights.
2. **Inviolable Zero-Enter Invariant**: Never simulates Enter or form submissions.
3. **Non-Activating Floating Flow Bar**: Win32 window with WS_EX_NOACTIVATE | WS_EX_TOPMOST.
4. **Target Application Support**: Seamless dictation into VS Code, Chrome, Slack, Notion, Word.`,
    createdAt: '2026-09-12T10:00:00Z',
    updatedAt: '2026-09-13T07:00:00Z',
    isPinned: true,
    wordCount: 54,
    characterCount: 420,
  },
  {
    id: 'note-2',
    title: 'Keyboard Shortcuts Reference',
    content: `- Hold [Right Alt]: Push-to-Talk dictation
- Double-Tap [Right Alt]: Hands-free continuous dictation toggle
- [Shift + Right Alt]: Backtrack Undo previous insertion
- [Escape]: Cancel active recording without inserting text`,
    createdAt: '2026-09-12T11:30:00Z',
    updatedAt: '2026-09-12T11:45:00Z',
    isPinned: false,
    wordCount: 30,
    characterCount: 220,
  },
];

export const INITIAL_SETTINGS: FlowSettings = {
  activeMic: 'Default Microphone (WASAPI)',
  sensitivity: 75,
  hotkey: 'Alt + Space',
  handsFreeHotkey: 'Alt + B',
  backtrackHotkey: 'Shift + Alt',
  language: 'Auto-Detect',
  zeroEnterInvariant: true,
  retentionPolicy: 'unlimited',
  activeStyleId: 'style-balanced',
  soundFeedback: true,
};
