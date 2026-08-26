namespace DeskStealth.App.Tests;

public sealed class WindowMetricsTests
{
    [Theory]
    [InlineData(96, 256, 160)]
    [InlineData(120, 320, 200)]
    [InlineData(144, 384, 240)]
    [InlineData(192, 512, 320)]
    public void OneXScale_ProducesFixedDipCanvasAtCommonMonitorDpis(int dpi, int width, int height)
    {
        Assert.Equal(new Size(width, height), WindowMetrics.ClientSizeFor(dpi, 1f));
    }

    [Theory]
    [InlineData(1f, 384, 240)]
    [InlineData(1.25f, 480, 300)]
    [InlineData(1.5f, 576, 360)]
    public void UserScale_ComposesWithMonitorDpi(float scale, int width, int height)
    {
        Assert.Equal(new Size(width, height), WindowMetrics.ClientSizeFor(144, scale));
    }

    [Fact]
    public void ClampLocation_ReturnsOffScreenWindowToRemainingWorkingArea()
    {
        var workingArea = new Rectangle(0, 0, 1920, 1040);

        var location = WindowMetrics.ClampLocation(
            workingArea,
            new Size(384, 240),
            new Point(5000, 3000));

        Assert.Equal(new Point(1536, 800), location);
    }

    [Fact]
    public void ClampLocation_HandlesNegativeOriginsAndOversizedWindows()
    {
        var negativeMonitor = new Rectangle(-1600, -200, 1600, 900);
        Assert.Equal(
            new Point(-1600, -200),
            WindowMetrics.ClampLocation(negativeMonitor, new Size(2000, 1000), new Point(0, 0)));
    }
}
