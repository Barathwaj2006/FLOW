import XCTest
@testable import FlowCore

final class DeterministicTextSanitizerTests: XCTestCase {
    
    func testSanitizerCapitalization() {
        let input = "this is a test sentence"
        let output = DeterministicTextSanitizer.sanitize(input)
        XCTAssertEqual(output, "This is a test sentence.")
    }
    
    func testSanitizerWhitespaceCollapsing() {
        let input = "   lots   of    extra     spaces   between   words   "
        let output = DeterministicTextSanitizer.sanitize(input)
        XCTAssertEqual(output, "Lots of extra spaces between words.")
    }
    
    func testSanitizerPreservesExistingPunctuation() {
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("already has period."), "Already has period.")
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("what is this?"), "What is this?")
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("urgent announcement!"), "Urgent announcement!")
    }
    
    func testSanitizerSingleWordNoTerminalPeriod() {
        // Isolated single words should not be forcefully punctuated
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("yes"), "Yes")
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("cancel"), "Cancel")
    }
    
    func testSanitizerEmptyString() {
        XCTAssertEqual(DeterministicTextSanitizer.sanitize(""), "")
        XCTAssertEqual(DeterministicTextSanitizer.sanitize("    "), "")
    }
}
