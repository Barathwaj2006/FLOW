import Foundation

/// Phase 1 Deterministic Text Sanitizer.
/// Applies only provably safe, rule-based formatting:
/// - Strips excess whitespace
/// - Capitalizes the first letter of sentences
/// - Adds a terminal period only if the utterance forms a complete sentence without trailing punctuation
///
/// Inviolable rule: Strictly NO speculative AI rewrites, hallucinated words, or intent mutations.
public enum DeterministicTextSanitizer {
    
    /// Sanitizes raw ASR transcript deterministically and safely.
    /// - Parameters:
    ///   - raw: Raw text output from ASR engine.
    ///   - ensureTerminalPunctuation: Whether to add a terminal period if missing (default: true).
    /// - Returns: Safely formatted text string ready for cursor insertion.
    public static func sanitize(_ raw: String, ensureTerminalPunctuation: Bool = true) -> String {
        // 1. Strip leading and trailing whitespace and newlines
        var text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return "" }
        
        // 2. Collapse internal repeated whitespace (e.g. "word    word" -> "word word")
        let components = text.components(separatedBy: .whitespaces).filter { !$0.isEmpty }
        text = components.joined(separator: " ")
        
        // 3. Capitalize the first character of the utterance
        if let firstChar = text.first, firstChar.isLetter {
            text = firstChar.uppercased() + text.dropFirst()
        }
        
        // 4. Safe Terminal Punctuation:
        // Only append period if text does not already terminate with punctuation
        // and contains multiple tokens (avoids punctuating single isolated words like "yes")
        if ensureTerminalPunctuation && components.count > 1 {
            let trailingPunctuation: Set<Character> = [".", "!", "?", ":", ";", ",", "—", "-"]
            if let lastChar = text.last, !trailingPunctuation.contains(lastChar) {
                text.append(".")
            }
        }
        
        return text
    }
}
