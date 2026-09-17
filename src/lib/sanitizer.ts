import { DictionaryEntry, SnippetEntry, StyleProfile } from '../types';

interface SanitizerOptions {
  removeFillerWords?: boolean;
  replaceSpokenPunctuation?: boolean;
  capitalizeFirstWord?: boolean;
  ensureTerminalPunctuation?: boolean;
  applyCasingCommands?: boolean;
  zeroEnterInvariant?: boolean;
  dictionary?: DictionaryEntry[];
  snippets?: SnippetEntry[];
  style?: StyleProfile;
}

const FILLER_WORDS_REGEX = /\b(um|uh|erm|ah|hmm)\b/gi;
const MULTIPLE_SPACES_REGEX = /[ \t]+/g;

// Spoken punctuation patterns matching DeterministicTextSanitizer.cs
const SPOKEN_PUNCTUATION: [RegExp, string][] = [
  [/\s+\bperiod\b/gi, '.'],
  [/\s+\bfull stop\b/gi, '.'],
  [/\s+\bcomma\b/gi, ','],
  [/\s+\bquestion mark\b/gi, '?'],
  [/\s+\bexclamation point\b/gi, '!'],
  [/\s+\bexclamation mark\b/gi, '!'],
  [/\s+\bcolon\b/gi, ':'],
  [/\s+\bsemicolon\b/gi, ';'],
  // CRITICAL: Inviolable Zero-Enter Invariant! "new line" / "new paragraph" maps to space
  [/\s+\bnew line\b/gi, ' '],
  [/\s+\bnew paragraph\b/gi, ' '],
];

// Spoken casing regexes
const CAMEL_CASE_REGEX = /\bcamel case\s+([a-zA-Z0-9\s]+?)(?=\s+(?:period|comma|question|exclamation|$|\.|\,))/gi;
const PASCAL_CASE_REGEX = /\bpascal case\s+([a-zA-Z0-9\s]+?)(?=\s+(?:period|comma|question|exclamation|$|\.|\,))/gi;
const SNAKE_CASE_REGEX = /\bsnake case\s+([a-zA-Z0-9\s]+?)(?=\s+(?:period|comma|question|exclamation|$|\.|\,))/gi;
const KEBAB_CASE_REGEX = /\bkebab case\s+([a-zA-Z0-9\s]+?)(?=\s+(?:period|comma|question|exclamation|$|\.|\,))/gi;
const ALL_CAPS_REGEX = /\ball caps\s+([a-zA-Z0-9\s]+?)(?=\s+(?:period|comma|question|exclamation|$|\.|\,))/gi;

function toWords(input: string): string[] {
  return input.trim().split(/\s+/).filter(Boolean);
}

function toCamelCase(input: string): string {
  const words = toWords(input);
  if (words.length === 0) return '';
  return words
    .map((w, idx) => {
      const lower = w.toLowerCase();
      return idx === 0 ? lower : lower.charAt(0).toUpperCase() + lower.slice(1);
    })
    .join('');
}

