import Foundation
#if os(macOS)
import AppKit
import AVFoundation
import ApplicationServices
#endif

/// State of system permissions required by FLOW.
public struct PermissionStatus: Sendable, Equatable {
    public let isMicrophoneAuthorized: Bool
    public let isAccessibilityAuthorized: Bool
    
    public var allAuthorized: Bool {
        isMicrophoneAuthorized && isAccessibilityAuthorized
    }
}

/// Central manager responsible for checking and requesting macOS system permissions.
public final class PermissionManager: @unchecked Sendable {
    public static let shared = PermissionManager()
    
    public init() {}
    
    /// Queries the current authorization status for Microphone and Accessibility.
    public func checkPermissions() -> PermissionStatus {
        #if os(macOS)
        let micStatus: Bool = {
            let status = AVCaptureDevice.authorizationStatus(for: .audio)
            return status == .authorized
        }()
        
        let axStatus: Bool = {
            return AXIsProcessTrusted()
        }()
        
        return PermissionStatus(
            isMicrophoneAuthorized: micStatus,
            isAccessibilityAuthorized: axStatus
        )
        #else
        return PermissionStatus(isMicrophoneAuthorized: true, isAccessibilityAuthorized: true)
        #endif
    }
    
    /// Prompts the user for microphone access if not yet granted.
    public func requestMicrophoneAccess() async -> Bool {
        #if os(macOS)
        let status = AVCaptureDevice.authorizationStatus(for: .audio)
        switch status {
        case .authorized:
            return true
        case .notDetermined:
            return await withCheckedContinuation { continuation in
                AVCaptureDevice.requestAccess(for: .audio) { granted in
                    continuation.resume(returning: granted)
                }
            }
        case .denied, .restricted:
            return false
        @unknown default:
            return false
        }
        #else
        return true
        #endif
    }
    
    /// Requests Accessibility authorization by presenting macOS System prompt if ungranted.
    public func requestAccessibilityAccess() -> Bool {
        #if os(macOS)
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        return AXIsProcessTrustedWithOptions(options)
        #else
        return true
        #endif
    }
    
    /// Opens the macOS System Settings pane to Privacy & Security.
    public func openSystemSettings() {
        #if os(macOS)
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
        #endif
    }
}
