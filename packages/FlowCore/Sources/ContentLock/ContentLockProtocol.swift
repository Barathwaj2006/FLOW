import Foundation

/// Category of an entity protected by Content Lock.
public enum ProtectedEntityCategory: String, Sendable, CaseIterable {
    case codeIdentifier
    case technicalTerm
    case negativeConstraint
    case number
    case dateOrTime
    case urlOrPath
    case properNoun
}

/// A specific token or constraint extracted from user speech that must not be altered.
public struct ProtectedEntity: Sendable, Equatable {
    public let text: String
    public let category: ProtectedEntityCategory
    public let isNegativeConstraint: Bool
    
    public init(text: String, category: ProtectedEntityCategory, isNegativeConstraint: Bool = false) {
        self.text = text
        self.category = category
        self.isNegativeConstraint = isNegativeConstraint
    }
}

/// Outcome of a Content Lock validation check.
public struct ContentLockValidationResult: Sendable {
    /// True if all constraints, entities, and the No-Invention Rule are satisfied.
    public let isApproved: Bool
    
    /// Entities successfully preserved in the output.
    public let preservedEntities: [ProtectedEntity]
    
    /// Entities dropped or mutated during transformation (Validation Failures).
    public let violatedEntities: [ProtectedEntity]
    
    /// Invented technical entities detected in output that were never in the input.
    public let hallucinatedEntities: [String]
    
    public init(
        isApproved: Bool,
        preservedEntities: [ProtectedEntity] = [],
        violatedEntities: [ProtectedEntity] = [],
        hallucinatedEntities: [String] = []
    ) {
        self.isApproved = isApproved
        self.preservedEntities = preservedEntities
        self.violatedEntities = violatedEntities
        self.hallucinatedEntities = hallucinatedEntities
    }
}

/// Protocol defining the Content Lock verification engine.
public protocol ContentLockProtocol: Sendable {
    /// Extract protected entities and negative constraints from original user speech.
    func extractProtectedEntities(from input: String) -> [ProtectedEntity]
    
    /// Validate an AI transformation against the original input and extracted entities.
    func validate(
        originalInput: String,
        transformedOutput: String,
        protectedEntities: [ProtectedEntity]
    ) -> ContentLockValidationResult
}
