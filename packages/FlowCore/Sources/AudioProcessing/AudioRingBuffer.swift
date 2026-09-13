import Foundation

/// A high-performance, thread-safe circular audio buffer for 16kHz mono PCM samples.
/// Designed for zero-allocation streaming during continuous speech recording.
public final class AudioRingBuffer: @unchecked Sendable {
    private let sampleRate: Double
    private let capacity: Int
    private var buffer: [Float]
    private var writeIndex: Int = 0
    private var availableCount: Int = 0
    private let lock = NSLock()
    
    /// Initializes an AudioRingBuffer with a maximum capacity.
    /// - Parameters:
    ///   - maxDurationSeconds: Maximum audio length to hold before rolling over (default: 30 seconds).
    ///   - sampleRate: Audio sample rate in Hz (default: 16,000 Hz).
    public init(maxDurationSeconds: Double = 30.0, sampleRate: Double = 16000.0) {
        self.sampleRate = sampleRate
        self.capacity = Int(maxDurationSeconds * sampleRate)
        self.buffer = [Float](repeating: 0.0, count: self.capacity)
    }
    
    /// Current number of samples available in the buffer.
    public var count: Int {
        lock.lock()
        defer { lock.unlock() }
        return availableCount
    }
    
    /// Duration of audio currently available in seconds.
    public var availableDuration: TimeInterval {
        lock.lock()
        defer { lock.unlock() }
        guard sampleRate > 0 else { return 0 }
        return Double(availableCount) / sampleRate
    }
    
    /// Appends incoming audio samples into the ring buffer.
    /// If capacity is exceeded, oldest samples are overwritten (rolling buffer).
    public func write(_ samples: [Float]) {
        guard !samples.isEmpty else { return }
        lock.lock()
        defer { lock.unlock() }
        
        for sample in samples {
            buffer[writeIndex] = sample
            writeIndex = (writeIndex + 1) % capacity
            if availableCount < capacity {
                availableCount += 1
            }
        }
    }
    
    /// Extracts all currently recorded audio samples in chronological order.
    public func readAll() -> [Float] {
        lock.lock()
        defer { lock.unlock() }
        
        guard availableCount > 0 else { return [] }
        
        var result = [Float](repeating: 0.0, count: availableCount)
        let startIndex = (writeIndex - availableCount + capacity) % capacity
        
        for i in 0..<availableCount {
            result[i] = buffer[(startIndex + i) % capacity]
        }
        
        return result
    }
    
    /// Extracts the most recent `seconds` duration of audio.
    public func readRecent(seconds: TimeInterval) -> [Float] {
        lock.lock()
        defer { lock.unlock() }
        
        let requestedCount = Int(seconds * sampleRate)
        let sampleCountToRead = min(requestedCount, availableCount)
        guard sampleCountToRead > 0 else { return [] }
        
        var result = [Float](repeating: 0.0, count: sampleCountToRead)
        let startIndex = (writeIndex - sampleCountToRead + capacity) % capacity
        
        for i in 0..<sampleCountToRead {
            result[i] = buffer[(startIndex + i) % capacity]
        }
        
        return result
    }
    
    /// Resets the buffer pointers without deallocating underlying memory.
    public func clear() {
        lock.lock()
        defer { lock.unlock() }
        writeIndex = 0
        availableCount = 0
    }
}
