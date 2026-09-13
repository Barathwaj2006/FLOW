import Foundation

/// Errors related to ASR engine selection and dispatch.
public enum ASREngineError: Error, Sendable, Equatable {
    case engineNotFound(String)
    case noActiveEngine
    case modelNotLoaded
    case audioBufferEmpty
    case transcriptionFailed(String)
}

/// Central registry managing pluggable ASR backends, runtime selection, and fallback escalation.
public final class ASREngineRegistry: @unchecked Sendable {
    private var engines: [String: ASREngineProtocol] = [:]
    private var primaryEngineId: String?
    private var fallbackEngineIds: [String] = []
    private let lock = NSLock()
    
    public init() {}
    
    /// Register an ASR engine instance.
    public func register(engine: ASREngineProtocol, as identifier: String, isPrimary: Bool = false) {
        lock.lock()
        defer { lock.unlock() }
        engines[identifier] = engine
        if isPrimary || primaryEngineId == nil {
            primaryEngineId = identifier
        }
    }
    
    /// Sets the list of engine identifiers to try if the primary fails.
    public func setFallbackChain(_ identifiers: [String]) {
        lock.lock()
        defer { lock.unlock() }
        fallbackEngineIds = identifiers
    }
    
    /// Set the primary active engine.
    public func setActiveEngine(_ identifier: String) throws {
        lock.lock()
        defer { lock.unlock() }
        guard engines[identifier] != nil else {
            throw ASREngineError.engineNotFound(identifier)
        }
        primaryEngineId = identifier
    }
    
    /// Retrieve the currently active primary engine.
    public var activeEngine: ASREngineProtocol? {
        lock.lock()
        defer { lock.unlock() }
        guard let id = primaryEngineId else { return nil }
        return engines[id]
    }
    
    /// Transcribe audio using the primary engine, escalating to fallbacks on failure.
    public func transcribeWithFallback(
        audio: AudioBuffer,
        options: TranscribeOptions = .init()
    ) async throws -> TranscriptionResult {
        guard !audio.samples.isEmpty else {
            throw ASREngineError.audioBufferEmpty
        }
        
        let (primary, fallbacks) = { () -> (ASREngineProtocol?, [ASREngineProtocol]) in
            lock.lock()
            defer { lock.unlock() }
            let p = primaryEngineId.flatMap { engines[$0] }
            let f = fallbackEngineIds.compactMap { engines[$0] }
            return (p, f)
        }()
        
        guard let primary = primary else {
            throw ASREngineError.noActiveEngine
        }
        
        // Try Primary Engine
        do {
            return try await primary.transcribe(audio: audio, options: options)
        } catch {
            FlowCore.logger.warning("Primary ASR '\(primary.modelIdentifier)' failed: \(error.localizedDescription). Escalating to fallbacks.")
            
            // Try Fallback Chain
            for fallback in fallbacks {
                do {
                    FlowCore.logger.info("Attempting fallback ASR: '\(fallback.modelIdentifier)'")
                    return try await fallback.transcribe(audio: audio, options: options)
                } catch {
                    FlowCore.logger.warning("Fallback ASR '\(fallback.modelIdentifier)' failed: \(error.localizedDescription)")
                }
            }
            
            throw ASREngineError.transcriptionFailed("All registered ASR engines in chain failed. Last error: \(error)")
        }
    }
}
