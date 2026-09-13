import Foundation

/// Mock implementation of TextInsertionServiceProtocol for testing and CI.
public final class MockTextInsertionService: TextInsertionServiceProtocol, @unchecked Sendable {
    private let lock = NSLock()
    private var _injectedHistory: [String] = []
    public var shouldSucceed: Bool = true
    public var targetIsEditable: Bool = true
    
    public init() {}
    
    public var injectedHistory: [String] {
        lock.lock()
        defer { lock.unlock() }
        return _injectedHistory
    }
    
    public func insert(text: String) async throws {
        lock.lock()
        defer { lock.unlock() }
        
        guard shouldSucceed else {
            throw TextInsertionError.elementNotEditable
        }
        
        _injectedHistory.append(text)
    }
    
    public func isTargetEditable() async -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return targetIsEditable
    }
    
    public func reset() {
        lock.lock()
        defer { lock.unlock() }
        _injectedHistory.removeAll()
    }
}
