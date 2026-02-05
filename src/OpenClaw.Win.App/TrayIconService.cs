using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.App;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _connectItem;
    private readonly ToolStripMenuItem _disconnectItem;
    private readonly Dictionary<NodeConnectionState, Icon> _icons;
    private NodeConnectionState _lastState = NodeConnectionState.Unknown;

    public TrayIconService(
        Func<Task> onConnect,
        Func<Task> onDisconnect,
        Func<Task> onToggle,
        Action onOpenSettings,
        Action onOpenLogs,
        Action onOpenControlUi,
        Action onQuit)
    {
        _icons = new Dictionary<NodeConnectionState, Icon>
        {
            [NodeConnectionState.Disconnected] = CreateCircleIcon(Color.Gray),
            [NodeConnectionState.Connecting] = CreateCircleIcon(Color.DodgerBlue),
            [NodeConnectionState.Connected] = CreateCircleIcon(Color.LimeGreen),
            [NodeConnectionState.Degraded] = CreateCircleIcon(Color.Goldenrod),
            [NodeConnectionState.Error] = CreateCircleIcon(Color.IndianRed),
            [NodeConnectionState.Unknown] = CreateCircleIcon(Color.LightSlateGray)
        };

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = _icons[NodeConnectionState.Disconnected],
            Text = "OpenClaw: Disconnected"
        };

        _statusItem = new ToolStripMenuItem("Status: Disconnected")
        {
            Enabled = false
        };

        _connectItem = new ToolStripMenuItem("Connect", null, async (_, _) => await onConnect());
        _disconnectItem = new ToolStripMenuItem("Disconnect", null, async (_, _) => await onDisconnect());

        var settingsItem = new ToolStripMenuItem("Open Settings", null, (_, _) => onOpenSettings());
        var logsItem = new ToolStripMenuItem("Open Logs", null, (_, _) => onOpenLogs());
        var controlUiItem = new ToolStripMenuItem("Open Control UI", null, (_, _) => onOpenControlUi());
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => onQuit());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_connectItem);
        menu.Items.Add(_disconnectItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(controlUiItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += async (_, _) => await onToggle();
    }

    public void UpdateStatus(NodeStatus status)
    {
        var state = status.ToConnectionState();
        _statusItem.Text = $"Status: {state}";
        _notifyIcon.Text = status.GatewayHost == null
            ? $"OpenClaw: {state}"
            : $"OpenClaw: {state} ({status.GatewayHost})";

        _connectItem.Enabled = !status.IsRunning;
        _disconnectItem.Enabled = status.IsRunning;

        if (_icons.TryGetValue(state, out var icon))
        {
            _notifyIcon.Icon = icon;
        }

        if (_lastState != state)
        {
            _notifyIcon.BalloonTipTitle = "OpenClaw";
            _notifyIcon.BalloonTipText = $"Status changed: {state}";
            _notifyIcon.ShowBalloonTip(2000);
            _lastState = state;
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
    }

    private static Icon CreateCircleIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bmp);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 1, 1, 14, 14);

        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
