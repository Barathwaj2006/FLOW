using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Audio capture device description.
/// </summary>
public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    bool IsDefault,
    int State
);

/// <summary>
/// Production WASAPI audio capture device manager.
/// Discovers and enumerates active Windows microphone endpoints, tracks the default audio capture device,
/// and listens for device changes (plug, unplug, default switch) via IMMNotificationClient.
/// </summary>
public sealed class WasapiDeviceManager : IDisposable
{
    private static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    private readonly ILogger<WasapiDeviceManager>? _logger;
    private IMMDeviceEnumerator? _enumerator;
    private NotificationClientWrapper? _notificationClient;
    private bool _isDisposed;
    private readonly object _lock = new();

    public event Action<string>? DefaultDeviceChanged;
    public event Action<string, int>? DeviceStateChanged;
    public event Action<string>? DeviceAdded;
    public event Action<string>? DeviceRemoved;

    public WasapiDeviceManager(ILogger<WasapiDeviceManager>? logger = null)
    {
        _logger = logger;
        InitializeEnumerator();
    }

    private void InitializeEnumerator()
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
            if (enumeratorType == null)
            {
                _logger?.LogError("Failed to locate WASAPI MMDeviceEnumerator COM type.");
                return;
            }

            _enumerator = Activator.CreateInstance(enumeratorType) as IMMDeviceEnumerator;
            if (_enumerator == null)
            {
                _logger?.LogError("Failed to instantiate IMMDeviceEnumerator COM object.");
                return;
            }

