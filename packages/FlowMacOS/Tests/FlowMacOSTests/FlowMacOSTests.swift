import XCTest
@testable import FlowMacOS
@testable import FlowCore

final class FlowMacOSTests: XCTestCase {
    
    func testMockTextInsertionSuccessful() async throws {
        let service = MockTextInsertionService()
        
        let editable = await service.isTargetEditable()
        XCTAssertTrue(editable)
        
        try await service.insert(text: "Send John the report tomorrow—actually, Friday.")
        
        let history = service.injectedHistory
        XCTAssertEqual(history.count, 1)
        XCTAssertEqual(history.first, "Send John the report tomorrow—actually, Friday.")
    }
    
    func testMockTextInsertionFailure() async {
        let service = MockTextInsertionService()
        service.shouldSucceed = false
        
        do {
            try await service.insert(text: "Test failure")
            XCTFail("Expected insertion to throw error")
        } catch {
            XCTAssertEqual(error as? TextInsertionError, .elementNotEditable)
        }
    }
}
