import Foundation
import os

/// FLOW Core Engine
/// Provides platform-agnostic audio processing, local speech recognition abstractions,
/// deterministic language cleanup, and Content Lock fidelity validation.
public enum FlowCore {
    public static let version = "0.1.0"
    public static let subsystem = "com.flow.core"
    
    public static let logger = Logger(subsystem: subsystem, category: "engine")
}
