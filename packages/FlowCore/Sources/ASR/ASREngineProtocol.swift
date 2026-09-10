import Foundation

/// Options configuring speech-to-text inference.
public struct TranscribeOptions: Sendable {
    /// BCP-47 language tag (e.g., "en" or "ta"). Nil indicates auto-detection.
    public let languageCode: String?
    
    /// Optional prompt conditioning text (e.g. recent context or custom terminology).
    public let promptConditioning: String?
    
    /// Temperature for decoding. Defaults to 0.0 for deterministic greedy decoding.
    public let temperature: Float
    
    public init(
        languageCode: String? = nil,
        promptConditioning: String? = nil,
        temperature: Float = 0.0
    ) {
        self.languageCode = languageCode
        self.promptConditioning = promptConditioning
        self.temperature = temperature
    }
}

/// The result of an ASR transcription operation.
public struct TranscriptionResult: Sendable {
    /// Raw textual transcript produced by the model.
    public let text: String
    
    /// Detected or specified language code.
    public let detectedLanguage: String
    
    /// Average transcription confidence score (0.0 to 1.0).
    public let confidence: Float
    
    /// Time taken to perform speech recognition in milliseconds.
    public let inferenceDurationMs: Double
    
    public init(
        text: String,
        detectedLanguage: String = "en",
        confidence: Float = 1.0,
        inferenceDurationMs: Double = 0.0
    ) {
        self.text = text
        self.detectedLanguage = detectedLanguage
        self.confidence = confidence
        self.inferenceDurationMs = inferenceDurationMs
    }
}

/// Abstract driver protocol for local on-device ASR engines (WhisperKit, whisper.cpp, Apple Speech).
public protocol ASREngineProtocol: Sendable {
    /// Identifier of the active model (e.g. "whisper-base.en").
    var modelIdentifier: String { get }
    
    /// Initialize and load weights into memory / ANE.
    func loadModel(identifier: String) async throws
    
    /// Transcribe a discrete buffer of 16kHz audio.
    func transcribe(audio: AudioBuffer, options: TranscribeOptions) async throws -> TranscriptionResult
}
