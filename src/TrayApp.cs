namespace Rinse;

class TrayApp : ApplicationContext
{
    readonly Settings settings = Settings.Load();
    readonly ClipHistory history = new();
    readonly MessageWindow messages = new();
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 90 };
    readonly ToolStripMenuItem undoItem, autoItem, notifyItem, startupItem;

    PanelForm panel;
    string lastWritten;      // text Rinse itself put on the clipboard (so we don't react to our own writes)
    string undoOriginal;     // the link before the last automatic rinse

    public string HotkeyText => messages.HotkeyText;

    public TrayApp(bool startHidden)
    {
        debounce.Tick += (_, _) => { debounce.Stop(); OnClipboardChanged(); };
        messages.ClipboardChanged += () => { debounce.Stop(); debounce.Start(); };
        messages.HotkeyPressed += ShowPanel;

        var menu = new ContextMenuStrip { Font = Theme.Ui(9.5f), ShowImageMargin = false };
        var open = new ToolStripMenuItem("Open Rinse" + (HotkeyText != null ? $"    {HotkeyText}" : ""), null, (_, _) => ShowPanel())
        { Font = Theme.Ui(9.5f, FontStyle.Bold) };
        undoItem = new ToolStripMenuItem("Undo last rinse", null, (_, _) => UndoLastRinse()) { Enabled = false };
        autoItem = new ToolStripMenuItem("Auto-rinse copied links", null, (_, _) => { settings.AutoRinse = !settings.AutoRinse; settings.Save(); SyncChecks(); });
        notifyItem = new ToolStripMenuItem("Show a notice when a link is rinsed", null, (_, _) => { settings.Notify = !settings.Notify; settings.Save(); SyncChecks(); });
        startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => { Settings.StartWithWindows = !Settings.StartWithWindows; SyncChecks(); });
        menu.Items.AddRange(new ToolStripItem[]
        {
            open, undoItem, new ToolStripSeparator(), autoItem, notifyItem, startupItem,
            new ToolStripSeparator(), new ToolStripMenuItem("Quit Rinse", null, (_, _) => Quit()),
        });
        menu.Opening += (_, _) => SyncChecks();

        tray = new NotifyIcon
        {
            Icon = Theme.AppIcon(SystemInformation.SmallIconSize),
            Text = "Rinse: keeping your clipboard clean",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPanel(); };
        tray.BalloonTipClicked += (_, _) => ShowPanel();
        SyncChecks();

        var current = Clip.GetText();
        if (current != null && !Clip.IsMarkedPrivate()) history.Add(current);

        if (!startHidden) ShowPanel();
        if (!settings.FirstRunDone)
        {
            tray.ShowBalloonTip(6000, "Rinse is in your tray",
                "Copied links get cleaned automatically. " +
                (HotkeyText != null ? $"Press {HotkeyText} or click" : "Click") + " the teal drop for text tools.",
                ToolTipIcon.None);
            settings.FirstRunDone = true;
            settings.Save();
        }
    }

    void SyncChecks()
    {
        autoItem.Checked = settings.AutoRinse;
        notifyItem.Checked = settings.Notify;
        startupItem.Checked = Settings.StartWithWindows;
        panel?.SyncSettings();
    }

    public bool WriteClipboard(string text)
    {
        lastWritten = text;
        return Clip.SetText(text);
    }

    void OnClipboardChanged()
    {
        if (Clip.IsMarkedPrivate()) return;
        var text = Clip.GetText();
        if (text == null) return;
        if (text == lastWritten) { lastWritten = null; return; }

        history.Add(text);

        if (settings.AutoRinse && LinkCleaner.IsSingleUrl(text))
        {
            var original = text.Trim();
            var r = LinkCleaner.Clean(original);
            if (r.Changed && r.Url != original && WriteClipboard(r.Url))
            {
                undoOriginal = text;
                undoItem.Enabled = true;
                history.ReplaceLatest(r.Url);
                settings.LinksRinsed++;
                settings.TrackersRemoved += r.TrackersRemoved;
                settings.Save();
                text = r.Url;
                if (settings.Notify)
                    tray.ShowBalloonTip(2500, "Link rinsed ✨", Describe(r), ToolTipIcon.None);
                panel?.RefreshStats();
            }
        }

        panel?.OnClipboardChanged(text);
    }

    static string Describe(RinseResult r)
    {
        var parts = new List<string>();
        if (r.Unwrapped) parts.Add("unwrapped a redirect");
        if (r.TrackersRemoved > 0) parts.Add($"removed {r.TrackersRemoved} tracker{(r.TrackersRemoved == 1 ? "" : "s")}");
        var what = string.Join(" and ", parts);
        what = char.ToUpper(what[0]) + what[1..] + ".";
        var shown = r.Url.Length > 90 ? r.Url[..87] + "..." : r.Url;
        return what + "\n" + shown;
    }

    public void UndoLastRinse()
    {
        if (undoOriginal == null) return;
        WriteClipboard(undoOriginal);
        history.Add(undoOriginal);
        panel?.OnClipboardChanged(undoOriginal);
        undoOriginal = null;
        undoItem.Enabled = false;
    }

    public bool CanUndoRinse => undoOriginal != null;

    void ShowPanel()
    {
        if (panel == null || panel.IsDisposed)
        {
            panel = new PanelForm(this, settings, history);
            panel.FormClosed += (_, _) => panel = null;
        }
        panel.Show();
        if (panel.WindowState == FormWindowState.Minimized) panel.WindowState = FormWindowState.Normal;
        panel.TopMost = true;
        panel.TopMost = false;
        panel.Activate();
    }

    void Quit()
    {
        panel?.Close();
        tray.Visible = false;
        tray.Dispose();
        messages.Dispose();
        ExitThread();
    }
}
