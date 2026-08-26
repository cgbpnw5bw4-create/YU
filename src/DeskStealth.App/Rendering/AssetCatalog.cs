using DeskStealth.Assets;

namespace DeskStealth.Rendering;

internal sealed class AssetCatalog : IDisposable
{
    public AssetCatalog()
    {
        Characters = SpriteSheet.FromBitmap(GameAssets.LoadBitmap("Characters"), columns: 5, rows: 4);
        OfficeTiles = SpriteSheet.FromBitmap(GameAssets.LoadBitmap("OfficeTiles"), columns: 5, rows: 2);
    }

    public SpriteSheet? Characters { get; }

    public SpriteSheet? OfficeTiles { get; }

    public void Dispose()
    {
        Characters?.Dispose();
        OfficeTiles?.Dispose();
    }
}
