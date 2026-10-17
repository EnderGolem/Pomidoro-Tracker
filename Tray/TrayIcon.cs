using System.Drawing;
using System.Windows.Forms;
using Pomidoro.Localization;

namespace Pomidoro.Tray;

// Через значок в области уведомлений приложение живёт, когда окно закрыто крестиком.
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _open;
    private readonly ToolStripMenuItem _exit;

    public TrayIcon()
    {
        _icon = TomatoIcon.CreateForTray();

        _open = new ToolStripMenuItem();
        _open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _exit = new ToolStripMenuItem();
        _exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new ContextMenuStrip
        {
            Items = { _open, new ToolStripSeparator(), _exit }
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.MouseClick += OnMouseClick;
        _notifyIcon.Text = Loc.S.AppTitle;

        _open.Text = Loc.S.TrayOpen;
        _exit.Text = Loc.S.TrayExit;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        // Правую кнопку не трогаем — на ней контекстное меню.
        if (e.Button == MouseButtons.Left)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
