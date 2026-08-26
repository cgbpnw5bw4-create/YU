using DeskStealth.Assets;

namespace DeskStealth.App.Tests;

public sealed class EmbeddedAssetTests
{
    [Fact]
    public void ResxContainsCompactCharacterAndOfficeAtlases()
    {
        using var characters = GameAssets.LoadBitmap("Characters");
        using var officeTiles = GameAssets.LoadBitmap("OfficeTiles");

        Assert.Equal(new Size(80, 64), characters.Size);
        Assert.Equal(new Size(80, 32), officeTiles.Size);
        AssertBinaryAlpha(characters);
        AssertBinaryAlpha(officeTiles);
    }

    private static void AssertBinaryAlpha(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                Assert.Contains(bitmap.GetPixel(x, y).A, new byte[] { 0, 255 });
            }
        }
    }
}
