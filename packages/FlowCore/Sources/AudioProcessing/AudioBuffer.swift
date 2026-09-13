import Foundation

/// Represents a standardized 16kHz mono audio buffer for local VAD and ASR processing.
public struct AudioBuffer: Sendable {
    /// Sample rate in Hertz (Standardized at 16,000 Hz).
    public let sampleRate: Double
    
    /// Number of audio channels (Standardized at 1 for mono).
    public let channelCount: Int
    
    /// Raw 32-bit floating-point linear PCM samples normalized to [-1.0, 1.0].
    public let samples: [Float]
    
    /// Duration of the audio buffer in seconds.
    public var duration: TimeInterval {
        guard sampleRate > 0 else { return 0 }
        return Double(samples.count) / sampleRate
    }
    
    public init(samples: [Float], sampleRate: Double = 16000.0, channelCount: Int = 1) {
        self.samples = samples
        self.sampleRate = sampleRate
        self.channelCount = channelCount
    }
}

/// Protocol for real-time audio capture devices and mock audio sources.
public protocol AudioSourceProtocol: Sendable {
    func startStreaming() async throws -> AsyncStream<AudioBuffer>
    func stopStreaming() async
}
