import Foundation
import FlowCore
#if os(macOS)
import AppKit
import ApplicationServices
#endif

/// Production implementation of TextInsertionServiceProtocol for macOS.
/// Guarantees:
/// 1. Directly injects text at the current cursor location without manual pasting.
/// 2. Inviolable Rule: NEVER emits keycodes for Return (0x24) or Keypad Enter (0x4C).
/// 3. Safely restores clipboard contents within 150ms if clipboard fallback is required.
public final class NativeTextInsertionService: TextInsertionServiceProtocol, @unchecked Sendable {
    
    public init() {}
    
    public func isTargetEditable() async -> Bool {
        #if os(macOS)
        let systemWide = AXUIElementCreateSystemWide()
        var focusedElementValue: AnyObject?
        let result = AXUIElementCopyAttributeValue(
            systemWide,
            kAXFocusedUIElementAttribute as CFString,
            &focusedElementValue
        )
        
        guard result == .success, let element = focusedElementValue else {
            return false
        }
        
        let axElement = element as! AXUIElement
        var isSettable: DarwinBoolean = false
        
        // Check if kAXSelectedTextAttribute or kAXValueAttribute can be modified
        let checkSelected = AXUIElementIsAttributeSettable(axElement, kAXSelectedTextAttribute as CFString, &isSettable)
        if checkSelected == .success && isSettable.boolValue {
            return true
        }
        
        let checkValue = AXUIElementIsAttributeSettable(axElement, kAXValueAttribute as CFString, &isSettable)
        if checkValue == .success && isSettable.boolValue {
            return true
        }
        
        // Element exists, may accept simulated Cmd+V even if not exposed as settable AX attribute
        return true
        #else
        return true
        #endif
    }
    
    public func insert(text: String) async throws {
        guard !text.isEmpty else { return }
        
        #if os(macOS)
        let systemWide = AXUIElementCreateSystemWide()
        var focusedElementValue: AnyObject?
        let result = AXUIElementCopyAttributeValue(
            systemWide,
            kAXFocusedUIElementAttribute as CFString,
            &focusedElementValue
        )
        
        // Tier 1: Direct Accessibility Injection
        if result == .success, let element = focusedElementValue {
            let axElement = element as! AXUIElement
            
            // Try inserting at cursor / replacing selection
            let setResult = AXUIElementSetAttributeValue(
                axElement,
                kAXSelectedTextAttribute as CFString,
                text as CFTypeRef
            )
            
            if setResult == .success {
                FlowMacOS.logger.info("Direct AXUIElement insertion succeeded.")
                return
            }
        }
        
        // Tier 2: Safe Clipboard Fallback with Automatic Restoration
        FlowMacOS.logger.info("Direct AX insertion unavailable. Escalating to safe clipboard injection fallback.")
        try await insertViaSafeClipboardFallback(text: text)
        #else
        // Non-macOS stub
        FlowCore.logger.info("Simulated text insertion: \(text)")
        #endif
    }
    
    #if os(macOS)
    private func insertViaSafeClipboardFallback(text: String) async throws {
        let pasteboard = NSPasteboard.general
        
        // 1. Backup existing clipboard contents
        let previousItems = pasteboard.pasteboardItems?.compactMap { item -> (NSPasteboard.PasteboardType, Data)? in
            for type in item.types {
                if let data = item.data(forType: type) {
                    return (type, data)
                }
            }
            return nil
        } ?? []
        
        // 2. Set new text to pasteboard
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        
        // 3. Post Cmd+V keystroke event (Virtual Keycode 0x09 is 'v')
        // Explicitly NEVER post keycode 0x24 (Return) or 0x4C (KeypadEnter)
        let src = CGEventSource(stateID: .combinedSessionState)
        guard let keyDown = CGEvent(keyboardEventSource: src, virtualKey: 0x09, keyDown: true),
              let keyUp = CGEvent(keyboardEventSource: src, virtualKey: 0x09, keyDown: false) else {
            throw TextInsertionError.clipboardFallbackFailed
        }
        
        keyDown.flags = .maskCommand
        keyUp.flags = .maskCommand
        
        keyDown.post(tap: .cghidEventTap)
        keyUp.post(tap: .cghidEventTap)
        
        // 4. Wait 150ms for target application to ingest clipboard, then restore
        try await Task.sleep(nanoseconds: 150_000_000)
        
        // Restore original clipboard state
        pasteboard.clearContents()
        for (type, data) in previousItems {
            pasteboard.setData(data, forType: type)
        }
        FlowMacOS.logger.info("Clipboard contents restored successfully.")
    }
    #endif
}
