import Foundation

/// Casing styles supported by Developer Mode.
public enum CasingStyle: String, Sendable, CaseIterable {
    case camelCase
    case pascalCase
    case snakeCase
    case screamingSnakeCase
    case kebabCase
}

/// Utility for transforming spoken phrases into programmatic identifiers.
public enum CasingTransformer {
    /// Converts a natural language phrase into the designated programming casing style.
    /// - Parameters:
    ///   - phrase: Input words (e.g. "get user account balance")
    ///   - style: Target CasingStyle (e.g. .camelCase)
    /// - Returns: Formatted identifier (e.g. "getUserAccountBalance")
    public static func transform(_ phrase: String, style: CasingStyle) -> String {
        let words = phrase
            .components(separatedBy: CharacterSet.alphanumerics.inverted)
            .filter { !$0.isEmpty }
            .map { $0.lowercased() }
        
        guard !words.isEmpty else { return "" }
        
        switch style {
        case .camelCase:
            let first = words[0]
            let rest = words.dropFirst().map { $0.capitalized }
            return ([first] + rest).joined()
            
        case .pascalCase:
            return words.map { $0.capitalized }.joined()
            
        case .snakeCase:
            return words.joined(separator: "_")
            
        case .screamingSnakeCase:
            return words.map { $0.uppercased() }.joined(separator: "_")
            
        case .kebabCase:
            return words.joined(separator: "-")
        }
    }
}
