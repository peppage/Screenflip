using System.Runtime.InteropServices;

// Blittable so the LibraryImport source generator can marshal it (no ByValTStr).
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DISPLAY_DEVICE
{
    public int cb;

    private fixed char _deviceName[32];
    private fixed char _deviceString[128];

    public int StateFlags;

    private fixed char _deviceID[128];
    private fixed char _deviceKey[128];

    public string DeviceName
    {
        get
        {
            fixed (char* p = _deviceName)
            {
                return new string(p);
            }
        }
    }
}
