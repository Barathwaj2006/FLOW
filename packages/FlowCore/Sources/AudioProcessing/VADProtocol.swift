import Foundation

/// Voice Activity Detection states.
public enum VADState: String, Sendable, Equatable {
    case speech
    case silence
}

/// The evaluation result of a single audio chunk processed by VAD.
public struct VADDecision: Sendable, Equatable {
    /// Whether speech or silence was detected in the chunk.
    public let state: VADState
    
    /// Estimated probability that speech is present (0.0 to 1.0).
    public let speechProbability: Float
    
    /// Root-Mean-Square (RMS) signal energy of the evaluated chunk.
    public let rmsEnergy: Float
    
    /// Continuous duration of trailing silence accumulated so far in milliseconds.
    public let trailingSilenceMs: Double
    
    public init(
        state: VADState,
        speechProbability: Float,
        rmsEnergy: Float,
        trailingSilenceMs: Double
    ) {
        self.state = state
        self.speechProbability = speechProbability
        self.rmsEnergy = rmsEnergy
        self.trailingSilenceMs = trailingSilenceMs
    }
}

/// Protocol defining a real-time Voice Activity Detector.
public protocol VADProtocol: AnyObject, Sendable {
    /// Process a discrete chunk of 16kHz audio samples.
    func process(samples: [Float]) -> VADDecision
    
    /// Reset internal state, silence counters, and adaptive noise floors.
    func reset()
}
