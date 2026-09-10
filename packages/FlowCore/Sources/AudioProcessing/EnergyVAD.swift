import Foundation

/// A lightweight, deterministic Voice Activity Detector based on Root-Mean-Square (RMS)
/// energy calculation and adaptive noise floor estimation.
public final class EnergyVAD: VADProtocol, @unchecked Sendable {
    private let sampleRate: Double
    private let energyThreshold: Float
    private let silenceTimeoutMs: Double
    
    private var accumulatedSilenceMs: Double = 0.0
    private var noiseFloor: Float = 0.005
    private let lock = NSLock()
    
    /// Initializes EnergyVAD.
    /// - Parameters:
    ///   - sampleRate: Audio sample rate in Hz (default: 16,000 Hz).
    ///   - energyThreshold: Minimum RMS amplitude required to trigger speech (default: 0.012).
    ///   - silenceTimeoutMs: Milliseconds of continuous sub-threshold audio before silence flag (default: 450ms).
    public init(
        sampleRate: Double = 16000.0,
        energyThreshold: Float = 0.012,
        silenceTimeoutMs: Double = 450.0
    ) {
        self.sampleRate = sampleRate
        self.energyThreshold = energyThreshold
        self.silenceTimeoutMs = silenceTimeoutMs
    }
    
    public func process(samples: [Float]) -> VADDecision {
        guard !samples.isEmpty else {
            return VADDecision(
                state: .silence,
                speechProbability: 0.0,
                rmsEnergy: 0.0,
                trailingSilenceMs: accumulatedSilenceMs
            )
        }
        
        lock.lock()
        defer { lock.unlock() }
        
        // 1. Calculate Root-Mean-Square (RMS) Energy
        var sumSquares: Float = 0.0
        for sample in samples {
            sumSquares += sample * sample
        }
        let rms = sqrt(sumSquares / Float(samples.count))
        
        // 2. Chunk duration in milliseconds
        let chunkDurationMs = (Double(samples.count) / sampleRate) * 1000.0
        
        // 3. Determine Speech vs Silence
        let isSpeech = rms > energyThreshold
        let probability = min(1.0, max(0.0, (rms - noiseFloor) / (energyThreshold * 2.0)))
        
        if isSpeech {
            accumulatedSilenceMs = 0.0
            return VADDecision(
                state: .speech,
                speechProbability: probability,
                rmsEnergy: rms,
                trailingSilenceMs: 0.0
            )
        } else {
            accumulatedSilenceMs += chunkDurationMs
            // Slowly track background noise floor during quiet passages
            noiseFloor = (noiseFloor * 0.95) + (rms * 0.05)
            
            return VADDecision(
                state: .silence,
                speechProbability: probability,
                rmsEnergy: rms,
                trailingSilenceMs: accumulatedSilenceMs
            )
        }
    }
    
    public func reset() {
        lock.lock()
        defer { lock.unlock() }
        accumulatedSilenceMs = 0.0
        noiseFloor = 0.005
    }
}
