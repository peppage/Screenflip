using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Svg;

namespace ScreenFlip;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal class MainForm : Form
{
    private static readonly string StateFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenFlip", "state.txt");

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenFlip", "settings.txt");

    private static readonly Color _colorDarkTeal = Color.FromArgb(23, 41, 45);
    private static readonly Color _colorAqua = Color.FromArgb(125, 249, 255);
    private static readonly Color _colorInkBlack = Color.FromArgb(15, 17, 23);

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _trayToggleItem;
    private readonly Button _toggleButton;
    private readonly Panel _accentBar;
    private readonly Label _statusDot;
    private readonly Label _statusLabel;
    private readonly CheckBox _overlayCheckBox;
    private bool _isActive;
    private OverlayForm _overlay;
    private bool _exitRequested;

    public MainForm()
    {
        Text = "ScreenFlip";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(280, 140);
        BackColor = _colorInkBlack;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = BuildIcon();

        _accentBar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(4, 44),
            BackColor = _colorDarkTeal,
        };

        _statusDot = new Label
        {
            Text = "●",
            Font = new Font("Segoe UI", 12f),
            AutoSize = true,
            Location = new Point(14, 10),
            ForeColor = _colorDarkTeal,
            BackColor = Color.Transparent,
        };

        _statusLabel = new Label
        {
            Text = "Inactive",
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(36, 13),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
        };

        _toggleButton = new Button
        {
            Text = "Enable",
            Font = new Font("Segoe UI", 10f),
            Size = new Size(248, 36),
            Location = new Point(16, 48),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            BackColor = _colorDarkTeal,
        };
        _toggleButton.FlatAppearance.BorderSize = 0;
        _toggleButton.Click += (s, e) => Toggle();

        var separator = new Panel
        {
            Location = new Point(16, 94),
            Size = new Size(248, 1),
            BackColor = Color.FromArgb(220, 220, 220),
        };

        _overlayCheckBox = new CheckBox
        {
            Text = "Show overlay on inactive monitor",
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(16, 102),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Checked = LoadOverlaySetting(),
        };
        _overlayCheckBox.CheckedChanged += (s, e) => SaveSettings();

        Controls.AddRange(_accentBar, _statusDot, _statusLabel, _toggleButton, separator, _overlayCheckBox);

