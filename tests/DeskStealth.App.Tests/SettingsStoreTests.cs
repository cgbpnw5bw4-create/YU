using DeskStealth.Settings;

namespace DeskStealth.App.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "DeskStealth.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsNormalizedSettingsWithoutLeavingTemporaryFile()
    {
        var store = new SettingsStore(_directory);
        var settings = new AppSettings
        {
            Version = -1,
            WindowX = 123,
            WindowY = 456,
            Scale = 1.25f,
            TopMost = false,
            BestScores = [100, -20, 300],
        };

        Assert.True(store.Save(settings));
        var loaded = store.Load();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(123, loaded.WindowX);
        Assert.Equal(456, loaded.WindowY);
        Assert.Equal(1.25f, loaded.Scale);
        Assert.False(loaded.TopMost);
        Assert.Equal([100, 0, 300], loaded.BestScores);
        Assert.False(File.Exists(store.SettingsPath + ".tmp"));
    }

    [Fact]
    public void Save_ReplacesExistingSettingsAndKeepsOnlyCompleteJson()
    {
        var store = new SettingsStore(_directory);
        Assert.True(store.Save(new AppSettings { BestScores = [100, 200, 300] }));

        Assert.True(store.Save(new AppSettings { BestScores = [400, 500, 600] }));

        Assert.Equal([400, 500, 600], store.Load().BestScores);
        Assert.Single(Directory.GetFiles(_directory, "settings.json"));
        Assert.False(File.Exists(store.SettingsPath + ".tmp"));
    }

    [Fact]
    public void Load_MovesCorruptJsonAsideAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var store = new SettingsStore(_directory);
        File.WriteAllText(store.SettingsPath, "{ this is not valid json");

        var loaded = store.Load();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal([0, 0, 0], loaded.BestScores);
        Assert.False(File.Exists(store.SettingsPath));
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
