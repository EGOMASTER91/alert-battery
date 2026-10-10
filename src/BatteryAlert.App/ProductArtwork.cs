using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BatteryAlert.App;

public static class ProductArtwork
{
    private static readonly Dictionary<string, ImageSource> Cache = [];
    public static string ImageDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryAlert", "device-images");

    public static ImageSource Resolve(ProductIdentity identity, string? customFile)
    {
        if (IsSafeFileName(customFile))
        {
            var path = Path.Combine(ImageDirectory, customFile!);
            try { return Load(path); }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FormatException or UnauthorizedAccessException) { }
        }
        if (identity.ImageFile != null)
            return Load($"pack://application:,,,/Assets/Products/{identity.ImageFile}");
        return Generic(identity.Category.Contains("Mouse", StringComparison.Ordinal) ? "mouse"
            : identity.Category.Contains("Tastatur", StringComparison.Ordinal) ? "keyboard" : "headset");
    }

    public static bool IsSafeFileName(string? name) => !string.IsNullOrWhiteSpace(name)
        && name == Path.GetFileName(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static ImageSource Load(string uri)
    {
        if (Cache.TryGetValue(uri, out var cached)) return cached;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 640;
        bitmap.UriSource = new Uri(uri, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        Cache[uri] = bitmap;
        return bitmap;
    }

    public static string Import(string source)
    {
        if (new FileInfo(source).Length > 10 * 1024 * 1024) throw new ArgumentException("Alege o fotografie de maximum 10 MB.");
        using var input = File.OpenRead(source);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth > 8192 || frame.PixelHeight > 8192) throw new ArgumentException("Fotografia poate avea maximum 8192 pixeli pe latură.");
        Directory.CreateDirectory(ImageDirectory);
        // Normalize an imported image to PNG; never retain a reference to an
        // external user file which may be moved or deleted after import.
        var file = Guid.NewGuid().ToString("N") + ".png";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var output = File.Create(Path.Combine(ImageDirectory, file));
        encoder.Save(output);
        return file;
    }

    private static ImageSource Generic(string category)
    {
        var key = "generic:" + category;
        if (Cache.TryGetValue(key, out var existing)) return existing;
        var group = new DrawingGroup();
        var body = new SolidColorBrush(Color.FromRgb(65, 78, 88));
        var line = new Pen(new SolidColorBrush(Color.FromRgb(111, 137, 145)), 3);
        if (category == "keyboard")
        {
            group.Children.Add(new GeometryDrawing(body, line, new RectangleGeometry(new(4, 35, 112, 66), 7, 7)));
            for (var row = 0; row < 3; row++) for (var column = 0; column < 9; column++)
                group.Children.Add(new GeometryDrawing(line.Brush, null, new RectangleGeometry(new(12 + column * 11, 44 + row * 12, 7, 7), 1, 1)));
            group.Children.Add(new GeometryDrawing(line.Brush, null, new RectangleGeometry(new(31, 82, 58, 7), 2, 2)));
        }
        else if (category == "mouse")
        {
            group.Children.Add(new GeometryDrawing(body, line, new EllipseGeometry(new(60, 72), 34, 53)));
            group.Children.Add(new GeometryDrawing(null, line, Geometry.Parse("M60,21 L60,65 M27,67 L93,67")));
            group.Children.Add(new GeometryDrawing(line.Brush, null, new RectangleGeometry(new(56, 32, 8, 21), 4, 4)));
        }
        else
        {
            group.Children.Add(new GeometryDrawing(null, new Pen(body, 12), Geometry.Parse("M22,75 L22,52 C22,6 98,6 98,52 L98,75")));
            group.Children.Add(new GeometryDrawing(body, line, new RectangleGeometry(new(15, 58, 22, 47), 9, 9)));
            group.Children.Add(new GeometryDrawing(body, line, new RectangleGeometry(new(83, 58, 22, 47), 9, 9)));
        }
        var image = new DrawingImage(group); image.Freeze(); Cache[key] = image; return image;
    }
}
