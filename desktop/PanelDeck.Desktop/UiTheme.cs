using System.Drawing.Drawing2D;

namespace PanelDeck;

internal static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(246, 247, 250), Sidebar = Color.FromArgb(233, 237, 243),
        Ink = Color.FromArgb(32, 39, 51), Muted = Color.FromArgb(105, 116, 133), Border = Color.FromArgb(223, 228, 235),
        Blue = Color.FromArgb(38, 109, 232), Green = Color.FromArgb(36, 135, 99), Purple = Color.FromArgb(121, 89, 208);
    private const string Family = "Microsoft YaHei UI";
    public static Font Font(float size, FontStyle style = FontStyle.Regular) => new(Family, size, style);
    public static UiLabel Label(string text, float size = 10, Color? color = null, bool bold = false) => new() {
        Text = text, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color ?? Ink,
        AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0)
    };
    public static UiButton Button(string text, Action? action = null, bool primary = false) {
        var b = new UiButton { Text = text, Primary = primary };
        if (action != null) b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(b.FindForm(), ex.Message, "曜屏"); } };
        return b;
    }
    public static void ApplyBackground(Control root) {
        foreach (Control child in root.Controls) { if (child is Panel && child.BackColor != Color.White && child.BackColor != Sidebar) child.BackColor = Background; ApplyBackground(child); }
    }
    public static GraphicsPath Round(RectangleF bounds, float radius) {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(bounds.X, bounds.Y, d, d, 180, 90); p.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        p.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); p.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

internal sealed class UiLabel : Label
{
    protected override void Dispose(bool disposing) { var font = Font; base.Dispose(disposing); if (disposing) font.Dispose(); }
    public override Size GetPreferredSize(Size proposedSize) {
        float scale = DeviceDpi / 96f;
        using var font = new Font(Font.FontFamily, Font.SizeInPoints * 96f / 72f * scale, Font.Style, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(1, 1); using var g = Graphics.FromImage(bitmap);
        int maxWidth = MaximumSize.Width > 0 ? MaximumSize.Width : proposedSize.Width > 1 ? proposedSize.Width : 10000;
        var size = g.MeasureString(Text.Length == 0 ? " " : Text, font, Math.Max(1, maxWidth - Padding.Horizontal));
        return new(Math.Min(maxWidth, (int)Math.Ceiling(size.Width) + Padding.Horizontal + 2), (int)Math.Ceiling(size.Height) + Padding.Vertical + 2);
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        float scale = DeviceDpi / 96f;
        using var font = new Font(Font.FontFamily, Font.SizeInPoints * 96f / 72f * scale, Font.Style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ForeColor);
        using var format = new StringFormat { Trimming = AutoEllipsis ? StringTrimming.EllipsisCharacter : StringTrimming.Word };
        e.Graphics.DrawString(Text, font, brush, new RectangleF(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical)), format);
    }
}

internal sealed class UiButton : Button
{
    protected override void Dispose(bool disposing) { var font = Font; base.Dispose(disposing); if (disposing) font.Dispose(); }
    [System.ComponentModel.DefaultValue(false)] public bool Primary { get; set; }
    private bool selected;
    [System.ComponentModel.DefaultValue(false)] public bool Selected { get => selected; set { if (selected != value) { selected = value; Invalidate(); } } }
    private bool hover;
    public UiButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Font = UiTheme.Font(10); Size = new(132, 38);
        Margin = new(0, 0, 10, 0); Cursor = Cursors.Hand; BackColor = UiTheme.Background;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    private Color Surface => Parent is UiCard ? Color.White : Parent?.BackColor ?? BackColor;
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Surface);
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool strong = Primary || Selected;
        Color fill = !Enabled ? Color.FromArgb(230, 233, 239) : strong ? (hover ? Color.FromArgb(26, 90, 201) : UiTheme.Blue) : hover ? Color.FromArgb(230, 236, 247) : Color.White;
        using var path = UiTheme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 9 * (DeviceDpi / 96f));
        using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, path);
        using var pen = new Pen(strong && Enabled ? fill : UiTheme.Border); e.Graphics.DrawPath(pen, path);
        float paintScale = DeviceDpi / 96f;
        using var textFont = new Font(Font.FontFamily, Font.SizeInPoints * 96f / 72f * paintScale, Font.Style, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(!Enabled ? UiTheme.Muted : strong ? Color.White : UiTheme.Ink);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        e.Graphics.DrawString(Text, textFont, textBrush, ClientRectangle, format);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}

internal class UiCard : Panel
{
    protected float PaintScale => DeviceDpi / 96f;
    public UiCard() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = UiTheme.Background; Padding = new(20); }
    protected override void OnPaintBackground(PaintEventArgs e) {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 15 * PaintScale);
        using var brush = new SolidBrush(Color.White); e.Graphics.FillPath(brush, path);
        using var pen = new Pen(UiTheme.Border); e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class MetricCard : UiCard
{
    private readonly Label name = UiTheme.Label("", 11, bold: true), model = UiTheme.Label("等待硬件数据", 9, UiTheme.Muted),
        value = UiTheme.Label("—", 34), unit = UiTheme.Label("使用率", 9, UiTheme.Muted), details = UiTheme.Label("温度 —     功耗 —", 10, UiTheme.Muted);
    private readonly Color accent;
    private float? load;
    public MetricCard(string title, Color color) {
        accent = color; name.Text = title;
        var oldFont = value.Font; value.Font = new Font("Segoe UI Semibold", 34); oldFont.Dispose();
        Controls.AddRange([name, model, value, unit, details]); name.SetBounds(22, 18, 260, 24); model.SetBounds(22, 48, 290, 22);
        value.SetBounds(18, 74, 250, 72); unit.SetBounds(24, 149, 240, 20); details.SetBounds(22, 190, 310, 25);
        foreach (var label in Controls.OfType<Label>()) { label.AutoSize = false; label.AutoEllipsis = true; }
    }
    public void Update(string device, Metric? usage, Metric? temperature, Metric? power) {
        model.Text = device; var nextLoad = usage?.Value;
        if (load != nextLoad) { load = nextLoad; Invalidate(); }
        value.Text = load.HasValue ? $"{load:0.#}%" : "—";
        details.Text = $"温度  {Format(temperature)}      功耗  {Format(power)}";
    }
    internal static string Format(Metric? metric) => metric?.Value is float number ? $"{number:0.#} {metric.Unit}" : "—";
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (model != null) { model.Width = Math.Max(60, Width - (int)(44 * PaintScale)); details.Width = Math.Max(60, Width - (int)(44 * PaintScale)); } }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = PaintScale; float width = Math.Max(1, Width - 44 * scale);
        using var bg = new SolidBrush(Color.FromArgb(237, 240, 245)); using var fg = new SolidBrush(accent);
        using var track = UiTheme.Round(new RectangleF(22 * scale, 171 * scale, width, 6 * scale), 3 * scale); e.Graphics.FillPath(bg, track);
        if (load.HasValue && load > 0) { using var fill = UiTheme.Round(new RectangleF(22 * scale, 171 * scale, Math.Max(6 * scale, width * Math.Clamp(load.Value, 0, 100) / 100), 6 * scale), 3 * scale); e.Graphics.FillPath(fg, fill); }
    }
}
