using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Production Windows Core Audio (WASAPI) capture engine.
/// Interfaces directly with native Windows Audio COM endpoints (IMMDevice, IAudioClient, IAudioCaptureClient),
/// converts device audio to standardized 16kHz Float32 mono PCM, and streams live buffers to FLOW.
/// </summary>
public sealed class WasapiAudioCapture : IDisposable
{
    public const int TargetSampleRate = 16000;
    private readonly ILogger<WasapiAudioCapture>? _logger;
    private readonly Action<float[]>? _onSamplesCaptured;

    private Thread? _captureThread;
    private volatile bool _isCapturing;
    private readonly ManualResetEventSlim _startedEvent = new(false);
    private IntPtr _hAudioEvent = IntPtr.Zero;
    private bool _isDisposed;

    public bool IsCapturing => _isCapturing;
    public string? ActiveDeviceName { get; private set; }
    public Exception? LastError { get; private set; }
    public int DiagnosticWaitCount { get; private set; }
    public int DiagnosticTimeoutCount { get; private set; }
    public int DiagnosticPacketsReceived { get; private set; }
    public int NativeSampleRate { get; private set; }
    public int NativeChannels { get; private set; }

    public bool WaitForStart(int timeoutMs = 3000) => _startedEvent.Wait(timeoutMs);

    public WasapiAudioCapture(Action<float[]>? onSamplesCaptured = null, ILogger<WasapiAudioCapture>? logger = null)
    {
        _onSamplesCaptured = onSamplesCaptured;
        _logger = logger;
    }

