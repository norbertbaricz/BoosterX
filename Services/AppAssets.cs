namespace BoosterX.App.Services;

internal static class AppAssets
{
    internal static Icon LoadIcon()
    {
        using Stream stream = Open("BoosterX.App.Icon");
        using var source = new Icon(stream);
        return (Icon)source.Clone();
    }
    internal static Bitmap LoadBrandImage()
    {
        // Keep a full-resolution alpha image; scaling a 32px Icon.ToBitmap blurred the header at high DPI.
        using Stream stream = Open("BoosterX.App.Brand");
        using var image = new Bitmap(stream);
        return new Bitmap(image);
    }
    private static Stream Open(string name) => typeof(AppAssets).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("The embedded BoosterX asset is missing: " + name);
}
