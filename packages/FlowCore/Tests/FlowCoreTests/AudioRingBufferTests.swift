import XCTest
@testable import FlowCore

final class AudioRingBufferTests: XCTestCase {
    
    func testWriteAndReadAll() {
        let ring = AudioRingBuffer(maxDurationSeconds: 1.0, sampleRate: 10.0) // Capacity: 10 samples
        
        XCTAssertEqual(ring.count, 0)
        XCTAssertEqual(ring.availableDuration, 0.0)
        
        ring.write([1.0, 2.0, 3.0])
        XCTAssertEqual(ring.count, 3)
        XCTAssertEqual(ring.availableDuration, 0.3, accuracy: 0.01)
        
        let all = ring.readAll()
        XCTAssertEqual(all, [1.0, 2.0, 3.0])
    }
    
    func testOverflowRollover() {
        let ring = AudioRingBuffer(maxDurationSeconds: 1.0, sampleRate: 5.0) // Capacity: 5 samples
        
        ring.write([1.0, 2.0, 3.0, 4.0, 5.0])
        XCTAssertEqual(ring.count, 5)
        XCTAssertEqual(ring.readAll(), [1.0, 2.0, 3.0, 4.0, 5.0])
        
        // Write 2 more samples, which should overwrite the oldest 2 (1.0, 2.0)
        ring.write([6.0, 7.0])
        XCTAssertEqual(ring.count, 5)
        XCTAssertEqual(ring.readAll(), [3.0, 4.0, 5.0, 6.0, 7.0])
    }
    
    func testReadRecent() {
        let ring = AudioRingBuffer(maxDurationSeconds: 1.0, sampleRate: 10.0) // Capacity: 10 samples
        ring.write([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0])
        
        // Read recent 0.3 seconds = 3 samples
        let recent = ring.readRecent(seconds: 0.3)
        XCTAssertEqual(recent, [5.0, 6.0, 7.0])
    }
    
    func testClear() {
        let ring = AudioRingBuffer(maxDurationSeconds: 1.0, sampleRate: 10.0)
        ring.write([1.0, 2.0, 3.0])
        XCTAssertEqual(ring.count, 3)
        
        ring.clear()
        XCTAssertEqual(ring.count, 0)
        XCTAssertEqual(ring.readAll(), [])
    }
}
