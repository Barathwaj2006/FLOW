import XCTest
@testable import FlowCore

final class EnergyVADTests: XCTestCase {
    
    func testSilenceDetectionOnSilentAudio() {
        let vad = EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01)
        
        // 160 samples (10ms) of pure digital silence
        let silentChunk = [Float](repeating: 0.0, count: 160)
        let decision = vad.process(samples: silentChunk)
        
        XCTAssertEqual(decision.state, .silence)
        XCTAssertEqual(decision.rmsEnergy, 0.0)
        XCTAssertEqual(decision.trailingSilenceMs, 10.0, accuracy: 0.1)
    }
    
    func testSpeechDetectionOnActiveAudio() {
        let vad = EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01)
        
        // 160 samples of active audio (amplitude 0.5)
        let activeChunk = [Float](repeating: 0.5, count: 160)
        let decision = vad.process(samples: activeChunk)
        
        XCTAssertEqual(decision.state, .speech)
        XCTAssertGreaterThan(decision.rmsEnergy, 0.01)
        XCTAssertEqual(decision.trailingSilenceMs, 0.0)
    }
    
    func testTrailingSilenceAccumulationAndReset() {
        let vad = EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.02)
        let silentChunk = [Float](repeating: 0.001, count: 160) // 10ms
        
        _ = vad.process(samples: silentChunk)
        let d2 = vad.process(samples: silentChunk)
        XCTAssertEqual(d2.trailingSilenceMs, 20.0, accuracy: 0.1)
        
        // Speech resets trailing silence
        let activeChunk = [Float](repeating: 0.5, count: 160)
        let d3 = vad.process(samples: activeChunk)
        XCTAssertEqual(d3.state, .speech)
        XCTAssertEqual(d3.trailingSilenceMs, 0.0)
    }
}
