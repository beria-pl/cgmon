using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace cgmon.Services;

public enum TemperatureLevel { Normal, Warning, Critical }

public static class TemperatureIconRenderer
{
    private const int Size = 32;

    private static readonly Color NormalBg    = Color.FromArgb(30, 30, 35);
    private static readonly Color WarningBg   = Color.FromArgb(190, 110, 0);
    private static readonly Color CriticalBg  = Color.FromArgb(200, 25, 25);
    private static readonly Color TextColor   = Color.White;

    public static Icon CreateIcon(string label, TemperatureLevel level)
    {
        using var bmp = new Bitmap(Size, Size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.Clear(Color.Transparent);

        var bg = level switch
        {
            TemperatureLevel.Warning  => WarningBg,
            TemperatureLevel.Critical => CriticalBg,
            _                         => NormalBg,
        };

        using var bgBrush = new SolidBrush(bg);
        FillRoundedRect(g, bgBrush, 1, 1, Size - 2, Size - 2, 4);

        float fontSize = label.Length switch
        {
            <= 2 => 17f,
            3    => 14f,
            _    => 11f,
        };

        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(TextColor);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.DrawString(label, font, brush, new RectangleF(0, 1, Size, Size - 1), sf);

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static void FillRoundedRect(Graphics g, Brush brush, int x, int y, int w, int h, int r)
    {
        using var path = new GraphicsPath();
        int d = r * 2;
        path.AddArc(x,         y,         d, d, 180, 90);
        path.AddArc(x + w - d, y,         d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d,   0, 90);
        path.AddArc(x,         y + h - d, d, d,  90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