    /// <summary>
    /// Starts capturing live microphone audio on a dedicated high-priority capture thread.
    /// </summary>
    public void Start()
    {
        if (_isCapturing) return;

        _startedEvent.Reset();
        _isCapturing = true;
        _captureThread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "Flow.WasapiCaptureThread",
            Priority = ThreadPriority.AboveNormal
        };
        _captureThread.Start();
    }

    /// <summary>
    /// Stops audio capture and cleans up unmanaged endpoints.
    /// </summary>
    public void Stop()
    {
        if (!_isCapturing) return;

        _isCapturing = false;
        _startedEvent.Reset();
        if (_hAudioEvent != IntPtr.Zero)
        {
            SetEvent(_hAudioEvent);
        }

        _captureThread?.Join(500);
        _captureThread = null;
    }

    private void CaptureLoop()
    {
        _logger?.LogInformation("Initializing live WASAPI microphone capture endpoint...");

        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioClient? audioClient = null;
        IAudioCaptureClient? captureClient = null;
        IntPtr pWaveFormat = IntPtr.Zero;

        try
        {
            // 1. Instantiate Core Audio Device Enumerator
            var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumeratorType == null)
            {
                throw new InvalidOperationException("Failed to locate WASAPI MMDeviceEnumerator COM type.");
            }

            enumerator = Activator.CreateInstance(enumeratorType) as IMMDeviceEnumerator;
            if (enumerator == null)
            {
                throw new InvalidOperationException("Failed to create IMMDeviceEnumerator.");
            }

            // 2. Get default audio capture endpoint (eCapture = 1, eConsole = 0)
            int hr = enumerator.GetDefaultAudioEndpoint(1, 0, out device);
            if (hr != 0 || device == null)
            {
                throw new InvalidOperationException($"Failed to obtain default audio capture endpoint. HRESULT: 0x{hr:X8}");
            }

            // 3. Activate IAudioClient
            var audioClientGuid = typeof(IAudioClient).GUID;
            hr = device.Activate(ref audioClientGuid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out object ppAudioClient);
            if (hr != 0 || ppAudioClient == null)
            {
                throw new InvalidOperationException($"Failed to activate IAudioClient. HRESULT: 0x{hr:X8}");
            }
            audioClient = (IAudioClient)ppAudioClient;

            // 4. Query mix format
            hr = audioClient.GetMixFormat(out pWaveFormat);
            if (hr != 0 || pWaveFormat == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to query IAudioClient mix format. HRESULT: 0x{hr:X8}");
            }

            var waveFormat = Marshal.PtrToStructure<WAVEFORMATEX>(pWaveFormat);
            int nativeSampleRate = (int)waveFormat.nSamplesPerSec;
            int channels = waveFormat.nChannels;
            int bitsPerSample = waveFormat.wBitsPerSample;
            bool isFloat = waveFormat.wFormatTag == 3 /* WAVE_FORMAT_IEEE_FLOAT */ ||
                           (waveFormat.wFormatTag == 0xFFFE /* WAVE_FORMAT_EXTENSIBLE */ && bitsPerSample == 32);

            _logger?.LogInformation("WASAPI Mic Format: {SampleRate}Hz, {Channels} ch, {Bits} bits (Float: {IsFloat})",
                nativeSampleRate, channels, bitsPerSample, isFloat);

            // 5. Initialize audio stream (Shared mode, 200ms buffer, event callback)
            const long bufferDurationHns = 2000000; // 200ms in 100ns units
            const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
            hr = audioClient.Initialize(0, AUDCLNT_STREAMFLAGS_EVENTCALLBACK, bufferDurationHns, 0, pWaveFormat, IntPtr.Zero);
            if (hr != 0)
            {
                throw new InvalidOperationException($"IAudioClient::Initialize failed with HRESULT: 0x{hr:X8}");
            }

            // 6. Create event handle and bind to audio client
            _hAudioEvent = CreateEvent(IntPtr.Zero, false, false, null);
            hr = audioClient.SetEventHandle(_hAudioEvent);
            if (hr != 0)
            {
                throw new InvalidOperationException($"IAudioClient::SetEventHandle failed with HRESULT: 0x{hr:X8}");
            }

            // 7. Get IAudioCaptureClient service
            var captureClientGuid = typeof(IAudioCaptureClient).GUID;
            hr = audioClient.GetService(ref captureClientGuid, out object ppCaptureClient);
            if (hr != 0 || ppCaptureClient == null)
            {
                throw new InvalidOperationException($"Failed to get IAudioCaptureClient service. HRESULT: 0x{hr:X8}");
            }
            captureClient = (IAudioCaptureClient)ppCaptureClient;

            // 8. Start capture
            hr = audioClient.Start();
            if (hr != 0)
            {
                throw new InvalidOperationException($"IAudioClient::Start failed with HRESULT: 0x{hr:X8}");
            }

            NativeSampleRate = nativeSampleRate;
            NativeChannels = channels;

            _startedEvent.Set();
            _logger?.LogInformation("WASAPI live capture loop active. Streaming 16kHz float32 mono samples.");

            // 9. Process live microphone packets
            while (_isCapturing)
            {
                uint waitResult = WaitForSingleObject(_hAudioEvent, 100);
                DiagnosticWaitCount++;
                if (!_isCapturing) break;

                if (waitResult == 0 /* WAIT_OBJECT_0 */)
                {
                    while (true)
                    {
                        hr = captureClient.GetNextPacketSize(out uint packetSize);
                        if (hr != 0 || packetSize == 0) break;

                        hr = captureClient.GetBuffer(
                            out IntPtr pData,
                            out uint numFramesRead,
                            out uint flags,
                            out ulong _,
                            out ulong _
                        );

                        if (hr != 0) break;

                        DiagnosticPacketsReceived++;

                        try
                        {
                            if (numFramesRead > 0 && pData != IntPtr.Zero)
                            {
                                bool isSilent = (flags & 0x01 /* AUDCLNT_BUFFERFLAGS_SILENT */) != 0;
                                float[] targetSamples = ConvertAndResample(pData, numFramesRead, nativeSampleRate, channels, bitsPerSample, isFloat, isSilent);

                                if (targetSamples.Length > 0 && _onSamplesCaptured != null)
                                {
                                    _onSamplesCaptured(targetSamples);
                                }
                            }
                        }
                        finally
                        {
                            captureClient.ReleaseBuffer(numFramesRead);
                        }
                    }
                }
                else
                {
                    DiagnosticTimeoutCount++;
                }
            }

            audioClient.Stop();
        }
        catch (Exception ex)
        {
            LastError = ex;
            _logger?.LogError(ex, "Live WASAPI capture encountered an exception.");
        }
        finally
        {
            if (pWaveFormat != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pWaveFormat);
            }
            if (captureClient != null) Marshal.ReleaseComObject(captureClient);
            if (audioClient != null) Marshal.ReleaseComObject(audioClient);
            if (device != null) Marshal.ReleaseComObject(device);
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);

            if (_hAudioEvent != IntPtr.Zero)
            {
                CloseHandle(_hAudioEvent);
                _hAudioEvent = IntPtr.Zero;
            }

            _logger?.LogInformation("WASAPI capture loop terminated and resources released.");
        }
    }

    /// <summary>
    /// Converts and resamples raw native audio buffer to canonical 16,000Hz mono Float32 array.
    /// </summary>
    private static float[] ConvertAndResample(
        IntPtr pData,
        uint numFrames,
        int nativeSampleRate,
        int channels,
        int bitsPerSample,
        bool isFloat,
        bool isSilent)
    {
        // 1. Decode native frames to mono float array
        float[] monoNative = new float[numFrames];

        if (isSilent)
        {
            Array.Clear(monoNative, 0, monoNative.Length);
        }
        else if (isFloat && bitsPerSample == 32)
        {
            int totalFloats = (int)numFrames * channels;
            float[] rawFloats = new float[totalFloats];
            Marshal.Copy(pData, rawFloats, 0, totalFloats);

            for (int i = 0; i < numFrames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += rawFloats[i * channels + c];
                }
                monoNative[i] = sum / channels;
            }
        }
        else if (bitsPerSample == 16)
        {
            int totalShorts = (int)numFrames * channels;
            short[] rawShorts = new short[totalShorts];
            Marshal.Copy(pData, rawShorts, 0, totalShorts);

            for (int i = 0; i < numFrames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += rawShorts[i * channels + c] / 32768.0f;
                }
                monoNative[i] = sum / channels;
            }
        }
        else
        {
            // Fallback for unsupported raw bit-depth
            Array.Clear(monoNative, 0, monoNative.Length);
        }

        // 2. Resample from nativeSampleRate to TargetSampleRate (16kHz)
        if (nativeSampleRate == TargetSampleRate)
        {
            return monoNative;
        }

        double resampleRatio = (double)nativeSampleRate / TargetSampleRate;
        int targetLength = (int)Math.Floor(numFrames / resampleRatio);
        if (targetLength <= 0) return Array.Empty<float>();

        float[] resampled = new float[targetLength];
        for (int i = 0; i < targetLength; i++)
        {
            double srcIdx = i * resampleRatio;
            int idxFloor = (int)Math.Floor(srcIdx);
            int idxCeil = Math.Min(idxFloor + 1, monoNative.Length - 1);
            double frac = srcIdx - idxFloor;

            resampled[i] = (float)((1.0 - frac) * monoNative[idxFloor] + frac * monoNative[idxCeil]);
        }

        return resampled;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _startedEvent.Dispose();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Win32 Core Audio COM Interfaces & P/Invoke

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
        [PreserveSig]
        int GetBufferSize(out uint pNumBufferFrames);
        [PreserveSig]
        int GetStreamLatency(out long phnsLatency);
        [PreserveSig]
        int GetCurrentPadding(out uint pNumPaddingFrames);
        [PreserveSig]
        int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        [PreserveSig]
        int GetMixFormat(out IntPtr ppDeviceFormat);
        [PreserveSig]
        int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        [PreserveSig]
        int Start();
        [PreserveSig]
        int Stop();
        [PreserveSig]
        int Reset();
        [PreserveSig]
        int SetEventHandle(IntPtr eventHandle);
        [PreserveSig]
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
        [PreserveSig]
        int ReleaseBuffer(uint numFramesRead);
        [PreserveSig]
        int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion
}
