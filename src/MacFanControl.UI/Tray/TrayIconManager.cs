using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacFanControl.Core.Models;

namespace MacFanControl.UI.Tray;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action _onOpenWindow;
    private readonly Action<FanMode> _onSetMode;
    private readonly Action _onExitApp;
    private IntPtr _lastHIcon = IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayIconManager(
        Action onOpenWindow,
        Action<FanMode> onSetMode,
        Action onExitApp)
    {
        _onOpenWindow = onOpenWindow;
        _onSetMode = onSetMode;
        _onExitApp = onExitApp;

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "MacFanControl (MBP 16\" 2019)"
        };

        _notifyIcon.MouseDoubleClick += (s, e) => _onOpenWindow();
        _notifyIcon.ContextMenuStrip = CreateContextMenu();

        UpdateTemperatureIcon(55f);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open Dashboard");
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (s, e) => _onOpenWindow();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        var autoItem = new ToolStripMenuItem("Mode: Apple Default (Auto)");
        autoItem.Click += (s, e) => _onSetMode(FanMode.AppleAuto);
        menu.Items.Add(autoItem);

        var curveItem = new ToolStripMenuItem("Mode: Custom Curve");
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

    public void UpdateTemperatureIcon(float temp)
    {
        try
        {
            int displayTemp = (int)Math.Round(temp);
            string text = displayTemp.ToString();

            // Background color depends on temperature severity
            Color textColor;
            if (displayTemp < 60)
                textColor = Color.FromArgb(76, 217, 100);  // Green
            else if (displayTemp < 78)
                textColor = Color.FromArgb(255, 149, 0);  // Orange
            else
                textColor = Color.FromArgb(255, 59, 48);   // Red

            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);

            // Draw rounded badge background
            using var bgBrush = new SolidBrush(Color.FromArgb(220, 24, 24, 28));
            g.FillEllipse(bgBrush, 0, 0, 31, 31);

            // Draw temperature text
            using var font = new Font("Segoe UI", text.Length > 2 ? 14 : 17, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(textColor);

            var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(text, font, textBrush, new RectangleF(0, 1, 32, 32), stringFormat);

            IntPtr hIcon = bmp.GetHicon();
            var icon = Icon.FromHandle(hIcon);

            _notifyIcon.Icon = icon;
            _notifyIcon.Text = $"MacFanControl: CPU {displayTemp}°C";

            // Clean up old GDI icon handle to prevent GDI leak
            if (_lastHIcon != IntPtr.Zero)
            {
                DestroyIcon(_lastHIcon);
            }
            _lastHIcon = hIcon;
        }
        catch
        {
            // Ignore GDI render glitches
        }
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
