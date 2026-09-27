using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacFanControl.Core.Models;

namespace MacFanControl.UI.Tray;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _cpuNotifyIcon;
    private readonly NotifyIcon _gpuNotifyIcon;
    private readonly Action _onOpenWindow;
    private readonly Action _onOpenSettings;
    private readonly Action<FanMode> _onSetMode;
    private readonly Action _onExitApp;
    private IntPtr _lastCpuHIcon = IntPtr.Zero;
    private IntPtr _lastGpuHIcon = IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayIconManager(
        Action onOpenWindow,
        Action onOpenSettings,
        Action<FanMode> onSetMode,
        Action onExitApp)
    {
        _onOpenWindow = onOpenWindow;
        _onOpenSettings = onOpenSettings;
        _onSetMode = onSetMode;
        _onExitApp = onExitApp;

        // 1. CPU Tray Icon (Max Core)
        _cpuNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MacFanControl: CPU Max"
        };
        _cpuNotifyIcon.MouseClick += HandleIconClick;
        _cpuNotifyIcon.MouseDoubleClick += (s, e) => _onOpenWindow();
        _cpuNotifyIcon.ContextMenuStrip = CreateContextMenu();

        // 2. GPU Tray Icon (Hotspot)
        _gpuNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MacFanControl: GPU Hotspot"
        };
        _gpuNotifyIcon.MouseClick += HandleIconClick;
        _gpuNotifyIcon.MouseDoubleClick += (s, e) => _onOpenWindow();
        _gpuNotifyIcon.ContextMenuStrip = CreateContextMenu();

        // Initial default render
        UpdateTemperatureIcon(50f, 48f);
    }

    private void HandleIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _onOpenWindow();
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open Fan Control Dashboard");
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        openItem.Click += (s, e) => _onOpenWindow();
        menu.Items.Add(openItem);

        var settingsItem = new ToolStripMenuItem("⚙️ Settings & Startup Options...");
        settingsItem.Click += (s, e) => _onOpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        var autoItem = new ToolStripMenuItem("Mode: Apple Default (Auto)");
        autoItem.Click += (s, e) => _onSetMode(FanMode.AppleAuto);
        menu.Items.Add(autoItem);

        var curveItem = new ToolStripMenuItem("Mode: Temperature Control");
        curveItem.Click += (s, e) => _onSetMode(FanMode.Curve);
        menu.Items.Add(curveItem);

        var turboItem = new ToolStripMenuItem("Mode: 100% Turbo Max");
        turboItem.Click += (s, e) => _onSetMode(FanMode.Turbo);
        menu.Items.Add(turboItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit MacFanControl");
        exitItem.Click += (s, e) => _onExitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void UpdateTemperatureIcon(float cpuTemp, float gpuTemp)
    {
        try
        {
            int cpuInt = Math.Clamp((int)Math.Round(cpuTemp), 0, 999);
            int gpuInt = Math.Clamp((int)Math.Round(gpuTemp), 0, 999);

            // CPU Color: Green (<60) -> Amber (60-78) -> Red (>=79)
            Color cpuColor = cpuInt < 60
                ? Color.FromArgb(76, 217, 100)    // Crisp Green
                : cpuInt < 79
                    ? Color.FromArgb(255, 159, 10)  // Warm Amber
                    : Color.FromArgb(255, 69, 58);   // Vibrant Red

            // GPU Color: Cyan/Cool Blue (<60) -> Amber (60-75) -> Red (>=75)
            Color gpuColor = gpuInt < 60
                ? Color.FromArgb(10, 215, 255)   // Vibrant Cyan
                : gpuInt < 75
                    ? Color.FromArgb(255, 180, 40)  // Warm Amber
                    : Color.FromArgb(255, 69, 58);   // Vibrant Red

            // 1. Render CPU Icon (Transparent background, bold colored number)
            var (cpuIcon, cpuHIcon) = CreateNumericIcon(cpuInt, cpuColor);
            var oldCpuIcon = _cpuNotifyIcon.Icon;
            _cpuNotifyIcon.Icon = cpuIcon;
            oldCpuIcon?.Dispose();

            string cpuTip = $"CPU Max: {cpuInt}°C (MacFanControl)";
            if (cpuTip.Length >= 64) cpuTip = cpuTip.Substring(0, 63);
            _cpuNotifyIcon.Text = cpuTip;

            if (_lastCpuHIcon != IntPtr.Zero)
            {
                DestroyIcon(_lastCpuHIcon);
            }
            _lastCpuHIcon = cpuHIcon;

            // 2. Render GPU Icon (Transparent background, bold colored number)
            var (gpuIcon, gpuHIcon) = CreateNumericIcon(gpuInt, gpuColor);
            var oldGpuIcon = _gpuNotifyIcon.Icon;
            _gpuNotifyIcon.Icon = gpuIcon;
            oldGpuIcon?.Dispose();

            string gpuTip = $"GPU Hotspot: {gpuInt}°C (MacFanControl)";
            if (gpuTip.Length >= 64) gpuTip = gpuTip.Substring(0, 63);
            _gpuNotifyIcon.Text = gpuTip;

            if (_lastGpuHIcon != IntPtr.Zero)
            {
                DestroyIcon(_lastGpuHIcon);
            }
            _lastGpuHIcon = gpuHIcon;
        }
        catch
        {
            // Ignore temporary GDI draw glitches
        }
    }

    private static (Icon icon, IntPtr hIcon) CreateNumericIcon(int tempValue, Color textColor)
    {
        const int size = 32;
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            string text = tempValue.ToString();

            // Font sizing:
            // 1 digit (0-9)    -> 22px
            // 2 digits (10-99) -> 18px (fits completely in 32px canvas without clipping)
            // 3 digits (100+)  -> 13px
            float fontSize = text.Length switch
            {
                1 => 22f,
                2 => 18f,
                _ => 13f
            };

            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var rect = new RectangleF(0, 0, size, size);

            // Subtle dark outline/shadow (1px offset) to ensure 100% visibility on both dark and light taskbars
            using (var shadowBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                g.DrawString(text, font, shadowBrush, new RectangleF(-1f, 0f, size, size), sf);
                g.DrawString(text, font, shadowBrush, new RectangleF(1f, 0f, size, size), sf);
                g.DrawString(text, font, shadowBrush, new RectangleF(0f, -1f, size, size), sf);
                g.DrawString(text, font, shadowBrush, new RectangleF(0f, 1f, size, size), sf);
            }

            // Draw crisp colored number
            using var textBrush = new SolidBrush(textColor);
            g.DrawString(text, font, textBrush, rect, sf);
        }

        IntPtr hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        bmp.Dispose();
        return (icon, hIcon);
    }

    public void Dispose()
    {
        _cpuNotifyIcon.Visible = false;
        var oldCpu = _cpuNotifyIcon.Icon;
        _cpuNotifyIcon.Dispose();
        oldCpu?.Dispose();

        _gpuNotifyIcon.Visible = false;
        var oldGpu = _gpuNotifyIcon.Icon;
        _gpuNotifyIcon.Dispose();
        oldGpu?.Dispose();

        if (_lastCpuHIcon != IntPtr.Zero)
        {
            DestroyIcon(_lastCpuHIcon);
            _lastCpuHIcon = IntPtr.Zero;
        }

        if (_lastGpuHIcon != IntPtr.Zero)
        {
            DestroyIcon(_lastGpuHIcon);
            _lastGpuHIcon = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
