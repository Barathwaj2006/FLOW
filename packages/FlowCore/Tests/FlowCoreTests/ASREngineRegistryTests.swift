import XCTest
@testable import FlowCore

final class ASREngineRegistryTests: XCTestCase {
    
    func testRegistryPrimaryDispatch() async throws {
        let registry = ASREngineRegistry()
        let mock = MockASREngine(identifier: "mock-primary", cannedResponse: "Primary ASR result")
        
        registry.register(engine: mock, as: "mock-primary", isPrimary: true)
        
        let dummyBuffer = AudioBuffer(samples: [0.1, 0.2, 0.3])
        let result = try await registry.transcribeWithFallback(audio: dummyBuffer)
        
        XCTAssertEqual(result.text, "Primary ASR result")
        XCTAssertEqual(mock.callCount, 1)
    }
    
    func testRegistryFallbackEscalation() async throws {
        let registry = ASREngineRegistry()
        
        let failingPrimary = MockASREngine(identifier: "failing-primary")
        failingPrimary.errorToThrow = ASREngineError.transcriptionFailed("Model OOM")
        
        let workingFallback = MockASREngine(identifier: "working-fallback", cannedResponse: "Fallback ASR result")
        
        registry.register(engine: failingPrimary, as: "primary", isPrimary: true)
        registry.register(engine: workingFallback, as: "fallback")
        registry.setFallbackChain(["fallback"])
        
        let dummyBuffer = AudioBuffer(samples: [0.1, 0.2, 0.3])
        let result = try await registry.transcribeWithFallback(audio: dummyBuffer)
        
        XCTAssertEqual(result.text, "Fallback ASR result")
        XCTAssertEqual(failingPrimary.callCount, 1)
        XCTAssertEqual(workingFallback.callCount, 1)
    }
    
    func testRegistryThrowsWhenAllFail() async {
        let registry = ASREngineRegistry()
        
        let failing1 = MockASREngine(identifier: "failing1")
        failing1.errorToThrow = ASREngineError.transcriptionFailed("Err 1")
        
        let failing2 = MockASREngine(identifier: "failing2")
        failing2.errorToThrow = ASREngineError.transcriptionFailed("Err 2")
        
        registry.register(engine: failing1, as: "f1", isPrimary: true)
        registry.register(engine: failing2, as: "f2")
        registry.setFallbackChain(["f2"])
        
        let dummyBuffer = AudioBuffer(samples: [0.1, 0.2, 0.3])
        
        do {
            _ = try await registry.transcribeWithFallback(audio: dummyBuffer)
            XCTFail("Expected transcription to fail")
        } catch {
            // Expected
        }
    }
}
