using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Teine64;

/// <summary>
/// Custom ApplicationContext hosting a NotifyIcon for the tray-based UX.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly AwakeService _awakeService;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly Icon _awakeIcon;
    private readonly Icon _pausedIcon;

    public TrayApplicationContext(bool startPaused)
    {
        _awakeIcon = LoadIcon("Resources/awake.ico");
        _pausedIcon = LoadIcon("Resources/paused.ico");

        _awakeService = new AwakeService(!startPaused);

        _toggleItem = new ToolStripMenuItem("Pause Keeping Awake", null, (_, _) => Toggle())
        {
            Checked = startPaused,
            CheckOnClick = false
        };

        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        _icon = new NotifyIcon
        {
            Icon = startPaused ? _pausedIcon : _awakeIcon,
            Text = GetTooltipText(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _icon.ContextMenuStrip.Items.Add(_toggleItem);
        _icon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _icon.ContextMenuStrip.Items.Add(exitItem);

        _icon.DoubleClick += (_, _) => Toggle();
    }

    private static Icon LoadIcon(string relativePath)
    {
        var full = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full))
        {
            return new Icon(full);
        }
        // Fallback: generate a placeholder icon dynamically.
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.Lime, 2, 2, 12, 12);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void Toggle()
    {
        bool newState = _awakeService.Enabled; // currently enabled -> we'll invert
        _awakeService.SetEnabled(!newState);
        _toggleItem.Checked = newState; // If it was enabled, after toggle it's paused -> checked means paused.
        _toggleItem.Text = newState ? "Resume Keeping Awake" : "Pause Keeping Awake";
        _icon.Icon = _awakeService.Enabled ? _awakeIcon : _pausedIcon;
        _icon.Text = GetTooltipText();
    }

    private string GetTooltipText() => _awakeService.Enabled ? "Teine64: Preventing sleep" : "Teine64: Paused";

    private void ExitApplication()
    {
        _icon.Visible = false;
        _awakeService.Dispose();
        _icon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _awakeService.Dispose();
            _awakeIcon?.Dispose();
            _pausedIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}