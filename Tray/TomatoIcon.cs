using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// System.Drawing and System.Windows.Media declare the same names (Color, Point),
// so only what's needed is taken from the latter, by its full name.
using ImageSource = System.Windows.Media.ImageSource;

namespace Pomidoro.Tray;

// The app icon is drawn in memory so there's no .ico file to ship.
// It's needed in two forms: System.Drawing.Icon for the tray and ImageSource for windows.
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
            // Icon.FromHandle doesn't own the handle, so we take a copy and free the original ourselves.
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

    // Not LibraryImport: the code it generates requires AllowUnsafeBlocks,
    // and enabling unsafe for the whole project isn't worth it for two calls.
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    [DllImport("gdi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);
}
