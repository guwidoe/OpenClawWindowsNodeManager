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
    private readonly ContextMenuStrip _menu;
    private readonly TrayMenuDismissFilter _menuDismissFilter;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _busyItem;
    private readonly ToolStripMenuItem _connectItem;
    private readonly ToolStripMenuItem _disconnectItem;
    private readonly Dictionary<NodeConnectionState, Icon> _icons;
    private NodeConnectionState _lastState = NodeConnectionState.Unknown;
    private NodeConnectionState _lastNotifiedState = NodeConnectionState.Unknown;
    private DateTimeOffset _lastNotificationAt = DateTimeOffset.MinValue;
    private readonly TimeSpan _notificationCooldown = TimeSpan.FromSeconds(30);
    private bool _isBusy;
    private bool _notificationsEnabled = true;

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

        _busyItem = new ToolStripMenuItem("Working...")
        {
            Enabled = false,
            Visible = false
        };

        _connectItem = new ToolStripMenuItem("Connect", null, async (_, _) => await onConnect());
        _disconnectItem = new ToolStripMenuItem("Disconnect", null, async (_, _) => await onDisconnect());

        var settingsItem = new ToolStripMenuItem("Open Settings", null, (_, _) => onOpenSettings());
        var logsItem = new ToolStripMenuItem("Open Logs", null, (_, _) => onOpenLogs());
        var controlUiItem = new ToolStripMenuItem("Open Control UI", null, (_, _) => onOpenControlUi());
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => onQuit());

        _menu = new ContextMenuStrip();
        _menu.AutoClose = true;
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(_busyItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_connectItem);
        _menu.Items.Add(_disconnectItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(logsItem);
        _menu.Items.Add(controlUiItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(quitItem);

        _menuDismissFilter = new TrayMenuDismissFilter(_menu);
        _menu.Opened += (_, _) => Application.AddMessageFilter(_menuDismissFilter);
        _menu.Closed += (_, _) => Application.RemoveMessageFilter(_menuDismissFilter);

        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Right)
            {
                _menu.Show(Cursor.Position);
            }
        };
        _notifyIcon.DoubleClick += async (_, _) => await onToggle();
    }

    public void UpdateStatus(NodeStatus status)
    {
        if (_isBusy)
        {
            _busyItem.Visible = false;
            _isBusy = false;
        }

        var state = status.ToConnectionState();
        _statusItem.Text = $"Status: {state}";
        var tooltip = status.GatewayHost == null
            ? $"OpenClaw: {state}"
            : $"OpenClaw: {state} ({status.GatewayHost})";
        SetNotifyIconText(tooltip);

        _connectItem.Enabled = !status.IsRunning;
        _disconnectItem.Enabled = status.IsRunning;

        if (_icons.TryGetValue(state, out var icon))
        {
            _notifyIcon.Icon = icon;
        }

        if (_lastState != state)
        {
            if (ShouldNotify(state))
            {
                _notifyIcon.BalloonTipTitle = "OpenClaw";
                _notifyIcon.BalloonTipText = $"Status changed: {state}";
                _notifyIcon.ShowBalloonTip(2000);
                _lastNotificationAt = DateTimeOffset.UtcNow;
                _lastNotifiedState = state;
            }

            _lastState = state;
        }
    }

    public void SetBusy(string message)
    {
        _isBusy = true;
        _busyItem.Text = message;
        _busyItem.Visible = true;

        var busyState = NodeConnectionState.Connecting;
        _statusItem.Text = $"Status: {message}";
        SetNotifyIconText($"OpenClaw: {message}");
        _connectItem.Enabled = false;
        _disconnectItem.Enabled = false;

        if (_icons.TryGetValue(busyState, out var icon))
        {
            _notifyIcon.Icon = icon;
        }
    }

    public void SetNotificationsEnabled(bool enabled)
    {
        _notificationsEnabled = enabled;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        Application.RemoveMessageFilter(_menuDismissFilter);
        _menu.Dispose();
        _notifyIcon.Dispose();
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
    }

    private void SetNotifyIconText(string text)
    {
        const int maxLength = 63;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength);
        }

        _notifyIcon.Text = text;
    }

    private bool ShouldNotify(NodeConnectionState state)
    {
        if (!_notificationsEnabled)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastNotificationAt < _notificationCooldown)
        {
            return false;
        }

        if (state == NodeConnectionState.Error && _lastNotifiedState == NodeConnectionState.Error)
        {
            return false;
        }

        return true;
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

    private sealed class TrayMenuDismissFilter : IMessageFilter
    {
        private readonly ContextMenuStrip _menu;

        public TrayMenuDismissFilter(ContextMenuStrip menu)
        {
            _menu = menu;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!_menu.Visible)
            {
                return false;
            }

            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_RBUTTONDOWN = 0x0204;
            const int WM_MBUTTONDOWN = 0x0207;

            if (m.Msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                var position = Control.MousePosition;
                if (!_menu.Bounds.Contains(position))
                {
                    _menu.Close(ToolStripDropDownCloseReason.AppClicked);
                }
            }

            return false;
        }
    }
}