            _notificationClient = new NotificationClientWrapper(this);
            int hr = _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
            if (hr != 0)
            {
                _logger?.LogWarning("IMMDeviceEnumerator::RegisterEndpointNotificationCallback returned 0x{Hr:X8}", hr);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize WasapiDeviceManager COM enumerator.");
        }
    }

    /// <summary>
    /// Enumerates all active audio capture (microphone) devices on the Windows system.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> EnumerateCaptureDevices()
    {
        lock (_lock)
        {
            var devices = new List<AudioDeviceInfo>();
            if (_enumerator == null) return devices;

            string? defaultDevId = GetDefaultCaptureDeviceId();

            IMMDeviceCollection? collection = null;
            try
            {
                // eCapture = 1, DEVICE_STATE_ACTIVE = 1
                int hr = _enumerator.EnumAudioEndpoints(1, 1, out collection);
                if (hr != 0 || collection == null)
                {
                    _logger?.LogWarning("EnumAudioEndpoints failed with HRESULT 0x{Hr:X8}", hr);
                    return devices;
                }

                hr = collection.GetCount(out uint count);
                if (hr != 0) return devices;

                for (uint i = 0; i < count; i++)
                {
                    IMMDevice? dev = null;
                    try
                    {
                        hr = collection.Item(i, out dev);
                        if (hr != 0 || dev == null) continue;

                        string devId = string.Empty;
                        if (dev.GetId(out string idStr) == 0 && idStr != null)
                        {
                            devId = idStr;
                        }

                        string name = GetDeviceFriendlyName(dev) ?? ("Microphone " + (i + 1));
                        int state = 1;
                        dev.GetState(out state);

                        bool isDefault = !string.IsNullOrEmpty(devId) && string.Equals(devId, defaultDevId, StringComparison.OrdinalIgnoreCase);

                        devices.Add(new AudioDeviceInfo(devId, name, isDefault, state));
                    }
                    finally
                    {
                        if (dev != null) Marshal.ReleaseComObject(dev);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while enumerating WASAPI capture endpoints.");
            }
            finally
            {
                if (collection != null) Marshal.ReleaseComObject(collection);
            }

            return devices;
        }
    }

    /// <summary>
    /// Gets the unique ID of the default audio capture endpoint (eCapture = 1, eConsole = 0).
    /// </summary>
    public string? GetDefaultCaptureDeviceId()
    {
        lock (_lock)
        {
            if (_enumerator == null) return null;

            IMMDevice? dev = null;
            try
            {
                int hr = _enumerator.GetDefaultAudioEndpoint(1, 0, out dev);
                if (hr == 0 && dev != null && dev.GetId(out string idStr) == 0)
                {
                    return idStr;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to query default audio capture endpoint ID.");
            }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
            }

            return null;
        }
    }

    /// <summary>
    /// Obtains an IMMDevice by its unique endpoint ID. If deviceId is null or empty, returns default.
    /// Caller is responsible for releasing the COM object.
    /// </summary>
    public int GetDevice(string? deviceId, out IMMDevice? device)
    {
        device = null;
        lock (_lock)
        {
            if (_enumerator == null) return -1;

            if (string.IsNullOrEmpty(deviceId))
            {
                return _enumerator.GetDefaultAudioEndpoint(1, 0, out device);
            }

            return _enumerator.GetDevice(deviceId, out device);
        }
    }

    private string? GetDeviceFriendlyName(IMMDevice dev)
    {
        IntPtr pStore = IntPtr.Zero;
        try
        {
            // STGM_READ = 0
            int hr = dev.OpenPropertyStore(0, out pStore);
            if (hr != 0 || pStore == IntPtr.Zero) return null;

            var propStore = (IPropertyStore)Marshal.GetObjectForIUnknown(pStore);
            var key = PKEY_Device_FriendlyName;
            hr = propStore.GetValue(ref key, out PROPVARIANT pv);
            if (hr == 0 && pv.vt == 31 /* VT_LPWSTR */ && pv.pwszVal != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(pv.pwszVal);
            }
        }
        catch
        {
            // Ignore property reading errors
        }
        finally
        {
            if (pStore != IntPtr.Zero)
            {
                Marshal.Release(pStore);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            lock (_lock)
            {
                if (_enumerator != null && _notificationClient != null)
                {
                    try
                    {
                        _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                    }
                    catch
                    {
                        // Ignore unregister errors during shutdown
                    }
                }

                if (_enumerator != null)
                {
                    Marshal.ReleaseComObject(_enumerator);
                    _enumerator = null;
                }

                _isDisposed = true;
            }
        }
        GC.SuppressFinalize(this);
    }

    #region Notification Client Inner Class

    [ComVisible(true)]
    private sealed class NotificationClientWrapper : IMMNotificationClient
    {
        private readonly WasapiDeviceManager _parent;

        public NotificationClientWrapper(WasapiDeviceManager parent)
        {
            _parent = parent;
        }

        public void OnDeviceStateChanged(string pwstrDeviceId, int dwNewState)
        {
            _parent._logger?.LogInformation("WASAPI Device State Changed: {DeviceId} -> {State}", pwstrDeviceId, dwNewState);
            _parent.DeviceStateChanged?.Invoke(pwstrDeviceId, dwNewState);
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _parent._logger?.LogInformation("WASAPI Device Added: {DeviceId}", pwstrDeviceId);
            _parent.DeviceAdded?.Invoke(pwstrDeviceId);
        }

        public void OnDeviceRemoved(string pwstrDeviceId)
        {
            _parent._logger?.LogInformation("WASAPI Device Removed: {DeviceId}", pwstrDeviceId);
            _parent.DeviceRemoved?.Invoke(pwstrDeviceId);
        }

        public void OnDefaultDeviceChanged(int dataFlow, int role, string pwstrDefaultDeviceId)
        {
            // dataFlow: eRender=0, eCapture=1; role: eConsole=0, eMultimedia=1, eCommunications=2
            if (dataFlow == 1 /* eCapture */)
            {
                _parent._logger?.LogInformation("WASAPI Default Capture Device Changed: {DeviceId} (Role: {Role})", pwstrDefaultDeviceId, role);
                _parent.DefaultDeviceChanged?.Invoke(pwstrDefaultDeviceId);
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key)
        {
            // Property value changes ignored for core voice
        }
    }

    #endregion

    #region COM Interfaces & Structures

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint pcDevices);
        [PreserveSig]
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig]
        int GetState(out int pdwState);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);
        [PreserveSig]
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig]
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        [PreserveSig]
        int Commit();
    }

    [ComImport]
    [Guid("7991EEC0-3658-4D59-963A-56F7372B4B54")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);
        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        void OnDefaultDeviceChanged(int dataFlow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(2)] public ushort wReserved1;
        [FieldOffset(4)] public ushort wReserved2;
        [FieldOffset(6)] public ushort wReserved3;
        [FieldOffset(8)] public IntPtr pwszVal;
        [FieldOffset(8)] public int lVal;
        [FieldOffset(8)] public uint ulVal;
    }

    #endregion
}
