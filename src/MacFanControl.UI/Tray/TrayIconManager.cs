using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacFanControl.Core.Models;

namespace MacFanControl.UI.Tray;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _cpuTempNotifyIcon;
    private readonly NotifyIcon _gpuTempNotifyIcon;
    private readonly NotifyIcon _cpuUsageNotifyIcon;
    private readonly NotifyIcon _gpuUsageNotifyIcon;

    private readonly Action _onOpenWindow;
    private readonly Action _onOpenSettings;
    private readonly Action<FanMode> _onSetMode;
    private readonly Action _onExitApp;

    private IntPtr _lastCpuTempHIcon = IntPtr.Zero;
    private IntPtr _lastGpuTempHIcon = IntPtr.Zero;
    private IntPtr _lastCpuUsageHIcon = IntPtr.Zero;
    private IntPtr _lastGpuUsageHIcon = IntPtr.Zero;

    private float _lastCpuTemp = 50f;
    private float _lastGpuTemp = 48f;
    private float _lastCpuUsage = 15f;
    private float _lastGpuUsage = 10f;

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

        // 1. CPU Temp Tray Icon (Max Core)
        _cpuTempNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "CPU Temp"
        };
        SetupNotifyIcon(_cpuTempNotifyIcon);

        // 2. GPU Temp Tray Icon (Hotspot)
        _gpuTempNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "GPU Temp"
        };
        SetupNotifyIcon(_gpuTempNotifyIcon);

        // 3. CPU Usage Tray Icon (Load / Process %)
        _cpuUsageNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "CPU Load"
        };
        SetupNotifyIcon(_cpuUsageNotifyIcon);

        // 4. GPU Usage Tray Icon (Load / Process %)
        _gpuUsageNotifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "GPU Load"
        };
        SetupNotifyIcon(_gpuUsageNotifyIcon);

        // Initial default render
        UpdateTrayIcons(_lastCpuTemp, _lastGpuTemp, _lastCpuUsage, _lastGpuUsage);
    }

    private void SetupNotifyIcon(NotifyIcon icon)
    {
        icon.MouseClick += HandleIconClick;
        icon.MouseDoubleClick += (s, e) => _onOpenWindow();
        icon.ContextMenuStrip = CreateContextMenu();
    }

    public void UpdateVisibility(bool showCpuTemp, bool showGpuTemp, bool showCpuUsage, bool showGpuUsage)
    {
        _cpuTempNotifyIcon.Visible = showCpuTemp;
        _gpuTempNotifyIcon.Visible = showGpuTemp;
        _cpuUsageNotifyIcon.Visible = showCpuUsage;
        _gpuUsageNotifyIcon.Visible = showGpuUsage;
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
        UpdateTrayIcons(cpuTemp, gpuTemp, _lastCpuUsage, _lastGpuUsage);
    }

    public void UpdateTrayIcons(float cpuTemp, float gpuTemp, float cpuUsage, float gpuUsage)
    {
        _lastCpuTemp = cpuTemp;
        _lastGpuTemp = gpuTemp;
        _lastCpuUsage = cpuUsage;
        _lastGpuUsage = gpuUsage;

        try
        {
            int cpuTempInt = Math.Clamp((int)Math.Round(cpuTemp), 0, 999);
            int gpuTempInt = Math.Clamp((int)Math.Round(gpuTemp), 0, 999);
            int cpuUsageInt = Math.Clamp((int)Math.Round(cpuUsage), 0, 100);
            int gpuUsageInt = Math.Clamp((int)Math.Round(gpuUsage), 0, 100);

            // 4 Signature Fixed Neon Colors (High-intensity luminous palette, no purple):
            // 1. CPU Temp: Neon Lime Green (#00FF66)
            Color cpuTempColor = Color.FromArgb(0, 255, 102);

            // 2. GPU Temp: Neon Electric Cyan (#00F5FF)
            Color gpuTempColor = Color.FromArgb(0, 245, 255);

            // 3. CPU Usage: Neon Fiery Orange (#FF6E00)
            Color cpuUsageColor = Color.FromArgb(255, 110, 0);

            // 4. GPU Usage: Neon Solar Yellow (#FFE600)
            Color gpuUsageColor = Color.FromArgb(255, 230, 0);

            // Render 1: CPU Temp Icon
            RenderIcon(
                _cpuTempNotifyIcon,
                cpuTempInt,
                cpuTempColor,
                $"CPU Max: {cpuTempInt}°C",
                ref _lastCpuTempHIcon);

            // Render 2: GPU Temp Icon
            RenderIcon(
                _gpuTempNotifyIcon,
                gpuTempInt,
                gpuTempColor,
                $"GPU Hotspot: {gpuTempInt}°C",
                ref _lastGpuTempHIcon);

            // Render 3: CPU Usage Icon
            RenderIcon(
                _cpuUsageNotifyIcon,
                cpuUsageInt,
                cpuUsageColor,
                $"CPU Load: {cpuUsageInt}%",
                ref _lastCpuUsageHIcon);

            // Render 4: GPU Usage Icon
            RenderIcon(
                _gpuUsageNotifyIcon,
                gpuUsageInt,
                gpuUsageColor,
                $"GPU Load: {gpuUsageInt}%",
                ref _lastGpuUsageHIcon);
        }
        catch
        {
            // Ignore temporary GDI draw glitches
        }
    }

    private void RenderIcon(
        NotifyIcon notifyIcon,
        int value,
        Color color,
        string tooltip,
        ref IntPtr lastHIcon)
    {
        var (icon, hIcon) = CreateNumericIcon(value, color);
        var oldIcon = notifyIcon.Icon;
        notifyIcon.Icon = icon;
        oldIcon?.Dispose();

        if (tooltip.Length >= 64)
            tooltip = tooltip.Substring(0, 63);
        notifyIcon.Text = tooltip;

        if (lastHIcon != IntPtr.Zero)
        {
            DestroyIcon(lastHIcon);
        }
        lastHIcon = hIcon;
    }

    private static (Icon icon, IntPtr hIcon) CreateNumericIcon(int value, Color textColor)
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

            string text = value.ToString();

            // Font sizing:
            // 1 digit (0-9)    -> 22px
            // 2 digits (10-99) -> 18px
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
            using (var shadowBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
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
        DisposeIcon(_cpuTempNotifyIcon, ref _lastCpuTempHIcon);
        DisposeIcon(_gpuTempNotifyIcon, ref _lastGpuTempHIcon);
        DisposeIcon(_cpuUsageNotifyIcon, ref _lastCpuUsageHIcon);
        DisposeIcon(_gpuUsageNotifyIcon, ref _lastGpuUsageHIcon);

        GC.SuppressFinalize(this);
    }

    private static void DisposeIcon(NotifyIcon notifyIcon, ref IntPtr lastHIcon)
    {
        notifyIcon.Visible = false;
        var old = notifyIcon.Icon;
        notifyIcon.Dispose();
        old?.Dispose();

        if (lastHIcon != IntPtr.Zero)
        {
            DestroyIcon(lastHIcon);
            lastHIcon = IntPtr.Zero;
        }
    }
}
