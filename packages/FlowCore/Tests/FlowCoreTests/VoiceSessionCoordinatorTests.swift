import XCTest
@testable import FlowCore

final class VoiceSessionCoordinatorTests: XCTestCase {
    
    func testSessionFullLifecycle() async throws {
        let registry = ASREngineRegistry()
        let mockASR = MockASREngine(cannedResponse: "hello world from local voice")
        registry.register(engine: mockASR, as: "mock", isPrimary: true)
        
        let coordinator = VoiceSessionCoordinator(asrRegistry: registry)
        
        let initial = await coordinator.state
        XCTAssertEqual(initial, .idle)
        
        await coordinator.startSession()
        let recordingState = await coordinator.state
        guard case .recording = recordingState else {
            XCTFail("Expected state to be recording")
            return
        }
        
        // Feed audio
        let decision = await coordinator.ingestAudio(samples: [0.1, 0.2, 0.3, 0.4])
        XCTAssertNotNil(decision)
        
        // Stop session and get sanitized text
        let result = try await coordinator.stopSession()
        XCTAssertEqual(result, "Hello world from local voice.")
        
        let finalState = await coordinator.state
        guard case .completed(let text, _) = finalState else {
            XCTFail("Expected state to be completed")
            return
        }
        XCTAssertEqual(text, "Hello world from local voice.")
    }
    
    func testSessionCancellation() async {
        let coordinator = VoiceSessionCoordinator()
        
        await coordinator.startSession()
        _ = await coordinator.ingestAudio(samples: [0.5, 0.5])
        
        await coordinator.cancelSession()
        let state = await coordinator.state
        XCTAssertEqual(state, .idle)
    }
    
    func testEmptySessionReturnsNil() async throws {
        let coordinator = VoiceSessionCoordinator()
        
        await coordinator.startSession()
        // Stop without ingesting audio
        let result = try await coordinator.stopSession()
        XCTAssertNil(result)
        
        let state = await coordinator.state
        XCTAssertEqual(state, .idle)
    }
}
