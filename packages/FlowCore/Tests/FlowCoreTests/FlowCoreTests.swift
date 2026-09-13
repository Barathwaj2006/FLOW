import XCTest
@testable import FlowCore

final class FlowCoreTests: XCTestCase {
    
    func testCasingTransformer() {
        let input = "get user account balance"
        
        XCTAssertEqual(CasingTransformer.transform(input, style: .camelCase), "getUserAccountBalance")
        XCTAssertEqual(CasingTransformer.transform(input, style: .pascalCase), "GetUserAccountBalance")
        XCTAssertEqual(CasingTransformer.transform(input, style: .snakeCase), "get_user_account_balance")
        XCTAssertEqual(CasingTransformer.transform(input, style: .screamingSnakeCase), "GET_USER_ACCOUNT_BALANCE")
        XCTAssertEqual(CasingTransformer.transform(input, style: .kebabCase), "get-user-account-balance")
    }
    
    func testAudioBufferDuration() {
        let sampleRate = 16000.0
        let samples = [Float](repeating: 0.0, count: 32000)
        let buffer = AudioBuffer(samples: samples, sampleRate: sampleRate, channelCount: 1)
        
        XCTAssertEqual(buffer.duration, 2.0, accuracy: 0.001)
        XCTAssertEqual(buffer.channelCount, 1)
        XCTAssertEqual(buffer.sampleRate, 16000.0)
    }
    
    func testLanguageEngineSpokenPunctuation() {
        let engine = RuleBasedLanguageEngine()
        let raw = "hello world period this is a test question mark"
        let processed = engine.process(rawTranscript: raw)
        
        XCTAssertEqual(processed, "Hello world. This is a test?")
    }
    
    func testLanguageEngineFillerRemoval() {
        let engine = RuleBasedLanguageEngine()
        let raw = "um we need to uh deploy the the server"
        let processed = engine.process(rawTranscript: raw)
        
        XCTAssertEqual(processed, "We need to deploy the server")
    }
    
    func testLanguageEngineBacktracking() {
        let engine = RuleBasedLanguageEngine()
        let raw = "send john the report tomorrow actually friday"
        let processed = engine.process(rawTranscript: raw)
        
        XCTAssertEqual(processed, "Send john the report tomorrow—actually, Friday")
    }
    
    func testContentLockProtocolModels() {
        let entity = ProtectedEntity(
            text: "Firebase",
            category: .technicalTerm,
            isNegativeConstraint: true
        )
        
        XCTAssertEqual(entity.text, "Firebase")
        XCTAssertTrue(entity.isNegativeConstraint)
        XCTAssertEqual(entity.category, .technicalTerm)
        
        let validation = ContentLockValidationResult(
            isApproved: false,
            violatedEntities: [entity]
        )
        
        XCTAssertFalse(validation.isApproved)
        XCTAssertEqual(validation.violatedEntities.count, 1)
        XCTAssertEqual(validation.violatedEntities.first?.text, "Firebase")
    }
}
