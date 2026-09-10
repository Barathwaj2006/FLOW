import Foundation
import FlowCore
#if os(macOS)
import AppKit
import SwiftUI
#endif

/// Visual display states for the floating status overlay.
public enum HUDVisualState: Sendable, Equatable {
    case hidden
    case recording
    case processing
    case error(String)
}

/// Floating HUD window controller displaying current recording and processing state
/// without stealing focus from the active text field.
public final class FloatingHUDController: @unchecked Sendable {
    public static let shared = FloatingHUDController()
    
    #if os(macOS)
    private var panel: NSPanel?
    #endif
    public private(set) var currentState: HUDVisualState = .hidden
    
    public init() {}
    
    /// Displays the HUD with the specified state.
    @MainActor
    public func show(state: HUDVisualState) {
        self.currentState = state
        
        #if os(macOS)
        if panel == nil {
            createPanel()
        }
        
        guard let panel = panel else { return }
        
        if state == .hidden {
            panel.orderOut(nil)
        } else {
            panel.orderFrontRegardless()
        }
        #endif
    }
    
    /// Hides the HUD overlay.
    @MainActor
    public func hide() {
        show(state: .hidden)
    }
    
    #if os(macOS)
    @MainActor
    private func createPanel() {
        let p = NSPanel(
            contentRect: NSRange(location: 0, length: 0).toRect(),
            styleMask: [.nonactivatingPanel, .borderless],
            backing: .buffered,
            defer: false
        )
        
        p.isOpaque = false
        p.backgroundColor = .clear
        p.level = .floating
        p.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        p.isMovableByWindowBackground = true
        
        // Position at bottom center of main screen
        if let screen = NSScreen.main {
            let screenRect = screen.visibleFrame
            let panelWidth: CGFloat = 180
            let panelHeight: CGFloat = 44
            let xPos = screenRect.origin.x + (screenRect.width - panelWidth) / 2
            let yPos = screenRect.origin.y + 40
            p.setFrame(NSRect(x: xPos, y: yPos, width: panelWidth, height: panelHeight), display: true)
        }
        
        self.panel = p
    }
    #endif
}

#if os(macOS)
private extension NSRange {
    func toRect() -> NSRect {
        NSRect(x: 0, y: 0, width: 180, height: 44)
    }
}
#endif
