using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// System.Drawing и System.Windows.Media объявляют одинаковые имена (Color, Point),
// поэтому из второго берём только то, что нужно, и по полному имени.
using ImageSource = System.Windows.Media.ImageSource;

namespace Pomidoro.Tray;

// Значок приложения рисуется в памяти, чтобы не таскать с собой .ico-файл.
// Нужен в двух видах: System.Drawing.Icon для трея и ImageSource для окон.
internal static class TomatoIcon
{
    private const int Size = 32;

    private static readonly Lazy<ImageSource> WindowIcon = new(CreateWindowIcon);

    public static ImageSource ForWindow => WindowIcon.Value;

    public static Icon CreateForTray()
    {
        using Bitmap bitmap = Draw();

        nint handle = bitmap.GetHicon();

        try
        {
            // Icon.FromHandle хэндлом не владеет, поэтому снимаем копию и освобождаем оригинал сами.
            using Icon borrowed = Icon.FromHandle(handle);

            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static ImageSource CreateWindowIcon()
    {
        using Bitmap bitmap = Draw();

        nint handle = bitmap.GetHbitmap(Color.FromArgb(0));

        try
        {
            ImageSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                nint.Zero,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();

            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static Bitmap Draw()
    {
        Bitmap bitmap = new(Size, Size);

        using Graphics graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using SolidBrush body = new(Color.FromArgb(229, 72, 77));
        graphics.FillEllipse(body, 2, 8, 28, 23);

        using SolidBrush leaf = new(Color.FromArgb(61, 190, 139));
        graphics.FillPolygon(leaf,
        [
            new Point(16, 1),
            new Point(26, 8),
            new Point(16, 13),
            new Point(6, 8)
        ]);

        return bitmap;
    }

    // Не LibraryImport: сгенерированный им код требует AllowUnsafeBlocks,
    // а ради двух вызовов включать unsafe на весь проект не стоит.
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    [DllImport("gdi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);
}
