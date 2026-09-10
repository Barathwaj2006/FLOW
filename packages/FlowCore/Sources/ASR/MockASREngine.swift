import Foundation

/// A configurable mock ASR engine for CI pipelines, unit testing, and latency benchmarks.
public final class MockASREngine: ASREngineProtocol, @unchecked Sendable {
    public var modelIdentifier: String
    public var cannedResponse: String
    public var simulatedDelayMs: Double
    public var simulatedConfidence: Float
    public var errorToThrow: Error?
    
    private let lock = NSLock()
    private var _callCount: Int = 0
    private var _lastAudioBuffer: AudioBuffer?
    
    public init(
        identifier: String = "mock-engine",
        cannedResponse: String = "Testing local voice recognition.",
        simulatedDelayMs: Double = 50.0,
        simulatedConfidence: Float = 0.98
    ) {
        self.modelIdentifier = identifier
        self.cannedResponse = cannedResponse
        self.simulatedDelayMs = simulatedDelayMs
        self.simulatedConfidence = simulatedConfidence
    }
    
    public var callCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return _callCount
    }
    
    public var lastAudioBuffer: AudioBuffer? {
        lock.lock()
        defer { lock.unlock() }
        return _lastAudioBuffer
    }
    
    public func loadModel(identifier: String) async throws {
        self.modelIdentifier = identifier
    }
    
    public func transcribe(audio: AudioBuffer, options: TranscribeOptions) async throws -> TranscriptionResult {
        lock.lock()
        _callCount += 1
        _lastAudioBuffer = audio
        let err = errorToThrow
        let delay = simulatedDelayMs
        let response = cannedResponse
        let confidence = simulatedConfidence
        lock.unlock()
        
        if let err = err {
            throw err
        }
        
        if delay > 0 {
            try await Task.sleep(nanoseconds: UInt64(delay * 1_000_000))
        }
        
        return TranscriptionResult(
            text: response,
            detectedLanguage: options.languageCode ?? "en",
            confidence: confidence,
            inferenceDurationMs: delay
        )
    }
    
    public func reset() {
        lock.lock()
        defer { lock.unlock() }
        _callCount = 0
        _lastAudioBuffer = nil
        errorToThrow = nil
    }
}
