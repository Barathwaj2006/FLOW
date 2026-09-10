import Foundation
import FlowCore
#if os(macOS)
import AVFoundation
#endif

/// Errors that can occur during native audio capture.
public enum AudioCaptureError: Error, Sendable, Equatable {
    case microphonePermissionDenied
    case engineInitializationFailed(String)
    case converterCreationFailed
    case deviceDisconnected
}

/// Native macOS audio capture engine streaming 16kHz Float32 mono samples from AVAudioEngine.
public final class AudioCaptureEngine: @unchecked Sendable {
    #if os(macOS)
    private let audioEngine = AVAudioEngine()
    private var isCapturing = false
    private let lock = NSLock()
    private var sampleHandler: (([Float]) -> Void)?
    #endif
    
    public init() {
        #if os(macOS)
        setupNotifications()
        #endif
    }
    
    /// Starts capturing audio from the default system microphone.
    /// - Parameter onChunk: Callback invoked with 16kHz mono Float32 audio samples.
    public func startCapture(onChunk: @escaping @Sendable ([Float]) -> Void) throws {
        #if os(macOS)
        lock.lock()
        defer { lock.unlock() }
        
        guard !isCapturing else { return }
        self.sampleHandler = onChunk
        
        let inputNode = audioEngine.inputNode
        let hardwareFormat = inputNode.inputFormat(forBus: 0)
        
        guard hardwareFormat.sampleRate > 0 else {
            throw AudioCaptureError.engineInitializationFailed("Invalid hardware sample rate: 0")
        }
        
        // Target format: 16,000 Hz, 1 channel (mono), Float32
        guard let targetFormat = AVAudioFormat(
            commonFormat: .pcmFormatFloat32,
            sampleRate: 16000.0,
            channels: 1,
            interleaved: false
        ) else {
            throw AudioCaptureError.converterCreationFailed
        }
        
        guard let converter = AVAudioConverter(from: hardwareFormat, to: targetFormat) else {
            throw AudioCaptureError.converterCreationFailed
        }
        
        // Install audio tap with a buffer size of ~512 to 1024 frames (~32-64ms)
        let bufferSize: AVAudioFrameCount = 1024
        inputNode.installTap(onBus: 0, bufferSize: bufferSize, format: hardwareFormat) { [weak self] (buffer, when) in
            guard let self = self else { return }
            
            // Determine capacity for resampled target buffer
            let frameRatio = targetFormat.sampleRate / hardwareFormat.sampleRate
            let targetFrameCapacity = AVAudioFrameCount(Double(buffer.frameLength) * frameRatio) + 64
            
            guard let outputBuffer = AVAudioPCMBuffer(pcmFormat: targetFormat, frameCapacity: targetFrameCapacity) else {
                return
            }
            
            var error: NSError?
            var isConsumed = false
            converter.convert(to: outputBuffer, error: &error) { inNumPackets, outStatus in
                if isConsumed {
                    outStatus.pointee = .noDataNow
                    return nil
                }
                outStatus.pointee = .haveData
                isConsumed = true
                return buffer
            }
            
            guard error == nil, outputBuffer.frameLength > 0 else { return }
            
            if let floatData = outputBuffer.floatChannelData?[0] {
                let count = Int(outputBuffer.frameLength)
                let floatArray = Array(UnsafeBufferPointer(start: floatData, count: count))
                self.sampleHandler?(floatArray)
            }
        }
        
        do {
            try audioEngine.start()
            isCapturing = true
            FlowMacOS.logger.info("AVAudioEngine capture started successfully at 16kHz target format.")
        } catch {
            inputNode.removeTap(onBus: 0)
            throw AudioCaptureError.engineInitializationFailed(error.localizedDescription)
        }
        #else
        // Non-macOS stub
        #endif
    }
    
    /// Stops audio capture and removes the audio tap.
    public func stopCapture() {
        #if os(macOS)
        lock.lock()
        defer { lock.unlock() }
        
        guard isCapturing else { return }
        audioEngine.inputNode.removeTap(onBus: 0)
        audioEngine.stop()
        isCapturing = false
        sampleHandler = nil
        FlowMacOS.logger.info("AVAudioEngine capture stopped.")
        #endif
    }
    
    #if os(macOS)
    private func setupNotifications() {
        NotificationCenter.default.addObserver(
            forName: .AVAudioEngineConfigurationChange,
            object: audioEngine,
            queue: .main
        ) { [weak self] _ in
            FlowMacOS.logger.warning("Audio device configuration changed (route change/headset switch).")
            guard let self = self, self.isCapturing else { return }
            // Re-bind tap if engine restarted by system
            self.stopCapture()
        }
    }
    #endif
}
