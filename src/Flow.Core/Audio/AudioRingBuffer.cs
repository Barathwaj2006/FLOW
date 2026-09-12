using System;

namespace Flow.Core.Audio;

/// <summary>
/// Thread-safe circular ring buffer designed for real-time 16kHz audio sample buffering.
/// Pre-allocates memory to eliminate heap allocations during active recording.
/// </summary>
public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private readonly object _syncLock = new();

    private int _head; // Write position
    private int _tail; // Read position
    private int _count;

    /// <summary>
    /// Total capacity of the ring buffer in samples.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Number of available samples currently stored in the buffer.
    /// </summary>
    public int AvailableSamples
    {
        get
        {
            lock (_syncLock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Sample rate of the audio in Hz (typically 16000).
    /// </summary>
    public double SampleRate { get; }

    /// <summary>
    /// Duration in seconds of currently buffered audio.
    /// </summary>
    public double BufferedDurationSeconds => SampleRate > 0 ? (double)AvailableSamples / SampleRate : 0.0;

    /// <summary>
    /// Cumulative count of samples written during the current session.
    /// </summary>
    public long TotalSamplesWritten { get; private set; }

    /// <summary>
    /// Count of samples dropped due to buffer rollover (should be 0 for sessions <= 20 minutes).
    /// </summary>
    public long TotalSamplesDropped { get; private set; }

    /// <summary>
    /// Peak absolute sample amplitude encountered.
    /// </summary>
    public float PeakAmplitude { get; private set; }

    /// <summary>
    /// Initializes an audio ring buffer with specified duration capacity.
    /// Default is 1200 seconds (20 minutes) to match the desktop recording limit without audio loss.
    /// </summary>
    /// <param name="capacitySeconds">Maximum buffer capacity in seconds (default: 1200.0 seconds / 20 minutes).</param>
    /// <param name="sampleRate">Sample rate in Hz (default: 16000).</param>
    public AudioRingBuffer(double capacitySeconds = 1200.0, double sampleRate = 16000.0)
    {
        if (capacitySeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacitySeconds), "Capacity must be positive.");
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        SampleRate = sampleRate;
        _capacity = (int)(capacitySeconds * sampleRate);
        _buffer = new float[_capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// Writes audio samples into the ring buffer.
    /// If buffer overflows, oldest samples are discarded.
    /// </summary>
    /// <param name="samples">Span of 32-bit float audio samples.</param>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;

        lock (_syncLock)
        {
            int toWrite = samples.Length;
            TotalSamplesWritten += toWrite;

            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Math.Abs(samples[i]);
                if (abs > PeakAmplitude) PeakAmplitude = abs;
            }

            // If input is larger than total buffer capacity, only take the latest part
            if (toWrite >= _capacity)
            {
                TotalSamplesDropped += (toWrite - _capacity) + _count;
                samples.Slice(toWrite - _capacity).CopyTo(_buffer);
                _head = 0;
                _tail = 0;
                _count = _capacity;
                return;
            }

            int firstChunk = Math.Min(toWrite, _capacity - _head);
            samples.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(_head, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                samples.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            _head = (_head + toWrite) % _capacity;

            if (_count + toWrite > _capacity)
            {
                TotalSamplesDropped += (_count + toWrite) - _capacity;
                _count = _capacity;
                _tail = _head; // Tail pushed forward to overwrite oldest
            }
            else
            {
                _count += toWrite;
            }
        }
    }

    /// <summary>
    /// Reads samples from the buffer without advancing the read position.
    /// </summary>
    /// <param name="destination">Span to receive the copied samples.</param>
    /// <returns>Number of samples copied.</returns>
    public int Peek(Span<float> destination)
    {
        lock (_syncLock)
        {
            int toRead = Math.Min(destination.Length, _count);
            if (toRead == 0) return 0;

            int firstChunk = Math.Min(toRead, _capacity - _tail);
            _buffer.AsSpan(_tail, firstChunk).CopyTo(destination);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk));
            }

            return toRead;
        }
    }

    /// <summary>
    /// Reads and consumes samples from the buffer.
    /// </summary>
    /// <param name="destination">Span to receive the read samples.</param>
    /// <returns>Number of samples read.</returns>
    public int Read(Span<float> destination)
    {
        lock (_syncLock)
        {
            int read = Peek(destination);
            if (read > 0)
            {
                _tail = (_tail + read) % _capacity;
                _count -= read;
            }
            return read;
        }
    }

    /// <summary>
    /// Clears all buffered samples and resets head and tail pointers.
    /// </summary>
    public void Clear()
    {
        lock (_syncLock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    /// <summary>
    /// Exports all currently available samples to a newly allocated AudioBuffer.
    /// </summary>
    public AudioBuffer ToAudioBuffer()
    {
        lock (_syncLock)
        {
            if (_count == 0)
            {
                return AudioBuffer.Empty;
            }

            var samples = new float[_count];
            Peek(samples);
            return new AudioBuffer(samples, SampleRate, 1);
        }
    }
}
