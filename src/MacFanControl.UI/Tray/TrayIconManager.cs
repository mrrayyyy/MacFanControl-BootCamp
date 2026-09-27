using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacFanControl.Core.Models;

namespace MacFanControl.UI.Tray;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action _onOpenWindow;
    private readonly Action _onOpenSettings;
    private readonly Action<FanMode> _onSetMode;
    private readonly Action _onExitApp;
    private IntPtr _lastHIcon = IntPtr.Zero;

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

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MacFanControl BootCamp"
        };

        _notifyIcon.MouseDoubleClick += (s, e) => _onOpenWindow();
        _notifyIcon.ContextMenuStrip = CreateContextMenu();

        UpdateTemperatureIcon(50f, 48f);
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

            string cpuText = cpuInt.ToString();
            string gpuText = gpuInt.ToString();

            // CPU Color threshold
            Color cpuColor = cpuInt < 60
                ? Color.FromArgb(76, 217, 100)   // Green
                : cpuInt < 78
                    ? Color.FromArgb(255, 149, 0)  // Orange
                    : Color.FromArgb(255, 59, 48);   // Red

            // GPU Color threshold
            Color gpuColor = gpuInt < 60
                ? Color.FromArgb(0, 220, 255)   // Cyan / Cool Blue
                : gpuInt < 75
                    ? Color.FromArgb(255, 175, 40) // Amber
                    : Color.FromArgb(255, 65, 54);  // Red

            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            // Draw dark rounded pill background
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 31, 31), 6);
            using var bgBrush = new SolidBrush(Color.FromArgb(240, 16, 17, 22));
            g.FillPath(bgBrush, path);

            using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
            g.DrawPath(borderPen, path);

            // Subtle divider line between CPU (top) and GPU (bottom)
            using var divPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1);
            g.DrawLine(divPen, 3, 16, 28, 16);

            var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Font sizing: compact if 3 digits
            float cpuFontSize = cpuText.Length >= 3 ? 9.5f : 12f;
            float gpuFontSize = gpuText.Length >= 3 ? 9.5f : 12f;

            // 1. Draw CPU Temp on TOP row
            using (var cpuFont = new Font("Segoe UI", cpuFontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var cpuBrush = new SolidBrush(cpuColor))
            {
                g.DrawString(cpuText, cpuFont, cpuBrush, new RectangleF(0, 0.5f, 32, 15), stringFormat);
            }

            // 2. Draw GPU Temp on BOTTOM row
            using (var gpuFont = new Font("Segoe UI", gpuFontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var gpuBrush = new SolidBrush(gpuColor))
            {
                g.DrawString(gpuText, gpuFont, gpuBrush, new RectangleF(0, 16.5f, 32, 15), stringFormat);
            }

            IntPtr hIcon = bmp.GetHicon();
            var icon = Icon.FromHandle(hIcon);

            _notifyIcon.Icon = icon;

            string tooltip = $"MacFanControl: CPU {cpuInt}°C | GPU {gpuInt}°C";
            if (tooltip.Length >= 64) tooltip = tooltip.Substring(0, 63);
            _notifyIcon.Text = tooltip;

            // Prevent GDI resource leaks
            if (_lastHIcon != IntPtr.Zero)
            {
                DestroyIcon(_lastHIcon);
            }
            _lastHIcon = hIcon;
        }
        catch
        {
            // Ignore temporary GDI draw glitches
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
    {
        var path = new GraphicsPath();
        int diameter = cornerRadius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        // Top left
        path.AddArc(arc, 180, 90);

        // Top right
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_lastHIcon != IntPtr.Zero)
        {
            DestroyIcon(_lastHIcon);
            _lastHIcon = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
