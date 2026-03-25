using System;
using System.Windows;
using System.Windows.Forms;
using FluentClip.Models;

namespace FluentClip.Services;

public class SystemTrayManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _monitorMenuItem;
    private ToolStripMenuItem? _dragActionMenuItem;
    private bool _isPinned;
    private AppSettings _settings;
    private readonly Action _showWindowAction;
    private readonly Action _forceCloseAction;
    private readonly Action _togglePinAction;
    private readonly Action<bool> _setMonitoringAction;

    public SystemTrayManager(
        AppSettings settings,
        bool isPinned,
        Action showWindowAction,
        Action forceCloseAction,
        Action togglePinAction,
        Action<bool> setMonitoringAction)
    {
        _settings = settings;
        _isPinned = isPinned;
        _showWindowAction = showWindowAction;
        _forceCloseAction = forceCloseAction;
        _togglePinAction = togglePinAction;
        _setMonitoringAction = setMonitoringAction;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Icon = new System.Drawing.Icon(
            System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/clip.ico")).Stream);
        _notifyIcon.Text = "FluentClip";
        _notifyIcon.Visible = true;

        var contextMenu = new ContextMenuStrip();

        contextMenu.Items.Add("打开", null, (s, e) => _showWindowAction());
        contextMenu.Items.Add("-");

        _monitorMenuItem = new ToolStripMenuItem("监控剪贴板");
        _monitorMenuItem.Checked = _settings.MonitorClipboard;
        _monitorMenuItem.Click += (s, e) =>
        {
            _settings.MonitorClipboard = !_settings.MonitorClipboard;
            _monitorMenuItem.Checked = _settings.MonitorClipboard;
            _settings.Save();
            _setMonitoringAction(_settings.MonitorClipboard);
        };
        contextMenu.Items.Add(_monitorMenuItem);

        _dragActionMenuItem = CreateDragActionMenuItem();
        contextMenu.Items.Add(_dragActionMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var pinMenuItem = new ToolStripMenuItem(_isPinned ? "取消置顶" : "置顶");
        pinMenuItem.Click += (s, e) =>
        {
            _togglePinAction();
            pinMenuItem.Text = _isPinned ? "取消置顶" : "置顶";
        };
        contextMenu.Items.Add(pinMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (s, e) => _forceCloseAction());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => _showWindowAction();
    }

    private ToolStripMenuItem CreateDragActionMenuItem()
    {
        var dragActionMenuItem = new ToolStripMenuItem("拖入文件行为");
        var copyItem = new ToolStripMenuItem(_settings.DragActionCopy ? "✓ 仅保存副本（复制）" : "仅保存副本（复制）");
        var cutItem = new ToolStripMenuItem(_settings.DragActionCopy ? "剪切原文件" : "✓ 剪切原文件");

        copyItem.Click += (s, e) =>
        {
            _settings.DragActionCopy = true;
            UpdateDragActionMenuTexts(dragActionMenuItem, true);
            _settings.Save();
        };

        cutItem.Click += (s, e) =>
        {
            _settings.DragActionCopy = false;
            UpdateDragActionMenuTexts(dragActionMenuItem, false);
            _settings.Save();
        };

        dragActionMenuItem.DropDownItems.Add(copyItem);
        dragActionMenuItem.DropDownItems.Add(cutItem);

        return dragActionMenuItem;
    }

    private void UpdateDragActionMenuTexts(ToolStripMenuItem parent, bool isCopy)
    {
        if (parent.DropDownItems.Count >= 2)
        {
            parent.DropDownItems[0].Text = isCopy ? "✓ 仅保存副本（复制）" : "仅保存副本（复制）";
            parent.DropDownItems[1].Text = isCopy ? "剪切原文件" : "✓ 剪切原文件";
        }
    }

    public void UpdatePinState(bool isPinned)
    {
        _isPinned = isPinned;
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
