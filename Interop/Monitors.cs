using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

/// <summary>
/// Win32 display-layout helpers for changing which monitor is primary.
/// </summary>
internal static class Monitors
{
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DM_POSITION = 0x00000020;
    private const uint CDS_SET_PRIMARY = 0x00000010;
    private const uint CDS_UPDATEREGISTRY = 0x00000001;
    private const uint CDS_NORESET = 0x10000000;

    /// <summary>
    /// Makes <paramref name="primaryDeviceName"/> the primary monitor, shifting every
    /// other monitor so the layout stays contiguous with the new origin at (0, 0).
    /// </summary>
    public static void SetPrimary(string primaryDeviceName)
    {
        var devices = new List<(string name, DEVMODE mode)>();
        uint deviceIdx = 0;

        var displayDevice = new DISPLAY_DEVICE();
        displayDevice.cb = Marshal.SizeOf(displayDevice);

        while (User32.EnumDisplayDevices(null, deviceIdx, ref displayDevice, 0))
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (User32.EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                devices.Add((displayDevice.DeviceName, devMode));
            }

            deviceIdx++;
            displayDevice.cb = Marshal.SizeOf(displayDevice);
        }

        var targetDevice = devices.FirstOrDefault(d => d.name == primaryDeviceName);
        if (targetDevice.name == null)
        {
            // Primary monitor not in our device list, bail out.
            return;
        }

        var offsetX = -targetDevice.mode.dmPositionX;
        var offsetY = -targetDevice.mode.dmPositionY;

        foreach (var (name, mode) in devices)
        {
            var devMode = mode;
            devMode.dmPositionX += offsetX;
            devMode.dmPositionY += offsetY;
            devMode.dmFields = DM_POSITION;

            uint flags = CDS_UPDATEREGISTRY | CDS_NORESET;
            if (name == primaryDeviceName)
            {
                flags |= CDS_SET_PRIMARY;
            }

            _ = User32.ChangeDisplaySettingsEx(name, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
        }

        // Apply all staged changes, use this version to set lpDevMode to NULL
        _ = User32.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
    }
}
