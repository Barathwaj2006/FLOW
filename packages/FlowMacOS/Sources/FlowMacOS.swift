import Foundation
import FlowCore
import os

/// Native macOS host integration layer for FLOW.
public enum FlowMacOS {
    public static let subsystem = "com.flow.macos"
    public static let logger = Logger(subsystem: subsystem, category: "host")
}