function toPascalCase(input: string): string {
  const words = toWords(input);
  return words
    .map(w => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
    .join('');
}

function toSnakeCase(input: string): string {
  const words = toWords(input);
  return words.map(w => w.toLowerCase()).join('_');
}

function toKebabCase(input: string): string {
  const words = toWords(input);
  return words.map(w => w.toLowerCase()).join('-');
}

function toAllCaps(input: string): string {
  return input.toUpperCase();
}

export function sanitizeAndFormat(rawText: string, options: SanitizerOptions = {}): string {
  if (!rawText || !rawText.trim()) {
    return '';
  }

  const {
    removeFillerWords = true,
    replaceSpokenPunctuation = true,
    capitalizeFirstWord = true,
    ensureTerminalPunctuation = true,
    applyCasingCommands = true,
    dictionary = [],
    snippets = [],
    style,
  } = options;

  // 1. INVIOLABLE SAFETY: Strip all newlines and carriage returns to prevent enter simulation
  let text = rawText.replace(/\r/g, ' ').replace(/\n/g, ' ');

  // 2. Expand Snippets if trigger matches
  for (const snip of snippets) {
    if (snip.trigger && snip.expansion) {
      const snipRegex = new RegExp(`\\b${escapeRegExp(snip.trigger)}\\b`, 'gi');
      // Protect Zero-Enter invariant in expansions as well
      const safeExpansion = snip.expansion.replace(/[\r\n]+/g, ' ');
      text = text.replace(snipRegex, safeExpansion);
    }
  }

  // 3. Spoken Casing Transformations (Developer Mode / Coding Commands)
  if (applyCasingCommands) {
    text = text.replace(CAMEL_CASE_REGEX, (_, match) => toCamelCase(match));
    text = text.replace(PASCAL_CASE_REGEX, (_, match) => toPascalCase(match));
    text = text.replace(SNAKE_CASE_REGEX, (_, match) => toSnakeCase(match));
    text = text.replace(KEBAB_CASE_REGEX, (_, match) => toKebabCase(match));
    text = text.replace(ALL_CAPS_REGEX, (_, match) => toAllCaps(match));
  }

  // 4. Personal Dictionary substitutions & corrections
  for (const entry of dictionary) {
    if (entry.isCorrection && entry.replacement) {
      const termRegex = new RegExp(`\\b${escapeRegExp(entry.term)}\\b`, 'gi');
      text = text.replace(termRegex, entry.replacement);
    }
  }

  // 5. Remove filler words if enabled
  if (removeFillerWords) {
    text = text.replace(FILLER_WORDS_REGEX, '');
  }

  // 6. Spoken punctuation substitution
  if (replaceSpokenPunctuation) {
    for (const [pattern, replacement] of SPOKEN_PUNCTUATION) {
      text = text.replace(pattern, replacement);
    }
  }

  // 7. Normalize spaces
  text = text.replace(MULTIPLE_SPACES_REGEX, ' ').trim();

  // Fix spaces before punctuation (e.g., "hello ." -> "hello.")
  text = text
    .replace(/\s+\./g, '.')
    .replace(/\s+,/g, ',')
    .replace(/\s+\?/g, '?')
    .replace(/\s+!/g, '!')
    .replace(/\s+:/g, ':')
    .replace(/\s+;/g, ';');

  if (!text) return '';

  // 8. Style profile adaptations
  if (style) {
    if (style.contractionPolicy === 'expand') {
      text = expandContractions(text);
    } else if (style.contractionPolicy === 'contract') {
      text = contractPhrases(text);
    }

    if (style.formalityLevel === 'formal') {
      // Capitalize formal acronyms and soften casual slang
      text = text.replace(/\bgonna\b/gi, 'going to')
                 .replace(/\bwanna\b/gi, 'want to')
                 .replace(/\bgotta\b/gi, 'have to')
                 .replace(/\bya\b/gi, 'you');
    }
  }

  // 9. Initial and sentence boundary capitalization
  if (capitalizeFirstWord && text.length > 0) {
    text = text.charAt(0).toUpperCase() + text.slice(1);
    text = text.replace(/(?<=[.!?]\s+)([a-z])/g, m => m.toUpperCase());
  }

  // 10. Terminal punctuation check
  if (ensureTerminalPunctuation && text.length > 0) {
    const lastChar = text.charAt(text.length - 1);
    if (!isTerminalPunctuation(lastChar)) {
      text += '.';
    }
  }

  // 11. INVIOLABLE ZERO-ENTER INVARIANT ENFORCEMENT
  // Absolutely no \r, \n, VK_RETURN simulation or line breaks in output
  if (options.zeroEnterInvariant !== false) {
    text = text.replace(/[\r\n\x0B\x0C\x85\u2028\u2029]+/g, ' ').replace(MULTIPLE_SPACES_REGEX, ' ').trim();
  }

  return text;
}

function escapeRegExp(str: string): string {
  return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function isTerminalPunctuation(char: string): boolean {
  return ['.', '!', '?', ':', ';', '"', "'", '`'].includes(char);
}

function expandContractions(text: string): string {
  return text
    .replace(/\bdon't\b/gi, 'do not')
    .replace(/\bdoesn't\b/gi, 'does not')
    .replace(/\bdidn't\b/gi, 'did not')
    .replace(/\bcan't\b/gi, 'cannot')
    .replace(/\bwon't\b/gi, 'will not')
    .replace(/\bshouldn't\b/gi, 'should not')
    .replace(/\bwouldn't\b/gi, 'would not')
    .replace(/\bcouldn't\b/gi, 'could not')
    .replace(/\bisn't\b/gi, 'is not')
    .replace(/\baren't\b/gi, 'are not')
    .replace(/\bwasn't\b/gi, 'was not')
    .replace(/\bweren't\b/gi, 'were not')
    .replace(/\bI'm\b/g, 'I am')
    .replace(/\bwe're\b/gi, 'we are')
    .replace(/\bthey're\b/gi, 'they are')
    .replace(/\bit's\b/gi, 'it is');
}

function contractPhrases(text: string): string {
  return text
    .replace(/\bdo not\b/gi, "don't")
    .replace(/\bdoes not\b/gi, "doesn't")
    .replace(/\bcannot\b/gi, "can't")
    .replace(/\bwill not\b/gi, "won't")
    .replace(/\bshould not\b/gi, "shouldn't")
    .replace(/\bwould not\b/gi, "wouldn't")
    .replace(/\bcould not\b/gi, "couldn't")
    .replace(/\bis not\b/gi, "isn't")
    .replace(/\bare not\b/gi, "aren't")
    .replace(/\bI am\b/g, "I'm")
    .replace(/\bwe are\b/gi, "we're")
    .replace(/\bit is\b/gi, "it's");
}
