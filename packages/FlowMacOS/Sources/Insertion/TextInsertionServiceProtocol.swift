import Foundation

/// Defines error states that can occur during text insertion into external macOS applications.
public enum TextInsertionError: Error, Sendable, Equatable {
    case noFocusedElement
    case elementNotEditable
    case accessibilityPermissionDenied
    case clipboardFallbackFailed
    case insertionTimeout
}

/// Invariant: The text insertion engine must NEVER simulate Return (kVK_Return, 0x24),
/// Keypad Enter, or click any form submit / message send buttons.
public protocol TextInsertionServiceProtocol: Sendable {
    /// Inserts text at the current cursor position in the active frontmost application.
    /// - Parameter text: The cleaned, validated text string to inject.
    func insert(text: String) async throws
    
    /// Checks whether the currently focused element in the frontmost application is editable.
    func isTargetEditable() async -> Bool
}
