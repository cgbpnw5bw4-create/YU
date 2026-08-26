using System.Resources;

namespace DeskStealth.Assets;

internal static class GameAssets
{
    private static readonly ResourceManager ResourceManager = new(
        "DeskStealth.Assets.GameAssets",
        typeof(GameAssets).Assembly);

    public static Bitmap LoadBitmap(string name)
    {
        var resource = ResourceManager.GetObject(name);
        return resource switch
        {
            Bitmap bitmap => new Bitmap(bitmap),
            byte[] bytes => LoadBitmap(bytes),
            _ => throw new InvalidOperationException($"Embedded bitmap resource '{name}' was not found.")
        };
    }

    private static Bitmap LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }
}
