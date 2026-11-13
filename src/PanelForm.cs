namespace Rinse;

class PanelForm : Form
{
    readonly TrayApp app;
    readonly Settings settings;
    readonly ClipHistory history;
    readonly TextBox editor;
    readonly ListBox list;
    readonly Label status, stats;
    readonly CheckBox autoBox;
    readonly Button undoButton;
    readonly Stack<string> undo = new();
    readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 4500 };
    readonly string idleStatus;
    bool dirty, settingText;

    public PanelForm(TrayApp app, Settings settings, ClipHistory history)
    {
        this.app = app;
        this.settings = settings;
        this.history = history;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Rinse";
        Icon = Theme.AppIcon(new Size(32, 32));
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1010, 640);
        MinimumSize = new Size(720, 500);
        BackColor = Theme.Teal50;
        ForeColor = Theme.Ink;
        Font = Theme.Ui(10f);
        KeyPreview = true;

        idleStatus = app.HotkeyText != null
            ? $"Tip: press {app.HotkeyText} anytime to open Rinse. Closing this window keeps it running in the tray."
            : "Tip: click the teal drop in the tray to open Rinse. Closing this window keeps it running.";

        // ---------- Header ----------
        var header = new GradientPanel { Dock = DockStyle.Top, Height = 78 };
        var logo = new PictureBox
        {
            Image = Theme.AppIcon(new Size(48, 48)).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(44, 44), Location = new Point(20, 17),
            BackColor = Color.Transparent,
        };
        var title = new Label
        {
            Text = "Rinse", Font = Theme.UiSemibold(20f), ForeColor = Color.White,
            AutoSize = true, Location = new Point(72, 9), BackColor = Color.Transparent,
        };
        var tagline = new Label
        {
            Text = "Your clipboard, minus the junk.", ForeColor = Theme.Teal100, Font = Theme.Ui(9.5f),
            AutoSize = true, Location = new Point(75, 47), BackColor = Color.Transparent,
        };
        stats = new Label
        {
            Dock = DockStyle.Right, Width = 380, TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.White, BackColor = Color.Transparent, Font = Theme.Ui(10f),
            Padding = new Padding(0, 0, 22, 0),
        };
        header.Controls.Add(stats);
        header.Controls.Add(logo);
        header.Controls.Add(title);
        header.Controls.Add(tagline);

        // ---------- Footer ----------
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.White, Padding = new Padding(16, 0, 12, 0) };
        footer.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
        status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Text = idleStatus, AutoEllipsis = true };
        autoBox = new CheckBox
        {
            Text = "Auto-rinse copied links", AutoSize = true, Dock = DockStyle.Right,
            ForeColor = Theme.Teal900, Checked = settings.AutoRinse, Padding = new Padding(12, 0, 0, 0),
        };
        autoBox.CheckedChanged += (_, _) => { settings.AutoRinse = autoBox.Checked; settings.Save(); };
        footer.Controls.Add(status);
        footer.Controls.Add(autoBox);

        // ---------- Body ----------
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(18, 14, 18, 12) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Left: editor + tools
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 14, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(Caption("ON YOUR CLIPBOARD  ·  edit freely, then click a tool"), 0, 0);

        editor = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, WordWrap = true,
            Font = Theme.Ui(10.5f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
            ForeColor = Theme.Ink, MaxLength = int.MaxValue, Margin = new Padding(0, 4, 0, 6),
            PlaceholderText = "Copy something (a link, a paragraph from a PDF, a list...) and it shows up here.",
        };
        editor.TextChanged += (_, _) => { if (!settingText) dirty = true; };
        left.Controls.Add(editor, 0, 1);

        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0) };
        AddGroup(tools, "CLEAN",
            Btn("Plain text", () => CopyOut(editor.Text, "✓ Copied as plain text (formatting stripped)")),
            Btn("Fix line breaks", () => Apply("Fixed line breaks", TextTools.FixLineBreaks)),
            Btn("Tidy spaces", () => Apply("Tidied spaces", TextTools.TidySpaces)),
            Btn("Fix quotes", () => Apply("Straightened quotes", TextTools.StraightQuotes)),
            Btn("Rinse links", RinseLinks));
        AddGroup(tools, "CASE",
            Btn("UPPER", () => Apply("UPPERCASED", TextTools.Upper)),
            Btn("lower", () => Apply("lowercased", TextTools.Lower)),
            Btn("Title Case", () => Apply("Title Cased", TextTools.TitleCase)),
            Btn("Sentence case", () => Apply("Sentence cased", TextTools.SentenceCase)));
        undoButton = Btn("↶ Undo", Undo);
        undoButton.Enabled = false;
        AddGroup(tools, "LINES",
            Btn("Sort A→Z", () => Apply("Sorted lines", TextTools.SortLines)),
            Btn("Remove duplicates", () => Apply("Removed duplicate lines", TextTools.RemoveDuplicateLines)),
            Btn("Remove blank lines", () => Apply("Removed blank lines", TextTools.RemoveBlankLines)),
            undoButton,
            Btn("Copy  (Ctrl+Enter)", () => CopyOut(editor.Text, "✓ Copied to clipboard"), primary: true));
        left.Controls.Add(tools, 0, 2);

        // Right: history
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(Caption("RECENT COPIES"), 0, 0);
        var clear = new LinkLabel
        {
            Text = "Clear", AutoSize = true, LinkColor = Theme.Teal700, ActiveLinkColor = Theme.Teal900,
            LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 0, 0, 0), Font = Theme.Ui(9f),
        };
        clear.LinkClicked += (_, _) => history.Clear();
        right.Controls.Add(clear, 1, 0);

        list = new ListBox
        {
            Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 48, IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Margin = new Padding(0, 4, 0, 6),
        };
        list.DrawItem += DrawHistoryItem;
        list.MouseClick += (_, e) =>
        {
            int i = list.IndexFromPoint(e.Location);
            if (i >= 0 && i < history.Items.Count) Restore(history.Items[i].Text);
        };
        right.Controls.Add(list, 0, 1);
        right.SetColumnSpan(list, 2);
        var privacy = new Label
        {
            Text = "Kept in memory only, never saved to disk. Items marked private by password managers are skipped.",
            AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Theme.Muted, Font = Theme.Ui(8.5f),
        };
        right.Controls.Add(privacy, 0, 2);
        right.SetColumnSpan(privacy, 2);

        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
        ResumeLayout(true);

        statusTimer.Tick += (_, _) => { statusTimer.Stop(); status.ForeColor = Theme.Muted; status.Text = idleStatus; };
        history.Changed += RefreshHistory;
        FormClosed += (_, _) => { history.Changed -= RefreshHistory; statusTimer.Dispose(); };
        Load += (_, _) =>
        {
            list.ItemHeight = (int)Math.Round(48 * DeviceDpi / 96.0);
            privacy.MaximumSize = new Size(Math.Max(200, list.Width), 0);
        };
        right.Resize += (_, _) => privacy.MaximumSize = new Size(Math.Max(200, right.Width - 4), 0);

        RefreshHistory();
        RefreshStats();
        var current = Clip.GetText();
        if (current != null && !Clip.IsMarkedPrivate()) SetEditor(current);
    }

    // ---------- Building blocks ----------

    static Label Caption(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = Theme.Teal700, Font = Theme.Ui(8.5f, FontStyle.Bold),
        Margin = new Padding(0, 2, 0, 0),
    };

    static void AddGroup(FlowLayoutPanel panel, string caption, params Button[] buttons)
    {
        var label = new Label
        {
            Text = caption, AutoSize = false, Width = 58, Height = 34, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted, Font = Theme.Ui(8f, FontStyle.Bold), Margin = new Padding(0, 4, 2, 4),
        };
        panel.Controls.Add(label);
        foreach (var b in buttons) panel.Controls.Add(b);
        panel.SetFlowBreak(buttons[^1], true);
    }

    static Button Btn(string text, Action click, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, 34),
            FlatStyle = FlatStyle.Flat, Padding = new Padding(8, 0, 8, 0), Margin = new Padding(0, 4, 6, 4),
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
            BackColor = primary ? Theme.Teal600 : Color.White,
            ForeColor = primary ? Color.White : Theme.Teal900,
            Font = primary ? Theme.UiSemibold(10f) : Theme.Ui(10f),
        };
        b.FlatAppearance.BorderColor = primary ? Theme.Teal600 : Theme.Line;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = primary ? Theme.Teal700 : Theme.Teal100;
        b.FlatAppearance.MouseDownBackColor = primary ? Theme.Teal900 : Theme.Teal200;
        b.Click += (_, _) => click();
        return b;
    }

    void DrawHistoryItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= history.Items.Count) return;
        var item = history.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var g = e.Graphics;
        using (var bg = new SolidBrush(selected ? Theme.Teal100 : Color.White)) g.FillRectangle(bg, e.Bounds);
        using (var sep = new Pen(Theme.Teal50)) g.DrawLine(sep, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        int pad = (int)(10 * DeviceDpi / 96.0);
        var firstLine = item.Text.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
        int lines = item.Text.Split('\n').Length;
        bool isLink = LinkCleaner.IsSingleUrl(item.Text);
        var meta = $"{item.Time:t}  ·  " + (isLink ? "link" : lines > 1 ? $"{lines} lines" : $"{item.Text.Length} chars");

        int half = e.Bounds.Height / 2;
        var top = new Rectangle(e.Bounds.Left + pad, e.Bounds.Top + 3, e.Bounds.Width - pad * 2, half - 1);
        var bottom = new Rectangle(e.Bounds.Left + pad, e.Bounds.Top + half, e.Bounds.Width - pad * 2, half - 4);
        TextRenderer.DrawText(g, firstLine, Font, top, Theme.Ink,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix);
        using var small = Theme.Ui(8.5f);
        TextRenderer.DrawText(g, meta, small, bottom, isLink ? Theme.Teal700 : Theme.Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
    }

    // ---------- Behaviour ----------

    void SetEditor(string text)
    {
        settingText = true;
        editor.Text = text;
        editor.SelectionStart = 0;
        settingText = false;
        dirty = false;
    }

    void Apply(string done, Func<string, string> op)
    {
        var src = editor.Text;
        if (string.IsNullOrEmpty(src)) { Flash("Nothing to work on yet. Copy some text first.", warn: true); return; }
        var result = op(src);
        if (result == src) { CopyOut(result, $"Already tidy, nothing to change. (Copied anyway.)"); return; }
        Push(src);
        SetEditor(result);
        CopyOut(result, $"✓ {done}, copied to clipboard");
    }

    void RinseLinks()
    {
        var src = editor.Text;
        if (string.IsNullOrEmpty(src)) { Flash("Nothing to work on yet. Copy some text first.", warn: true); return; }
        var result = LinkCleaner.CleanAllInText(src, out int links, out int trackers);
        if (links == 0) { CopyOut(src, "No tracking junk found in these links. (Copied anyway.)"); return; }
        Push(src);
        SetEditor(result);
        settings.LinksRinsed += links;
        settings.TrackersRemoved += trackers;
        settings.Save();
        RefreshStats();
        CopyOut(result, $"✓ Rinsed {links} link{(links == 1 ? "" : "s")}, removed {trackers} tracker{(trackers == 1 ? "" : "s")}, copied");
    }

    void Push(string previous)
    {
        undo.Push(previous);
        undoButton.Enabled = true;
    }

    void Undo()
    {
        if (undo.Count == 0) return;
        var prev = undo.Pop();
        SetEditor(prev);
        undoButton.Enabled = undo.Count > 0;
        CopyOut(prev, "↶ Undone, previous text is back on the clipboard");
    }

    void CopyOut(string text, string message)
    {
        if (string.IsNullOrEmpty(text)) { Flash("Nothing to copy yet.", warn: true); return; }
        if (app.WriteClipboard(text)) { dirty = false; Flash(message); }
        else Flash("Another app is holding the clipboard. Try again in a moment.", warn: true);
    }

    void Restore(string text)
    {
        SetEditor(text);
        CopyOut(text, "✓ Put back on your clipboard");
    }

    void Flash(string message, bool warn = false)
    {
        status.ForeColor = warn ? Color.FromArgb(0xB4, 0x53, 0x09) : Theme.Teal700;
        status.Text = message;
        statusTimer.Stop();
        statusTimer.Start();
    }

    public void OnClipboardChanged(string text)
    {
        if (!dirty) { SetEditor(text); undo.Clear(); undoButton.Enabled = false; }
    }

    public void RefreshStats()
    {
        stats.Text = $"{settings.LinksRinsed:N0} link{(settings.LinksRinsed == 1 ? "" : "s")} rinsed   ·   " +
                     $"{settings.TrackersRemoved:N0} tracker{(settings.TrackersRemoved == 1 ? "" : "s")} removed";
    }

    public void SyncSettings() => autoBox.Checked = settings.AutoRinse;

    void RefreshHistory()
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var i in history.Items) list.Items.Add(i);
        list.EndUpdate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter)) { CopyOut(editor.Text, "✓ Copied to clipboard"); return true; }
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData == (Keys.Control | Keys.A) && editor.Focused) { editor.SelectAll(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
