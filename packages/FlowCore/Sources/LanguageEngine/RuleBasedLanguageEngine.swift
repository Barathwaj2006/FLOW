import Foundation

/// Deterministic, zero-latency rule-based language cleaner for local-first execution.
public struct RuleBasedLanguageEngine: LanguageEngineProtocol, Sendable {
    
    public init() {}
    
    public func process(rawTranscript: String, options: LanguageCleanupOptions = .init()) -> String {
        var text = rawTranscript.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return "" }
        
        if options.resolveBacktracks {
            text = resolveBacktracks(text)
        }
        
        if options.removeFillers {
            text = removeFillerWords(text)
        }
        
        if options.formatPunctuation {
            text = formatSpokenPunctuation(text)
        }
        
        if options.autoCapitalize {
            text = capitalizeSentences(text)
        }
        
        return cleanWhitespace(text)
    }
    
    public func formatSpokenPunctuation(_ text: String) -> String {
        var result = text
        let mappings: [(pattern: String, replacement: String)] = [
            (#"\bquestion mark\b"#, "?"),
            (#"\bexclamation mark\b"#, "!"),
            (#"\bperiod\b"#, "."),
            (#"\bcomma\b"#, ","),
            (#"\bcolon\b"#, ":"),
            (#"\bsemicolon\b"#, ";"),
            (#"\bnew line\b"#, "\n"),
            (#"\bnew paragraph\b"#, "\n\n")
        ]
        
        for mapping in mappings {
            if let regex = try? NSRegularExpression(pattern: mapping.pattern, options: [.caseInsensitive]) {
                result = regex.stringByReplacingMatches(
                    in: result,
                    range: NSRange(result.startIndex..., in: result),
                    withTemplate: mapping.replacement
                )
            }
        }
        
        // Fix spacing around punctuation: e.g. "word ." -> "word."
        let spaceFixes: [(pattern: String, replacement: String)] = [
            (#"\s+([.,!?:;])"#, "$1"),
            (#"([.,!?:;])([A-Za-z])"#, "$1 $2")
        ]
        for fix in spaceFixes {
            if let regex = try? NSRegularExpression(pattern: fix.pattern) {
                result = regex.stringByReplacingMatches(
                    in: result,
                    range: NSRange(result.startIndex..., in: result),
                    withTemplate: fix.replacement
                )
            }
        }
        
        return result
    }
    
    public func removeFillerWords(_ text: String) -> String {
        var result = text
        // Remove standalone fillers: um, uh, er, ah
        let fillerPattern = #"\b(um|uh|er|ah)\b"#
        if let regex = try? NSRegularExpression(pattern: fillerPattern, options: [.caseInsensitive]) {
            result = regex.stringByReplacingMatches(
                in: result,
                range: NSRange(result.startIndex..., in: result),
                withTemplate: ""
            )
        }
        
        // Remove duplicated stutters: "the the" -> "the"
        let stutterPattern = #"\b([A-Za-z]+)\s+\1\b"#
        if let regex = try? NSRegularExpression(pattern: stutterPattern, options: [.caseInsensitive]) {
            result = regex.stringByReplacingMatches(
                in: result,
                range: NSRange(result.startIndex..., in: result),
                withTemplate: "$1"
            )
        }
        
        return cleanWhitespace(result)
    }
    
    public func resolveBacktracks(_ text: String) -> String {
        var result = text
        
        // Match phrases like "send john the report tomorrow actually friday"
        // Form: <sentence> actually <correction>
        let backtrackPattern = #"\b(.+?)\s+(?:actually)\s+(.+)"#
        if let regex = try? NSRegularExpression(pattern: backtrackPattern, options: [.caseInsensitive]) {
            let nsRange = NSRange(result.startIndex..., in: result)
            if let match = regex.firstMatch(in: result, range: nsRange),
               match.numberOfRanges >= 3,
               let range1 = Range(match.range(at: 1), in: result),
               let range2 = Range(match.range(at: 2), in: result) {
                let antecedent = String(result[range1]).trimmingCharacters(in: .whitespaces)
                let correction = String(result[range2]).trimmingCharacters(in: .whitespaces)
                
                // Formulate hyphenated/dash correction preserving conversational context
                result = "\(antecedent)—actually, \(correction)"
            }
        }
        
        return result
    }
    
    private func capitalizeSentences(_ text: String) -> String {
        guard !text.isEmpty else { return "" }
        var result = ""
        var capitalizeNext = true
        
        for char in text {
            if capitalizeNext && char.isLetter {
                result.append(char.uppercased())
                capitalizeNext = false
            } else {
                result.append(char)
                if char == "." || char == "!" || char == "?" || char == "\n" {
                    capitalizeNext = true
                }
            }
        }
        return result
    }
    
    private func cleanWhitespace(_ text: String) -> String {
        let pattern = #"[ \t]+"#
        if let regex = try? NSRegularExpression(pattern: pattern) {
            let cleaned = regex.stringByReplacingMatches(
                in: text,
                range: NSRange(text.startIndex..., in: text),
                withTemplate: " "
            )
            return cleaned.trimmingCharacters(in: .whitespaces)
        }
        return text.trimmingCharacters(in: .whitespaces)
    }
}
