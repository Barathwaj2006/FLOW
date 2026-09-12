using System;
using System.Collections.Generic;
using Flow.Host.Windows.Native;
using Xunit;

namespace Flow.Windows.Tests;

public class WasapiDeviceManagerTests
{
    [Fact]
    public void EnumerateCaptureDevices_DoesNotThrow_AndReturnsValidList()
    {
        using var manager = new WasapiDeviceManager();
        var devices = manager.EnumerateCaptureDevices();

        Assert.NotNull(devices);
        // On Windows 10/11 with hardware microphone, at least one device or empty list if headless CI
        foreach (var dev in devices)
        {
            Assert.False(string.IsNullOrEmpty(dev.Id), "Device ID should not be empty.");
            Assert.False(string.IsNullOrEmpty(dev.Name), "Device Name should not be empty.");
            Assert.True(dev.State >= 0, "Device State should be non-negative.");
        }
    }

    [Fact]
    public void GetDefaultCaptureDeviceId_ReturnsNonNullOrEmpty_WhenEndpointsExist()
    {
        using var manager = new WasapiDeviceManager();
        var devices = manager.EnumerateCaptureDevices();
        string? defaultId = manager.GetDefaultCaptureDeviceId();

        if (devices.Count > 0)
        {
            Assert.NotNull(defaultId);
            Assert.NotEmpty(defaultId);
        }
    }

    [Fact]
    public void DeviceNotificationEvents_CanBeRegisteredAndDisposedSafely()
    {
        using var manager = new WasapiDeviceManager();
        bool defaultChangedFired = false;
        bool stateChangedFired = false;

        manager.DefaultDeviceChanged += id => defaultChangedFired = true;
        manager.DeviceStateChanged += (id, state) => stateChangedFired = true;

        // Disposing manager should unregister COM notifications without throwing
        manager.Dispose();

        Assert.False(defaultChangedFired);
        Assert.False(stateChangedFired);
    }
}
