import Foundation

/// Configuration options for the Language Engine post-processing.
public struct LanguageCleanupOptions: Sendable {
    public let formatPunctuation: Bool
    public let removeFillers: Bool
    public let resolveBacktracks: Bool
    public let autoCapitalize: Bool
    
    public init(
        formatPunctuation: Bool = true,
        removeFillers: Bool = true,
        resolveBacktracks: Bool = true,
        autoCapitalize: Bool = true
    ) {
        self.formatPunctuation = formatPunctuation
        self.removeFillers = removeFillers
        self.resolveBacktracks = resolveBacktracks
        self.autoCapitalize = autoCapitalize
    }
}

/// Protocol defining local language formatting and cleanup operations.
public protocol LanguageEngineProtocol: Sendable {
    /// Full cleanup pipeline converting raw ASR transcript into clean prose.
    func process(rawTranscript: String, options: LanguageCleanupOptions) -> String
    
    /// Parse and replace spoken punctuation words (e.g. "period", "comma").
    func formatSpokenPunctuation(_ text: String) -> String
    
    /// Remove filler words and stutters ("um", "uh", "the the").
    func removeFillerWords(_ text: String) -> String
    
    /// Detect and resolve backtrack corrections ("actually Friday").
    func resolveBacktracks(_ text: String) -> String
}
