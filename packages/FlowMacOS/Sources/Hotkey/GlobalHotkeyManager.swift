import Foundation
#if os(macOS)
import AppKit
import Carbon
#endif

/// Mode of operation for the global hotkey.
public enum HotkeyMode: Sendable {
    case pushToTalk  // Hold to record, release to transcribe & insert
    case toggle      // Press once to start, press again to stop
}

/// Manages system-wide global hotkeys for triggering voice recording sessions.
public final class GlobalHotkeyManager: @unchecked Sendable {
    public var mode: HotkeyMode
    private var isRecording = false
    private let lock = NSLock()
    
    #if os(macOS)
    private var globalMonitor: Any?
    private var localMonitor: Any?
    #endif
    
    public var onHotkeyDown: (@Sendable () -> Void)?
    public var onHotkeyUp: (@Sendable () -> Void)?
    
    public init(mode: HotkeyMode = .pushToTalk) {
        self.mode = mode
    }
    
    /// Starts monitoring for the designated hotkey combination.
    public func startMonitoring() {
        #if os(macOS)
        // Monitor global keydown/keyup events (requires Accessibility permission)
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.flagsChanged, .keyDown, .keyUp]) { [weak self] event in
            self?.handleKeyEvent(event)
        }
        
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: [.flagsChanged, .keyDown, .keyUp]) { [weak self] event in
            self?.handleKeyEvent(event)
            return event
        }
        #endif
    }
    
    /// Stops event monitors.
    public func stopMonitoring() {
        #if os(macOS)
        if let gm = globalMonitor { NSEvent.removeMonitor(gm) }
        if let lm = localMonitor { NSEvent.removeMonitor(lm) }
        globalMonitor = nil
        localMonitor = nil
        #endif
    }
    
    #if os(macOS)
    private func handleKeyEvent(_ event: NSEvent) {
        // Trigger condition: Option + Space key combination
        let isOptionPressed = event.modifierFlags.contains(.option)
        let isSpaceKey = (event.keyCode == 49) // 49 is macOS virtual keycode for Space
        
        guard isOptionPressed && isSpaceKey else { return }
        
        lock.lock()
        defer { lock.unlock() }
        
        if event.type == .keyDown {
            if mode == .pushToTalk {
                if !isRecording {
                    isRecording = true
                    onHotkeyDown?()
                }
            } else if mode == .toggle {
                isRecording.toggle()
                if isRecording {
                    onHotkeyDown?()
                } else {
                    onHotkeyUp?()
                }
            }
        } else if event.type == .keyUp && mode == .pushToTalk {
            if isRecording {
                isRecording = false
                onHotkeyUp?()
            }
        }
    }
    #endif
}