        _trayToggleItem = new ToolStripMenuItem("Enable", null, (s, e) => Toggle());

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(_trayToggleItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Open", null, (s, e) => OpenWindow());
        trayMenu.Items.Add("Exit", null, (s, e) => RequestExit());

        _trayIcon = new NotifyIcon
        {
            Text = "ScreenFlip - Inactive",
            Icon = BuildIcon(size: 16),
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (s, e) => OpenWindow();
    }

    /// <summary>
    /// Toggles the screen flip on or off.
    /// </summary>
    private void Toggle()
    {
        if (_isActive)
        {
            DeactivateScreenFlip();
        }
        else
        {
            ActivateScreenFlip();
        }
    }

    private void ActivateScreenFlip()
    {
        var screens = Screen.AllScreens;
        if (screens.Length < 2)
        {
            MessageBox.Show("Only one monitor detected.", "ScreenFlip",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var currentPrimary = screens.First(s => s.Primary);
        var otherScreen = screens.First(s => !s.Primary);

        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, currentPrimary.DeviceName);

        // SetPrimaryMonitor shifts all monitors by (-otherScreen.X, -otherScreen.Y).
        // Pre-calculate where the old primary will land after the swap.
        var overlayBounds = new Rectangle(
            currentPrimary.Bounds.X - otherScreen.Bounds.X,
            currentPrimary.Bounds.Y - otherScreen.Bounds.Y,
            currentPrimary.Bounds.Width,
            currentPrimary.Bounds.Height);

        SetPrimaryMonitor(otherScreen.DeviceName);

        if (_overlayCheckBox.Checked)
        {
            _overlay = new OverlayForm(overlayBounds);
            _overlay.Show();
        }

        _isActive = true;
        RefreshUI();
    }

    private void DeactivateScreenFlip()
    {
        _overlay?.Close();
        _overlay = null;

        if (File.Exists(StateFile))
        {
            var original = File.ReadAllText(StateFile).Trim();
            if (!string.IsNullOrEmpty(original))
            {
                SetPrimaryMonitor(original);
            }
            File.Delete(StateFile);
        }

        _isActive = false;
        RefreshUI();
    }

    private void RefreshUI()
    {
        var state = _isActive ? "Active" : "Inactive";
        var action = _isActive ? "Disable" : "Enable";

        var statusColor = _colorDarkTeal;
        var statusColorText = Color.White;
        if (_isActive)
        {
            statusColor = _colorAqua;
            statusColorText = _colorInkBlack;
        }

        _accentBar.BackColor = statusColor;
        _statusDot.ForeColor = statusColor;
        _statusLabel.Text = state;
        _toggleButton.Text = action;
        _toggleButton.ForeColor = statusColorText;
        _toggleButton.BackColor = statusColor;
        _trayToggleItem.Text = action;
        _trayIcon.Text = $"ScreenFlip - {state}";
    }

    private static bool LoadOverlaySetting()
    {
        if (!File.Exists(SettingsFile))
        {
            return true;
        }

        var line = File.ReadAllText(SettingsFile).Trim();
        return !line.Equals("overlay=false", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, _overlayCheckBox.Checked ? "overlay=true" : "overlay=false");
    }

    private void OpenWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        base.Activate();
    }

    private void RequestExit()
    {
        _exitRequested = true;

        if (_isActive)
        {
            DeactivateScreenFlip();
        }

        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    #region icon drawing

    private static Icon BuildIcon(int size = 32)
    {
        using var stream = typeof(MainForm).Assembly
            .GetManifestResourceStream("ScreenFlip.screenflip-logo.svg")!;
        var svgDoc = SvgDocument.Open<SvgDocument>(stream);
        var bmp = svgDoc.Draw(size, size);
        return Icon.FromHandle(bmp.GetHicon());
    }

    #endregion icon drawing

    #region Win32

    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint CDS_SET_PRIMARY = 0x00000010;
    private const uint CDS_UPDATEREGISTRY = 0x00000001;
    private const uint CDS_NORESET = 0x10000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    private static void SetPrimaryMonitor(string primaryDeviceName)
    {
        var devices = new List<(string name, DEVMODE mode)>();
        uint deviceIdx = 0;

        var displayDevice = new DISPLAY_DEVICE();
        displayDevice.cb = Marshal.SizeOf(displayDevice);

        while (EnumDisplayDevices(null, deviceIdx, ref displayDevice, 0))
        {
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(devMode);

            if (EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
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
            devMode.dmFields = 0x00000020; // DM_POSITION

            uint flags = CDS_UPDATEREGISTRY | CDS_NORESET;
            if (name == primaryDeviceName)
            {
                flags |= CDS_SET_PRIMARY;
            }

            _ = ChangeDisplaySettingsEx(name, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
        }

        // Apply all staged changes, use this version to set lpDevMode to NULL
        _ = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
    }

    #endregion Win32
}

/// <summary>
/// Represents a borderless, semi-transparent overlay form that dims the screen.
/// </summary>
/// <remarks>This form is intended to be displayed above all other windows
/// and does not appear in the taskbar. Allows it be readable but
/// less distracting for gaming.</remarks>
internal class OverlayForm : Form
{
    private readonly Timer _clockTimer;
    private readonly Label _clockLabel;

    public OverlayForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        Opacity = 0.90; // Could add a slider to control the dim but needed?
        TopMost = true;
        ShowInTaskbar = false;

        // Add label to remind why the screen is dark.
        // Maybe needs an option to disable the label?
        var label = new Label
        {
            Text = "Screen dimmed - use ScreenFlip to restore",
            ForeColor = Color.FromArgb(100, 255, 255, 255),
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point((bounds.Width - 360) / 2, bounds.Height - 40)
        };

        _clockLabel = new Label
        {
            Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ForeColor = Color.FromArgb(160, 255, 255, 255),
            Font = new Font("Segoe UI", 48f, FontStyle.Regular),
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point((bounds.Width - 310) / 2, bounds.Height - 120)
        };

        Controls.Add(label);
        Controls.Add(_clockLabel);

        _clockTimer = new Timer { Interval = 1000 };
        _clockTimer.Tick += (s, e) => _clockLabel.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        _clockTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return createParams;
        }
    }
}
