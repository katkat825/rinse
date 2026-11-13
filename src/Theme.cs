namespace Rinse;

static class Theme
{
    public static readonly Color Teal900 = ColorTranslator.FromHtml("#134E4A");
    public static readonly Color Teal700 = ColorTranslator.FromHtml("#0F766E");
    public static readonly Color Teal600 = ColorTranslator.FromHtml("#0D9488");
    public static readonly Color Teal500 = ColorTranslator.FromHtml("#14B8A6");
    public static readonly Color Teal200 = ColorTranslator.FromHtml("#99F6E4");
    public static readonly Color Teal100 = ColorTranslator.FromHtml("#CCFBF1");
    public static readonly Color Teal50 = ColorTranslator.FromHtml("#F0FDFA");
    public static readonly Color Ink = ColorTranslator.FromHtml("#0F172A");
    public static readonly Color Muted = ColorTranslator.FromHtml("#5B6B7A");
    public static readonly Color Line = ColorTranslator.FromHtml("#B7E4DD");

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) => new Font("Segoe UI", size, style);
    public static Font UiSemibold(float size) => new Font("Segoe UI Semibold", size);

    public static Icon AppIcon(Size size)
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream("Rinse.ico");
        return s != null ? new Icon(s, size) : (Icon)SystemIcons.Application.Clone();
    }
}

/// <summary>A panel that paints a soft teal gradient.</summary>
class GradientPanel : Panel
{
    public GradientPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            ClientRectangle, Theme.Teal700, Theme.Teal500, 0f);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
