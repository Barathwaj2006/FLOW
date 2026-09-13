import Foundation

/// Lifecycle states of a Voice Session.
public enum VoiceSessionState: Sendable, Equatable {
    case idle
    case recording(startTime: Date)
    case processing(audioDuration: TimeInterval)
    case inserting(text: String)
    case completed(text: String, totalLatencyMs: Double)
    case cancelled
    case error(String)
}

/// Central Actor managing the lifecycle, VAD, ASR invocation, and deterministic text sanitization.
public actor VoiceSessionCoordinator {
    public private(set) var state: VoiceSessionState = .idle
    
    private let ringBuffer: AudioRingBuffer
    private let vad: VADProtocol
    private let asrRegistry: ASREngineRegistry
    
    private var sessionStartTime: Date?
    private var autoSilenceTermination: Bool
    
    public init(
        ringBuffer: AudioRingBuffer = AudioRingBuffer(maxDurationSeconds: 30.0),
        vad: VADProtocol = EnergyVAD(),
        asrRegistry: ASREngineRegistry = ASREngineRegistry(),
        autoSilenceTermination: Bool = true
    ) {
        self.ringBuffer = ringBuffer
        self.vad = vad
        self.asrRegistry = asrRegistry
        self.autoSilenceTermination = autoSilenceTermination
    }
    
    /// Starts a new voice recording session.
    public func startSession() {
        guard state == .idle || state == .cancelled else {
            FlowCore.logger.warning("Attempted to start session while in state: \(String(describing: self.state))")
            return
        }
        
        ringBuffer.clear()
        vad.reset()
        
        let now = Date()
        sessionStartTime = now
        state = .recording(startTime: now)
        FlowCore.logger.info("Voice session started.")
    }
    
    /// Ingests incoming 16kHz audio samples, feeds the ring buffer, and evaluates VAD.
    /// - Returns: VADDecision evaluating the chunk.
    public func ingestAudio(samples: [Float]) -> VADDecision {
        guard case .recording = state else {
            return VADDecision(state: .silence, speechProbability: 0.0, rmsEnergy: 0.0, trailingSilenceMs: 0.0)
        }
        
        ringBuffer.write(samples)
        let decision = vad.process(samples: samples)
        
        // Auto-terminate if trailing silence exceeds threshold
        if autoSilenceTermination && decision.state == .silence && decision.trailingSilenceMs >= 500.0 && ringBuffer.count > 4800 {
            FlowCore.logger.info("Trailing silence threshold met (\(decision.trailingSilenceMs)ms). Auto-committing session.")
        }
        
        return decision
    }
    
    /// Stops the recording session, executes local ASR, and sanitizes the transcript.
    /// - Returns: Clean, sanitized text ready for insertion, or nil if recording was empty.
    public func stopSession() async throws -> String? {
        guard case .recording = state else {
            FlowCore.logger.warning("Stop called while not recording.")
            return nil
        }
        
        let audioSamples = ringBuffer.readAll()
        guard !audioSamples.isEmpty else {
            FlowCore.logger.info("Recording buffer empty. Resetting to idle.")
            state = .idle
            return nil
        }
        
        let audioDuration = ringBuffer.availableDuration
        state = .processing(audioDuration: audioDuration)
        
        let startTimestamp = sessionStartTime ?? Date()
        
        do {
            let buffer = AudioBuffer(samples: audioSamples, sampleRate: 16000.0, channelCount: 1)
            let rawResult = try await asrRegistry.transcribeWithFallback(audio: buffer)
            
            // Apply deterministic rule-based sanitization
            let cleanText = DeterministicTextSanitizer.sanitize(rawResult.text)
            
            let totalLatencyMs = Date().timeIntervalSince(startTimestamp) * 1000.0
            state = .completed(text: cleanText, totalLatencyMs: totalLatencyMs)
            
            FlowCore.logger.info("Session completed. Total latency: \(totalLatencyMs, format: .fixed(precision: 1))ms. Text: \(cleanText, privacy: .private)")
            
            return cleanText
        } catch {
            state = .error(error.localizedDescription)
            FlowCore.logger.error("Session processing failed: \(error.localizedDescription)")
            throw error
        }
    }
    
    /// Aborts the current session immediately.
    /// Invariant: Cancelling discards all buffers and guarantees zero text insertion.
    public func cancelSession() {
        ringBuffer.clear()
        vad.reset()
        sessionStartTime = nil
        state = .cancelled
        FlowCore.logger.info("Voice session cancelled by user.")
        
        // Return to idle state after cleanup
        state = .idle
    }
}
