using System.Runtime.InteropServices;

namespace Rinse;

static class Clip
{
    public static string GetText()
    {
        for (int i = 0; i < 6; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch (ExternalException) { Thread.Sleep(40); }
        }
        return null;
    }

    public static bool SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            // Plain Unicode text only: this is also what strips formatting.
            Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true, 8, 50);
            return true;
        }
        catch (ExternalException) { return false; }
    }

    /// <summary>
    /// Password managers mark secrets with these formats so clipboard tools leave them alone. We do.
    /// </summary>
    public static bool IsMarkedPrivate()
    {
        try
        {
            var d = Clipboard.GetDataObject();
            if (d == null) return false;
            var formats = d.GetFormats(false);
            return formats.Contains("ExcludeClipboardContentFromMonitorProcessing")
                || formats.Contains("Clipboard Viewer Ignore");
        }
        catch { return false; }
    }
}

/// <summary>Invisible window that receives clipboard-change and global hotkey messages.</summary>
sealed class MessageWindow : NativeWindow, IDisposable
{
    const int WM_CLIPBOARDUPDATE = 0x031D;
    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    const uint VK_V = 0x56;
    const int HotkeyId = 1;

    [DllImport("user32.dll", SetLastError = true)] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event Action ClipboardChanged;
    public event Action HotkeyPressed;
    public string HotkeyText { get; private set; }

    public MessageWindow()
    {
        CreateHandle(new CreateParams());
        AddClipboardFormatListener(Handle);
        if (RegisterHotKey(Handle, HotkeyId, MOD_WIN | MOD_ALT | MOD_NOREPEAT, VK_V)) HotkeyText = "Win+Alt+V";
        else if (RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_V)) HotkeyText = "Ctrl+Alt+Shift+V";
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) ClipboardChanged?.Invoke();
        else if (m.Msg == WM_HOTKEY) HotkeyPressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        UnregisterHotKey(Handle, HotkeyId);
        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}

class ClipItem
{
    public string Text { get; init; }
    public DateTime Time { get; init; } = DateTime.Now;
}

/// <summary>Recent copies, kept in memory only. Nothing is written to disk.</summary>
class ClipHistory
{
    const int Max = 25;
    readonly List<ClipItem> items = new();
    public IReadOnlyList<ClipItem> Items => items;
    public event Action Changed;

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        items.RemoveAll(i => i.Text == text);
        items.Insert(0, new ClipItem { Text = text });
        if (items.Count > Max) items.RemoveRange(Max, items.Count - Max);
        Changed?.Invoke();
    }

    public void ReplaceLatest(string text)
    {
        if (items.Count == 0) { Add(text); return; }
        items[0] = new ClipItem { Text = text, Time = items[0].Time };
        Changed?.Invoke();
    }

    public void Clear() { items.Clear(); Changed?.Invoke(); }
}
